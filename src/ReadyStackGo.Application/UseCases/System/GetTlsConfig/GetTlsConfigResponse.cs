namespace ReadyStackGo.Application.UseCases.System.GetTlsConfig;

public record GetTlsConfigResponse
{
    public required string Mode { get; init; }
    public CertificateInfoDto? CertificateInfo { get; init; }
    public bool HttpEnabled { get; init; }
    public ReverseProxyDto? ReverseProxy { get; init; }
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

public record ReverseProxyDto
{
    public bool Enabled { get; init; }
    public string SslMode { get; init; } = "Termination";
    public bool TrustForwardedFor { get; init; }
    public bool TrustForwardedProto { get; init; }
    public bool TrustForwardedHost { get; init; }
    public List<string> KnownProxies { get; init; } = new();
    public int? ForwardLimit { get; init; }
    public string? PathBase { get; init; }
}
