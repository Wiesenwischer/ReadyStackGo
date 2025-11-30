using MediatR;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Application.UseCases.Deployments.DeployCompose;

public record DeployComposeCommand(
    string EnvironmentId,
    string StackName,
    string YamlContent,
    Dictionary<string, string> Variables
) : IRequest<DeployComposeResponse>;
