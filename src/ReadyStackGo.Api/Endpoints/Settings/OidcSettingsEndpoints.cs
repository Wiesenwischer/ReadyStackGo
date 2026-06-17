using FastEndpoints;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.Services.Oidc;

namespace ReadyStackGo.API.Endpoints.Settings;

/// <summary>
/// OIDC provider DTO. The client secret is write-only: never returned on read (only
/// <see cref="HasClientSecret"/>), and an empty value on write keeps the stored secret.
/// </summary>
public class OidcProviderSettingsDto
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Authority { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string? ClientSecret { get; set; }
    public bool HasClientSecret { get; set; }
    public string Scopes { get; set; } = "openid email profile";
    public bool Enabled { get; set; }
}

public class OidcSettingsDto
{
    public List<OidcProviderSettingsDto> Providers { get; set; } = new();
}

/// <summary>GET /api/settings/oidc — read configured OIDC providers (secrets never returned).</summary>
[RequireSystemAdmin]
public class GetOidcSettingsEndpoint : EndpointWithoutRequest<OidcSettingsDto>
{
    private readonly IOidcSettingsService _settings;

    public GetOidcSettingsEndpoint(IOidcSettingsService settings)
    {
        _settings = settings;
    }

    public override void Configure()
    {
        Get("/api/settings/oidc");
        Description(b => b.WithTags("Settings"));
        PreProcessor<RbacPreProcessor<EmptyRequest>>();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var providers = await _settings.GetAllAsync(ct);
        Response = new OidcSettingsDto
        {
            Providers = providers.Select(p => new OidcProviderSettingsDto
            {
                Name = p.Name,
                DisplayName = p.DisplayName,
                Authority = p.Authority,
                ClientId = p.ClientId,
                ClientSecret = null,
                HasClientSecret = !string.IsNullOrEmpty(p.ClientSecret),
                Scopes = p.Scopes,
                Enabled = p.Enabled
            }).ToList()
        };
    }
}

/// <summary>PUT /api/settings/oidc — replace the OIDC provider configuration.</summary>
[RequireSystemAdmin]
public class SaveOidcSettingsEndpoint : Endpoint<OidcSettingsDto>
{
    private readonly IOidcSettingsService _settings;

    public SaveOidcSettingsEndpoint(IOidcSettingsService settings)
    {
        _settings = settings;
    }

    public override void Configure()
    {
        Put("/api/settings/oidc");
        Description(b => b.WithTags("Settings"));
        PreProcessor<RbacPreProcessor<OidcSettingsDto>>();
    }

    public override async Task HandleAsync(OidcSettingsDto req, CancellationToken ct)
    {
        await _settings.SaveAllAsync(req.Providers.Select(p => new OidcProviderSettings
        {
            Name = p.Name,
            DisplayName = p.DisplayName,
            Authority = p.Authority,
            ClientId = p.ClientId,
            ClientSecret = p.ClientSecret,
            Scopes = p.Scopes,
            Enabled = p.Enabled
        }), ct);

        await Send.NoContentAsync(ct);
    }
}
