namespace ReadyStackGo.Infrastructure.Services.IdentityProviders;

/// <summary>
/// Operator settings of the identity provider template catalog (section
/// "IdentityProviderTemplates", e.g. IdentityProviderTemplates__Path).
/// </summary>
public class IdentityProviderTemplateOptions
{
    public const string SectionName = "IdentityProviderTemplates";

    /// <summary>
    /// Directory with additional templates (one folder per template). May be missing.
    /// A template here replaces a built-in template with the same id.
    /// </summary>
    public string? Path { get; set; }

    /// <summary>Comma-separated list of offered template ids; empty offers all.</summary>
    public string? Enabled { get; set; }
}
