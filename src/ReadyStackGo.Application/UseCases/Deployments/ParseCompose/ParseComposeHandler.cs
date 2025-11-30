using MediatR;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Application.UseCases.Deployments.ParseCompose;

public class ParseComposeHandler : IRequestHandler<ParseComposeCommand, ParseComposeResponse>
{
    private readonly IDeploymentService _deploymentService;

    public ParseComposeHandler(IDeploymentService deploymentService)
    {
        _deploymentService = deploymentService;
    }

    public async Task<ParseComposeResponse> Handle(ParseComposeCommand request, CancellationToken cancellationToken)
    {
        var parseRequest = new ParseComposeRequest { YamlContent = request.YamlContent };
        return await _deploymentService.ParseComposeAsync(parseRequest);
    }
}
