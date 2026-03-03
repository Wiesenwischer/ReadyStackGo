using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.UseCases.Deployments.StopProductContainers;
using ReadyStackGo.Domain.Deployment.ProductDeployments;

namespace ReadyStackGo.Application.UseCases.Hooks.StopContainers;

public record StopContainersViaHookRequest
{
    public required string ProductId { get; init; }
    public string? StackDefinitionName { get; init; }
    public string? EnvironmentId { get; init; }
}

public record StopContainersViaHookResponse
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public int TotalStacks { get; init; }
    public int StoppedStacks { get; init; }
    public int FailedStacks { get; init; }

    public static StopContainersViaHookResponse Failed(string message) =>
        new() { Success = false, Message = message };
}

public record StopContainersViaHookCommand(
    string ProductId,
    string? StackDefinitionName,
    string EnvironmentId
) : IRequest<StopContainersViaHookResponse>;

public class StopContainersViaHookHandler : IRequestHandler<StopContainersViaHookCommand, StopContainersViaHookResponse>
{
    private readonly IProductDeploymentRepository _productDeploymentRepository;
    private readonly IMediator _mediator;
    private readonly ILogger<StopContainersViaHookHandler> _logger;

    public StopContainersViaHookHandler(
        IProductDeploymentRepository productDeploymentRepository,
        IMediator mediator,
        ILogger<StopContainersViaHookHandler> logger)
    {
        _productDeploymentRepository = productDeploymentRepository;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<StopContainersViaHookResponse> Handle(
        StopContainersViaHookCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ProductId))
        {
            return StopContainersViaHookResponse.Failed("ProductId is required.");
        }

        if (!Guid.TryParse(request.EnvironmentId, out var envGuid))
        {
            return StopContainersViaHookResponse.Failed("Invalid EnvironmentId format.");
        }

        // Resolve active product deployment
        var productDeployment = _productDeploymentRepository.GetActiveByProductGroupId(
            new Domain.Deployment.Environments.EnvironmentId(envGuid), request.ProductId);

        if (productDeployment == null)
        {
            return StopContainersViaHookResponse.Failed(
                $"No active product deployment found for productId '{request.ProductId}' in environment '{request.EnvironmentId}'.");
        }

        // Determine stack names filter
        List<string>? stackNames = null;
        if (!string.IsNullOrWhiteSpace(request.StackDefinitionName))
        {
            stackNames = new List<string> { request.StackDefinitionName };
        }

        _logger.LogInformation(
            "Hook: Stopping containers for product '{ProductId}' (deployment {ProductDeploymentId}), stacks: {Stacks}",
            request.ProductId, productDeployment.Id,
            stackNames != null ? string.Join(", ", stackNames) : "all");

        // Dispatch to the core stop command
        var result = await _mediator.Send(new StopProductContainersCommand(
            request.EnvironmentId,
            productDeployment.Id.Value.ToString(),
            stackNames), cancellationToken);

        return new StopContainersViaHookResponse
        {
            Success = result.Success,
            Message = result.Message,
            TotalStacks = result.TotalStacks,
            StoppedStacks = result.StoppedStacks,
            FailedStacks = result.FailedStacks
        };
    }
}
