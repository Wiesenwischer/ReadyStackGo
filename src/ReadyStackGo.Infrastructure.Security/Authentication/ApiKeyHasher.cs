using System.Security.Cryptography;
using System.Text;

namespace ReadyStackGo.Infrastructure.Security.Authentication;

/// <summary>
/// Utility for computing SHA-256 hashes of API keys.
/// </summary>
public static class ApiKeyHasher
{
    /// <summary>
    /// Computes a SHA-256 hash of the raw API key, returned as lowercase hex (64 chars).
    /// </summary>
    public static string ComputeSha256Hash(string rawKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawKey));
        return Convert.ToHexStringLower(bytes);
    }
}
