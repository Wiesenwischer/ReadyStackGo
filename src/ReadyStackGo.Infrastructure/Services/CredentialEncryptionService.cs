namespace ReadyStackGo.Infrastructure.Services;

using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;

/// <summary>
/// AES-256-CBC encryption service for sensitive credentials (SSH keys, passwords).
/// Master key is sourced from RSGO_ENCRYPTION_KEY environment variable or auto-generated.
/// </summary>
public class CredentialEncryptionService : ICredentialEncryptionService
{
    private readonly byte[] _masterKey;
    private readonly ILogger<CredentialEncryptionService> _logger;

    public CredentialEncryptionService(IConfiguration configuration, ILogger<CredentialEncryptionService> logger)
    {
        _logger = logger;
        var keyString = configuration["RSGO_ENCRYPTION_KEY"]
            ?? Environment.GetEnvironmentVariable("RSGO_ENCRYPTION_KEY");

        if (!string.IsNullOrEmpty(keyString))
        {
            _masterKey = DeriveKey(keyString);
            _logger.LogInformation("Credential encryption initialized with provided master key");
        }
        else
        {
            // Auto-generate a key and persist it in a file next to the database
            var dataDir = configuration["DataPath"] ?? "/app/data";
            var keyFilePath = Path.Combine(dataDir, ".encryption-key");

            if (File.Exists(keyFilePath))
            {
                var storedKey = File.ReadAllText(keyFilePath).Trim();
                _masterKey = DeriveKey(storedKey);
                _logger.LogInformation("Credential encryption initialized from stored key file");
            }
            else
            {
                var generatedKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
                try
                {
                    Directory.CreateDirectory(dataDir);
                    File.WriteAllText(keyFilePath, generatedKey);
                    _logger.LogWarning("Auto-generated encryption key stored at {Path}. Set RSGO_ENCRYPTION_KEY for production use", keyFilePath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not persist auto-generated encryption key. Credentials will not survive container restart without RSGO_ENCRYPTION_KEY");
                }
                _masterKey = DeriveKey(generatedKey);
            }
        }
    }

    public string Encrypt(string plaintext)
    {
        ArgumentException.ThrowIfNullOrEmpty(plaintext);

        using var aes = Aes.Create();
        aes.Key = _masterKey;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertextBytes = encryptor.TransformFinalBlock(plaintextBytes, 0, plaintextBytes.Length);

        // Prepend IV to ciphertext for storage
        var result = new byte[aes.IV.Length + ciphertextBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(ciphertextBytes, 0, result, aes.IV.Length, ciphertextBytes.Length);

        return Convert.ToBase64String(result);
    }

    public string Decrypt(string ciphertext)
    {
        ArgumentException.ThrowIfNullOrEmpty(ciphertext);

        var fullBytes = Convert.FromBase64String(ciphertext);

        using var aes = Aes.Create();
        aes.Key = _masterKey;

        // Extract IV from first 16 bytes
        var iv = new byte[aes.BlockSize / 8];
        var encrypted = new byte[fullBytes.Length - iv.Length];
        Buffer.BlockCopy(fullBytes, 0, iv, 0, iv.Length);
        Buffer.BlockCopy(fullBytes, iv.Length, encrypted, 0, encrypted.Length);

        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        var plaintextBytes = decryptor.TransformFinalBlock(encrypted, 0, encrypted.Length);

        return Encoding.UTF8.GetString(plaintextBytes);
    }

    private static byte[] DeriveKey(string passphrase)
    {
        // Use SHA-256 to derive a consistent 32-byte key from any passphrase
        return SHA256.HashData(Encoding.UTF8.GetBytes(passphrase));
    }
}
