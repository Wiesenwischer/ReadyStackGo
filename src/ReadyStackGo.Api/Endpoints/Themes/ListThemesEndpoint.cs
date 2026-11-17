using FastEndpoints;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Api.Endpoints.Themes;

/// <summary>
/// GET /api/themes - List the theme packages offered by this installation and the default theme.
/// Anonymous access (the login page is themed as well).
/// </summary>
public class ListThemesEndpoint : EndpointWithoutRequest<ThemeListResponse>
{
    private readonly IThemeCatalog _themeCatalog;
    private readonly ISystemConfigService _systemConfigService;

    public ListThemesEndpoint(IThemeCatalog themeCatalog, ISystemConfigService systemConfigService)
    {
        _themeCatalog = themeCatalog;
        _systemConfigService = systemConfigService;
    }

    public override void Configure()
    {
        Get("/api/themes");
        AllowAnonymous();
        Description(b => b
            .WithTags("Themes")
            .WithSummary("List offered theme packages")
            .WithDescription("Returns the offered theme packages and the default theme id of this installation."));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var installationDefault = await _systemConfigService.GetDefaultThemeAsync();
        var snapshot = _themeCatalog.GetThemes(installationDefault);

        Response = new ThemeListResponse
        {
            Default = snapshot.DefaultId,
            Themes = snapshot.Themes
                .Select(t => new ThemeDto
                {
                    Id = t.Id,
                    Name = t.Name,
                    Description = t.Description,
                    CssUrl = $"/api/themes/{t.Id}/theme.css"
                })
                .ToList()
        };
    }
}

public class ThemeListResponse
{
    /// <summary>
    /// Default theme id; null only when no theme is offered.
    /// </summary>
    public string? Default { get; init; }

    public IReadOnlyList<ThemeDto> Themes { get; init; } = [];
}

public class ThemeDto
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string CssUrl { get; init; }
}
