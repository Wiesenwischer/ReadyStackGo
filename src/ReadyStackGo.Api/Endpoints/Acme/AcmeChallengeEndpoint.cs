using FastEndpoints;
using ReadyStackGo.Infrastructure.LetsEncrypt;

namespace ReadyStackGo.Api.Endpoints.Acme;

/// <summary>
/// GET /.well-known/acme-challenge/{token}
/// ACME HTTP-01 challenge endpoint for Let's Encrypt domain validation.
/// Must be publicly accessible without authentication.
/// </summary>
public class AcmeChallengeEndpoint : EndpointWithoutRequest
{
    private readonly IPendingChallengeStore _challengeStore;
    private readonly ILogger<AcmeChallengeEndpoint> _logger;

    public AcmeChallengeEndpoint(
        IPendingChallengeStore challengeStore,
        ILogger<AcmeChallengeEndpoint> logger)
    {
        _challengeStore = challengeStore;
        _logger = logger;
    }

    public override void Configure()
    {
        Get("/.well-known/acme-challenge/{token}");
        AllowAnonymous();
        Description(b => b
            .WithTags("ACME")
            .WithSummary("ACME HTTP-01 challenge endpoint")
            .WithDescription("Returns the key authorization for Let's Encrypt HTTP-01 domain validation. This endpoint must be publicly accessible."));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var token = Route<string>("token");

        if (string.IsNullOrEmpty(token))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var keyAuthorization = await _challengeStore.GetHttpChallengeAsync(token);

        if (keyAuthorization == null)
        {
            _logger.LogDebug("ACME challenge token not found: {Token}", token);
            HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        _logger.LogInformation("Serving ACME HTTP-01 challenge for token: {Token}", token);

        HttpContext.Response.ContentType = "text/plain";
        await HttpContext.Response.WriteAsync(keyAuthorization, ct);
    }
}
