using MediatR;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Application.UseCases.Deployments.RemoveDeployment;

public record RemoveDeploymentCommand(string EnvironmentId, string StackName, string? SessionId = null) : IRequest<DeployComposeResponse>;

public record RemoveDeploymentByIdCommand(string EnvironmentId, string DeploymentId, string? SessionId = null) : IRequest<DeployComposeResponse>;
