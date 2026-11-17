using System.Security.Cryptography;
using System.Text;

namespace ReadyStackGo.Infrastructure.Services.Health;

/// <summary>
/// Computes the HMAC-SHA256 signature for maintenance webhook bodies.
/// Format: "sha256=" + lowercase hex digest of HMAC-SHA256(secret, body).
/// </summary>
public static class WebhookSignature
{
    public const string HeaderName = "X-RSGO-Signature";

    public static string Compute(string secret, string body)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var bodyBytes = Encoding.UTF8.GetBytes(body);

        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(bodyBytes);

        return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
    }
}
