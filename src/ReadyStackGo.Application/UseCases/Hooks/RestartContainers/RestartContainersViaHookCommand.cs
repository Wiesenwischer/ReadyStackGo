using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Deployments.RestartProductContainers;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.ProductDeployments;

namespace ReadyStackGo.Application.UseCases.Hooks.RestartContainers;

public record RestartContainersViaHookRequest
{
    public required string ProductId { get; init; }
    public string? StackDefinitionName { get; init; }
    public string? EnvironmentId { get; init; }
    public string? EnvironmentName { get; init; }
}

public record RestartContainersViaHookResponse
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public int TotalStacks { get; init; }
    public int RestartedStacks { get; init; }
    public int FailedStacks { get; init; }

    public static RestartContainersViaHookResponse Failed(string message) =>
        new() { Success = false, Message = message };
}

public record RestartContainersViaHookCommand(
    string ProductId,
    string? StackDefinitionName,
    string EnvironmentId,
    string? EnvironmentName = null
) : IRequest<RestartContainersViaHookResponse>;

public class RestartContainersViaHookHandler : IRequestHandler<RestartContainersViaHookCommand, RestartContainersViaHookResponse>
{
    private readonly IProductDeploymentRepository _productDeploymentRepository;
    private readonly IEnvironmentRepository _environmentRepository;
    private readonly IMediator _mediator;
    private readonly ILogger<RestartContainersViaHookHandler> _logger;

    public RestartContainersViaHookHandler(
        IProductDeploymentRepository productDeploymentRepository,
        IEnvironmentRepository environmentRepository,
        IMediator mediator,
        ILogger<RestartContainersViaHookHandler> logger)
    {
        _productDeploymentRepository = productDeploymentRepository;
        _environmentRepository = environmentRepository;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<RestartContainersViaHookResponse> Handle(
        RestartContainersViaHookCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ProductId))
        {
            return RestartContainersViaHookResponse.Failed("ProductId is required.");
        }

        var (resolvedEnvId, envError) = EnvironmentResolver.Resolve(request.EnvironmentId, request.EnvironmentName, _environmentRepository);
        if (resolvedEnvId == null)
        {
            return RestartContainersViaHookResponse.Failed(envError!);
        }

        // Resolve active product deployment
        var productDeployment = _productDeploymentRepository.GetActiveByProductGroupId(
            resolvedEnvId, request.ProductId);

        if (productDeployment == null)
        {
            return RestartContainersViaHookResponse.Failed(
                $"No active product deployment found for productId '{request.ProductId}' in environment '{request.EnvironmentId}'.");
        }

        // Determine stack names filter
        List<string>? stackNames = null;
        if (!string.IsNullOrWhiteSpace(request.StackDefinitionName))
        {
            stackNames = new List<string> { request.StackDefinitionName };
        }

        _logger.LogInformation(
            "Hook: Restarting containers for product '{ProductId}' (deployment {ProductDeploymentId}), stacks: {Stacks}",
            request.ProductId, productDeployment.Id,
            stackNames != null ? string.Join(", ", stackNames) : "all");

        // Dispatch to the core restart command
        var result = await _mediator.Send(new RestartProductContainersCommand(
            request.EnvironmentId,
            productDeployment.Id.Value.ToString(),
            stackNames), cancellationToken);

        return new RestartContainersViaHookResponse
        {
            Success = result.Success,
            Message = result.Message,
            TotalStacks = result.TotalStacks,
            RestartedStacks = result.RestartedStacks,
            FailedStacks = result.FailedStacks
        };
    }
}
