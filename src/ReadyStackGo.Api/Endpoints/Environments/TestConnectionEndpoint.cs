using FastEndpoints;
using ReadyStackGo.Application.Containers;

namespace ReadyStackGo.API.Endpoints.Environments;

/// <summary>
/// Request to test a Docker host connection.
/// </summary>
public class TestConnectionRequest
{
    /// <summary>
    /// The Docker host URL to test.
    /// Examples: "unix:///var/run/docker.sock", "npipe:////./pipe/docker_engine", "tcp://192.168.1.10:2375"
    /// </summary>
    public string DockerHost { get; set; } = null!;
}

/// <summary>
/// Response from testing a Docker host connection.
/// </summary>
public class TestConnectionResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = null!;
    public string? DockerVersion { get; set; }
}

/// <summary>
/// POST /api/environments/test-connection - Test connection to a Docker host
/// </summary>
public class TestConnectionEndpoint : Endpoint<TestConnectionRequest, TestConnectionResponse>
{
    public IDockerService DockerService { get; set; } = null!;

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

        var result = await DockerService.TestConnectionAsync(req.DockerHost, ct);

        Response = new TestConnectionResponse
        {
            Success = result.Success,
            Message = result.Message,
            DockerVersion = result.DockerVersion
        };
    }
}
