using ReadyStackGo.Application.Services.Oidc;

namespace ReadyStackGo.Application.Services.IdentityProviders;

/// <summary>
/// Catalog of identity provider templates (folder with template.json and an optional
/// icon.svg). Templates come from the image (built-in, embedded) and an optional operator
/// directory; a template in the directory replaces a built-in one with the same id.
/// See docs/Architecture/Identity-Provider-Templates.md.
/// </summary>
public interface IIdentityProviderTemplateCatalog
{
    /// <summary>The offered templates, sorted by order, then id.</summary>
    IReadOnlyList<IdentityProviderTemplate> GetTemplates();

    /// <summary>An offered template by id, or null if the id is invalid, unknown or not offered.</summary>
    IdentityProviderTemplate? GetTemplate(string? id);

    /// <summary>The SVG icon of an offered template, or null if there is none.</summary>
    byte[]? GetIcon(string? id);
}

/// <summary>A provider template as read from its template.json, with defaults applied.</summary>
public sealed record IdentityProviderTemplate
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string Description { get; init; } = string.Empty;

    /// <summary>Text of the tile in the setup wizard; defaults to <see cref="Description"/>.</summary>
    public string SetupDescription { get; init; } = string.Empty;

    public int Order { get; init; } = int.MaxValue;

    /// <summary>Suggested provider name (the id in the routes).</summary>
    public required string ProviderName { get; init; }

    /// <summary>Suggested display name; null lets the user choose (Generic OIDC).</summary>
    public string? ProviderDisplayName { get; init; }

    /// <summary>Fixed authority, or null if the user enters it (see <see cref="AuthorityInput"/>).</summary>
    public string? AuthorityUrl { get; init; }

    public TemplateAuthorityInput? AuthorityInput { get; init; }

    public required string RegistrationKind { get; init; }

    public bool RequirePar { get; init; }

    /// <summary>The address of the installation must be https (or http on loopback).</summary>
    public bool RequireHttps { get; init; }

    public string Scopes { get; init; } = OidcProviderSettings.DefaultScopes;

    public OidcClaimNames Claims { get; init; } = new();

    public string? HelpUrl { get; init; }

    /// <summary>Offered as a tile in the first step of the setup wizard.</summary>
    public bool OfferInSetup { get; init; }

    public bool HasIcon { get; init; }

    public bool HasFixedAuthority => AuthorityUrl != null;
}

/// <summary>Hint and example for an authority the user enters.</summary>
public sealed record TemplateAuthorityInput(string? Hint, string? Example);
