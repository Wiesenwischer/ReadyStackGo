using MediatR;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Application.UseCases.Environments.TestConnection;

public class TestConnectionHandler : IRequestHandler<TestConnectionCommand, TestConnectionResult>
{
    private readonly IDockerService _dockerService;

    public TestConnectionHandler(IDockerService dockerService)
    {
        _dockerService = dockerService;
    }

    public async Task<TestConnectionResult> Handle(TestConnectionCommand request, CancellationToken cancellationToken)
    {
        return await _dockerService.TestConnectionAsync(request.DockerHost, cancellationToken);
    }
}
