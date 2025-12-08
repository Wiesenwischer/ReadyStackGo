using MediatR;

namespace ReadyStackGo.Application.UseCases.Health.GetStackHealth;

/// <summary>
/// Query to get detailed health information for a specific stack deployment.
/// </summary>
public record GetStackHealthQuery(
    string EnvironmentId,
    string DeploymentId,
    bool ForceRefresh = false) : IRequest<GetStackHealthResponse>;
