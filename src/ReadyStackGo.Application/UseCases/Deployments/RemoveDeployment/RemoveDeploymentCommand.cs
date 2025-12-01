using MediatR;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Application.UseCases.Deployments.RemoveDeployment;

public record RemoveDeploymentCommand(string EnvironmentId, string StackName) : IRequest<DeployComposeResponse>;
