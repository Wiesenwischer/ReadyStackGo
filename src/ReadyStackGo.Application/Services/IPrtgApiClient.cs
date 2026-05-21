namespace ReadyStackGo.Application.Services;

/// <summary>
/// Thin wrapper around the PRTG HTTP API for the lifecycle handlers behind
/// Variant 3. The PRTG API is a URL/query-string-based "API" — every call is
/// a GET or POST against an .htm endpoint with parameters in the URL.
/// </summary>
public interface IPrtgApiClient
{
    /// <summary>
    /// Duplicates a template device and returns the new device's PRTG object-id.
    /// The new device starts paused; call <see cref="ResumeAsync"/> to start polling.
    /// </summary>
    Task<int?> DuplicateDeviceAsync(
        PrtgConnectionInfo connection,
        int templateDeviceId,
        string newName,
        string host,
        CancellationToken cancellationToken);

    /// <summary>Sets a property on a PRTG object (e.g. <c>host</c>, <c>name</c>).</summary>
    Task<bool> SetObjectPropertyAsync(
        PrtgConnectionInfo connection,
        int objectId,
        string propertyName,
        string value,
        CancellationToken cancellationToken);

    /// <summary>Resumes (unpauses) a PRTG object so it starts polling.</summary>
    Task<bool> ResumeAsync(PrtgConnectionInfo connection, int objectId, CancellationToken cancellationToken);

    /// <summary>Deletes a PRTG object. Returns true if deleted or if the object did not exist.</summary>
    Task<bool> DeleteObjectAsync(PrtgConnectionInfo connection, int objectId, CancellationToken cancellationToken);

    /// <summary>Lightweight liveness probe — returns true when the URL responds and the API token authenticates.</summary>
    Task<bool> PingAsync(PrtgConnectionInfo connection, CancellationToken cancellationToken);
}

/// <summary>
/// Per-call connection info. Decrypted by the caller (lifecycle handler) so the
/// HTTP client itself does not depend on the encryption service.
/// </summary>
public sealed record PrtgConnectionInfo(string BaseUrl, string ApiToken, bool VerifyTls);
