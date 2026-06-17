namespace ReadyStackGo.Application.Services.Oidc;

/// <summary>OIDC provider settings as used at runtime (client secret decrypted).</summary>
public class OidcProviderSettings
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Authority { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string? ClientSecret { get; set; }
    public string Scopes { get; set; } = "openid email profile";
    public bool Enabled { get; set; }
}

/// <summary>The identity claims extracted from a validated OIDC id token.</summary>
public record OidcUserInfo(string Subject, string? Email, bool EmailVerified);
