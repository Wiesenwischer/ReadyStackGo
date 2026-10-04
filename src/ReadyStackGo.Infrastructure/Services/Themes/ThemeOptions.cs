namespace ReadyStackGo.Infrastructure.Services.Themes;

/// <summary>
/// Configuration of the theme packages (section "Themes", environment variables Themes__*).
/// </summary>
public class ThemeOptions
{
    public const string SectionName = "Themes";

    /// <summary>
    /// Optional operator directory with additional theme packages. May not exist.
    /// A package with the same id replaces the built-in package.
    /// Default: /app/themes.
    /// </summary>
    public string? Path { get; set; } = "/app/themes";

    /// <summary>
    /// Directory with the built-in theme packages. When empty, the host sets it to
    /// WebRootPath/themes; without a host value the catalog falls back to
    /// &lt;AppContext.BaseDirectory&gt;/wwwroot/themes.
    /// </summary>
    public string? BuiltInPath { get; set; }

    /// <summary>
    /// Comma-separated list of offered theme ids. Empty = all packages found.
    /// </summary>
    public string? Enabled { get; set; } = "";

    /// <summary>
    /// Default theme id. If it is not offered, the first theme by order (then id) is the default.
    /// </summary>
    public string? Default { get; set; } = "turquoise";
}
