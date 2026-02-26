using MediatR;

namespace ReadyStackGo.Application.UseCases.Health.GetServiceHealth;

/// <summary>
/// Query to get detailed health information for a specific service within a deployment.
/// Returns the service health with full health check entries.
/// </summary>
public record GetServiceHealthQuery(
    string EnvironmentId,
    string DeploymentId,
    string ServiceName,
    bool ForceRefresh = false) : IRequest<GetServiceHealthResponse>;
