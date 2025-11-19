namespace ReadyStackGo.Domain.Configuration;

/// <summary>
/// TLS configuration stored in rsgo.tls.json
/// </summary>
public class TlsConfig
{
    public TlsMode TlsMode { get; set; } = TlsMode.SelfSigned;
    public string CertificatePath { get; set; } = "/app/config/tls/selfsigned.pfx";
    public int Port { get; set; } = 5001;
    public bool HttpEnabled { get; set; } = true;
    public string? TerminatingContext { get; set; }
}

public enum TlsMode
{
    SelfSigned,
    Custom
}
