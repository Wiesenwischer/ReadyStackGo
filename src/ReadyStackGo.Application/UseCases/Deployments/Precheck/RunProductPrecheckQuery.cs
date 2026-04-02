using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.Deployment.Precheck;
using ReadyStackGo.Domain.Deployment.ProductDeployments;

namespace ReadyStackGo.Application.UseCases.Deployments.Precheck;

/// <summary>
/// Per-stack configuration for a product precheck request.
/// </summary>
public record ProductPrecheckStackConfig(
    string StackId,
    Dictionary<string, string> Variables);

/// <summary>
/// Query to run deployment precheck for all stacks in a product.
/// </summary>
public record RunProductPrecheckQuery(
    string EnvironmentId,
    string ProductId,
    string DeploymentName,
    List<ProductPrecheckStackConfig> StackConfigs,
    Dictionary<string, string> SharedVariables
) : IRequest<ProductPrecheckResult>;

/// <summary>
/// Handler that orchestrates per-stack prechecks in parallel and aggregates results.
/// </summary>
public class RunProductPrecheckHandler : IRequestHandler<RunProductPrecheckQuery, ProductPrecheckResult>
{
    private readonly IProductSourceService _productSourceService;
    private readonly IMediator _mediator;
    private readonly ILogger<RunProductPrecheckHandler> _logger;

    public RunProductPrecheckHandler(
        IProductSourceService productSourceService,
        IMediator mediator,
        ILogger<RunProductPrecheckHandler> logger)
    {
        _productSourceService = productSourceService;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<ProductPrecheckResult> Handle(RunProductPrecheckQuery request, CancellationToken cancellationToken)
    {
        // 1. Load product from catalog
        var product = await _productSourceService.GetProductAsync(request.ProductId, cancellationToken);
        if (product == null)
        {
            return new ProductPrecheckResult([new ProductPrecheckStackResult(
                request.ProductId,
                request.ProductId,
                new PrecheckResult([new PrecheckItem(
                    "Product",
                    PrecheckSeverity.Error,
                    "Product not found",
                    $"Product '{request.ProductId}' not found in catalog")]))]);
        }

        // 2. Validate stack configs
        if (request.StackConfigs.Count == 0)
        {
            return new ProductPrecheckResult([new ProductPrecheckStackResult(
                request.ProductId,
                product.Name,
                new PrecheckResult([new PrecheckItem(
                    "Product",
                    PrecheckSeverity.Error,
                    "No stack configurations provided",
                    "At least one stack configuration is required for product precheck")]))]);
        }

        // 3. Run precheck for each stack sequentially
        // Sequential execution is required because the MediatR handlers use EF Core's
        // DbContext which is not thread-safe. Docker API calls within each stack
        // precheck still run in parallel.
        var stackResults = new List<ProductPrecheckStackResult>();
        foreach (var stackConfig in request.StackConfigs)
        {
            var stackDef = product.Stacks.FirstOrDefault(s =>
                s.Id.Value.Equals(stackConfig.StackId, StringComparison.OrdinalIgnoreCase));

            if (stackDef == null)
            {
                stackResults.Add(new ProductPrecheckStackResult(
                    stackConfig.StackId,
                    stackConfig.StackId,
                    new PrecheckResult([new PrecheckItem(
                        "StackDefinition",
                        PrecheckSeverity.Error,
                        "Stack not found",
                        $"Stack '{stackConfig.StackId}' not found in product '{product.Name}'")])));
                continue;
            }

            // Merge variables: defaults → shared → per-stack
            var mergedVariables = MergeVariables(stackDef, request.SharedVariables, stackConfig.Variables);
            var stackDeploymentName = ProductDeployment.DeriveStackDeploymentName(
                request.DeploymentName, stackDef.Name);

            try
            {
                var result = await _mediator.Send(
                    new RunDeploymentPrecheckQuery(
                        request.EnvironmentId,
                        stackConfig.StackId,
                        stackDeploymentName,
                        mergedVariables),
                    cancellationToken);

                stackResults.Add(new ProductPrecheckStackResult(stackConfig.StackId, stackDef.Name, result));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Precheck failed for stack '{StackName}' in product '{ProductName}'",
                    stackDef.Name, product.Name);

                stackResults.Add(new ProductPrecheckStackResult(
                    stackConfig.StackId,
                    stackDef.Name,
                    new PrecheckResult([new PrecheckItem(
                        "PrecheckExecution",
                        PrecheckSeverity.Warning,
                        "Precheck failed",
                        $"Could not complete precheck: {ex.Message}")])));
            }
        }

        _logger.LogInformation(
            "Product precheck completed for '{ProductName}': {StackCount} stacks, {ErrorCount} error(s), {WarningCount} warning(s)",
            product.Name,
            stackResults.Count,
            stackResults.Sum(s => s.Result.ErrorCount),
            stackResults.Sum(s => s.Result.WarningCount));

        return new ProductPrecheckResult(stackResults);
    }

    private static Dictionary<string, string> MergeVariables(
        Domain.StackManagement.Stacks.StackDefinition stackDef,
        Dictionary<string, string> sharedVariables,
        Dictionary<string, string> perStackVariables)
    {
        var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // 1. Stack definition defaults
        foreach (var variable in stackDef.Variables)
        {
            if (!string.IsNullOrEmpty(variable.DefaultValue))
            {
                merged[variable.Name] = variable.DefaultValue;
            }
        }

        // 2. Shared variables (product-level)
        foreach (var kvp in sharedVariables)
        {
            merged[kvp.Key] = kvp.Value;
        }

        // 3. Per-stack overrides
        foreach (var kvp in perStackVariables)
        {
            merged[kvp.Key] = kvp.Value;
        }

        return merged;
    }
}
