using MediatR;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Application.UseCases.Deployments.GetDeployment;

public record GetDeploymentQuery(string EnvironmentId, string StackName) : IRequest<GetDeploymentResponse>;
