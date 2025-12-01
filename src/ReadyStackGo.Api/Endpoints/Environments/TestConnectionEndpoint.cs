using FastEndpoints;
using MediatR;
using ReadyStackGo.Application.UseCases.Environments.TestConnection;

namespace ReadyStackGo.API.Endpoints.Environments;

public class TestConnectionRequest
{
    public string DockerHost { get; set; } = null!;
}

public class TestConnectionResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = null!;
    public string? DockerVersion { get; set; }
}

public class TestConnectionEndpoint : Endpoint<TestConnectionRequest, TestConnectionResponse>
{
    private readonly IMediator _mediator;

    public TestConnectionEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("/api/environments/test-connection");
        Description(b => b.WithTags("Environments"));
    }

    public override async Task HandleAsync(TestConnectionRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.DockerHost))
        {
            Response = new TestConnectionResponse
            {
                Success = false,
                Message = "Docker host URL is required"
            };
            return;
        }

        var result = await _mediator.Send(new TestConnectionCommand(req.DockerHost), ct);

        Response = new TestConnectionResponse
        {
            Success = result.Success,
            Message = result.Message,
            DockerVersion = result.DockerVersion
        };
    }
}
