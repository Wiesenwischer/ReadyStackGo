namespace ReadyStackGo.Application.UseCases.System.GetTlsConfig;

public record GetTlsConfigResponse
{
    public required string Mode { get; init; }
    public CertificateInfoDto? CertificateInfo { get; init; }
    public bool HttpEnabled { get; init; }
}

public record CertificateInfoDto
{
    public required string Subject { get; init; }
    public required string Issuer { get; init; }
    public required DateTime ExpiresAt { get; init; }
    public required string Thumbprint { get; init; }
    public bool IsSelfSigned { get; init; }
    public bool IsExpired { get; init; }
    public bool IsExpiringSoon { get; init; }
}
