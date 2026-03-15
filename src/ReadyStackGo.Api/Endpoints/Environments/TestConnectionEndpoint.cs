using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Environments.TestConnection;

namespace ReadyStackGo.API.Endpoints.Environments;

public class TestConnectionRequest
{
    /// <summary>
    /// Connection type: "DockerSocket" (default) or "SshTunnel"
    /// </summary>
    public string Type { get; set; } = "DockerSocket";

    // DockerSocket fields
    public string? DockerHost { get; set; }

    // SSH Tunnel fields
    public string? SshHost { get; set; }
    public int? SshPort { get; set; }
    public string? SshUsername { get; set; }
    public string? SshAuthMethod { get; set; }
    public string? SshSecret { get; set; }
    public string? RemoteSocketPath { get; set; }

    // RBAC scope fields
    public string? OrganizationId { get; set; }
}

public class TestConnectionResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = null!;
    public string? DockerVersion { get; set; }
}

/// <summary>
/// POST /api/environments/test-connection - Test Docker connection.
/// Supports both direct Docker socket and SSH tunnel connections.
/// Accessible by: SystemAdmin, OrganizationOwner.
/// </summary>
[RequirePermission("Environments", "Create")]
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
        PreProcessor<RbacPreProcessor<TestConnectionRequest>>();
    }

    public override async Task HandleAsync(TestConnectionRequest req, CancellationToken ct)
    {
        if (req.Type == "SshTunnel")
        {
            if (string.IsNullOrWhiteSpace(req.SshHost) ||
                string.IsNullOrWhiteSpace(req.SshUsername) ||
                string.IsNullOrWhiteSpace(req.SshSecret))
            {
                Response = new TestConnectionResponse
                {
                    Success = false,
                    Message = "SSH host, username, and credential are required"
                };
                return;
            }

            var sshResult = await _mediator.Send(new TestSshConnectionCommand(
                req.SshHost,
                req.SshPort ?? 22,
                req.SshUsername,
                req.SshAuthMethod ?? "PrivateKey",
                req.SshSecret,
                req.RemoteSocketPath ?? "/var/run/docker.sock"), ct);

            Response = new TestConnectionResponse
            {
                Success = sshResult.Success,
                Message = sshResult.Message,
                DockerVersion = sshResult.DockerVersion
            };
        }
        else
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
}
