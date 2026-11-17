namespace ReadyStackGo.Application.Services.Edge;

/// <summary>
/// Thin client for the Caddy admin API. Used to atomically, connection-preservingly
/// reload an edge's configuration (locked decision §2: <c>POST /load</c>).
/// </summary>
public interface ICaddyAdminClient
{
    /// <summary>
    /// Pushes a full Caddy JSON config to <c>{adminBaseUrl}/load</c>.
    /// </summary>
    /// <returns>True on success (HTTP 2xx), false otherwise.</returns>
    Task<bool> LoadConfigAsync(string adminBaseUrl, string configJson, CancellationToken cancellationToken = default);
}
