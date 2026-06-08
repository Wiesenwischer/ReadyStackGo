namespace ReadyStackGo.Application.UseCases.PrtgConnections;

using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.Deployment.PrtgConnections;

/// <summary>
/// Asks the live PRTG instance for its group hierarchy. The Connection form
/// shows them as a dropdown so the admin can pick a <em>target group</em>
/// (where RSGO creates new devices on Link) without having to find the
/// numeric id in PRTG's UI.
///
/// Accepts either inline credentials (create-new flow) or an existing
/// connection id (edit flow, where the form may not re-enter the token).
/// </summary>
public sealed record ProbePrtgGroupsQuery(
    string Url,
    string ApiToken,
    Guid? ExistingConnectionId,
    bool VerifyTls) : IRequest<ProbePrtgGroupsResponse>;

public sealed record ProbePrtgGroupsResponse(
    bool Success,
    string? Error,
    IReadOnlyList<ProbedPrtgGroup> Groups);

public sealed record ProbedPrtgGroup(int ObjectId, string Name, string? Probe);

public sealed class ProbePrtgGroupsHandler : IRequestHandler<ProbePrtgGroupsQuery, ProbePrtgGroupsResponse>
{
    private readonly IPrtgApiClient _client;
    private readonly IPrtgConnectionRepository _connections;
    private readonly ICredentialEncryptionService _encryption;
    private readonly ILogger<ProbePrtgGroupsHandler> _logger;

    public ProbePrtgGroupsHandler(
        IPrtgApiClient client,
        IPrtgConnectionRepository connections,
        ICredentialEncryptionService encryption,
        ILogger<ProbePrtgGroupsHandler> logger)
    {
        _client = client;
        _connections = connections;
        _encryption = encryption;
        _logger = logger;
    }

    public async Task<ProbePrtgGroupsResponse> Handle(ProbePrtgGroupsQuery query, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query.Url))
            return new ProbePrtgGroupsResponse(false, "PRTG URL is required.", Array.Empty<ProbedPrtgGroup>());

        string apiToken;
        if (!string.IsNullOrWhiteSpace(query.ApiToken))
        {
            apiToken = query.ApiToken;
        }
        else if (query.ExistingConnectionId is not null)
        {
            var existing = _connections.Get(new PrtgConnectionId(query.ExistingConnectionId.Value));
            if (existing is null)
                return new ProbePrtgGroupsResponse(false, "Connection not found.", Array.Empty<ProbedPrtgGroup>());
            apiToken = _encryption.Decrypt(existing.EncryptedApiToken);
        }
        else
        {
            return new ProbePrtgGroupsResponse(false, "API token is required.", Array.Empty<ProbedPrtgGroup>());
        }

        var info = new PrtgConnectionInfo(query.Url, apiToken, query.VerifyTls);

        try
        {
            var groups = await _client.ListGroupsAsync(info, ct);
            var mapped = groups
                .Select(g => new ProbedPrtgGroup(g.ObjectId, g.Name, g.Probe))
                .OrderBy(g => g.Probe)
                .ThenBy(g => g.Name)
                .ToList();
            _logger.LogInformation("Probed PRTG {Url} — found {Count} groups", query.Url, mapped.Count);
            return new ProbePrtgGroupsResponse(true, null, mapped);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PRTG group probe failed for {Url}", query.Url);
            return new ProbePrtgGroupsResponse(false,
                $"Could not reach PRTG: {ex.Message}", Array.Empty<ProbedPrtgGroup>());
        }
    }
}
