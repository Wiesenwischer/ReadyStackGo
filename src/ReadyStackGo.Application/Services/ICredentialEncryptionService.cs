namespace ReadyStackGo.Application.Services;

/// <summary>
/// Service for AES-encrypting and decrypting secrets (SSH keys, passwords).
/// Encryption is reversible because the plaintext is needed at runtime (e.g., for SSH tunnel creation).
/// </summary>
public interface ICredentialEncryptionService
{
    /// <summary>
    /// Encrypts a plaintext secret using AES.
    /// </summary>
    string Encrypt(string plaintext);

    /// <summary>
    /// Decrypts an AES-encrypted secret back to plaintext.
    /// </summary>
    string Decrypt(string ciphertext);
}
