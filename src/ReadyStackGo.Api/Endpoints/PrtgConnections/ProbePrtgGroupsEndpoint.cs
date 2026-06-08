using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.PrtgConnections;

namespace ReadyStackGo.Api.Endpoints.PrtgConnections;

/// <summary>
/// POST /api/prtg-connections/probe-groups — calls the given PRTG instance and
/// returns its group list so the Connection form can show a dropdown instead
/// of asking the admin to type a numeric id. The credentials are transient
/// (not stored) so this works for both the create-new and edit-existing flows
/// the same way.
/// </summary>
[RequirePermission("Settings", "Manage")]
public class ProbePrtgGroupsEndpoint : Endpoint<ProbePrtgGroupsRequest, ProbePrtgGroupsResponse>
{
    private readonly IMediator _mediator;

    public ProbePrtgGroupsEndpoint(IMediator mediator) => _mediator = mediator;

    public override void Configure() => Post("/api/prtg-connections/probe-groups");

    public override async Task HandleAsync(ProbePrtgGroupsRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(new ProbePrtgGroupsQuery(
            req.Url ?? string.Empty,
            req.ApiToken ?? string.Empty,
            req.ExistingConnectionId,
            req.VerifyTls), ct);
        Response = result;
    }
}

public class ProbePrtgGroupsRequest
{
    /// <summary>PRTG base URL (e.g. https://prtg.example.local).</summary>
    public string? Url { get; set; }

    /// <summary>
    /// API token. When editing an existing connection the form may leave this
    /// empty to mean "reuse the stored token" — in that case set
    /// <see cref="ExistingConnectionId"/>.
    /// </summary>
    public string? ApiToken { get; set; }

    /// <summary>When provided, the stored encrypted token of that connection is used.</summary>
    public Guid? ExistingConnectionId { get; set; }

    public bool VerifyTls { get; set; } = true;
}
