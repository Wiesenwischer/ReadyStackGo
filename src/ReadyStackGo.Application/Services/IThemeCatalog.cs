using System.Text.RegularExpressions;

namespace ReadyStackGo.Application.Services;

/// <summary>
/// Catalog of runtime-loadable theme packages (folder with theme.json and theme.css).
/// Packages come from the built-in directory (wwwroot/themes) and an optional operator directory.
/// See docs/Architecture/Themes.md.
/// </summary>
public interface IThemeCatalog
{
    /// <summary>
    /// Returns the themes offered by this installation (sorted by order, then id) and the default theme id.
    /// The default id is null only when no theme is offered.
    /// </summary>
    ThemeCatalogSnapshot GetThemes();

    /// <summary>
    /// Returns the CSS content of an offered theme, or null if the id is invalid, unknown or not offered.
    /// </summary>
    string? GetThemeCss(string? id);
}

/// <summary>
/// Metadata of a theme package as read from its theme.json.
/// </summary>
public sealed record ThemeInfo(string Id, string Name, string Description, int Order);

/// <summary>
/// The offered themes and the default theme id at the time of the call.
/// </summary>
public sealed record ThemeCatalogSnapshot(string? DefaultId, IReadOnlyList<ThemeInfo> Themes);

/// <summary>
/// Validation rules for theme ids, shared by the catalog and the API.
/// </summary>
public static partial class ThemeId
{
    /// <summary>
    /// Lowercase letters, digits and hyphens, 1 to 40 characters, not starting with a hyphen.
    /// \z instead of $ so that a trailing newline is not accepted.
    /// </summary>
    [GeneratedRegex(@"^[a-z0-9][a-z0-9-]{0,39}\z", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();

    public static bool IsValid(string? id) => id is not null && Pattern().IsMatch(id);
}
