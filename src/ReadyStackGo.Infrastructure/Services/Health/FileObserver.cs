using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Domain.Deployment.Observers;

namespace ReadyStackGo.Infrastructure.Services.Health;

/// <summary>
/// Observer that monitors a file for maintenance state.
/// Supports checking file existence or reading file content.
/// </summary>
public sealed class FileObserver : BaseMaintenanceObserver
{
    private readonly FileObserverSettings _settings;

    public FileObserver(
        MaintenanceObserverConfig config,
        ILogger<FileObserver> logger)
        : base(config, logger)
    {
        _settings = config.Settings as FileObserverSettings
            ?? throw new ArgumentException("Invalid settings type for File observer");
    }

    public override ObserverType Type => ObserverType.File;

    protected override async Task<string> GetObservedValueAsync(CancellationToken cancellationToken)
    {
        return _settings.Mode switch
        {
            FileCheckMode.Exists => CheckFileExists(),
            FileCheckMode.Content => await ReadFileContentAsync(cancellationToken),
            _ => throw new InvalidOperationException($"Unknown file check mode: {_settings.Mode}")
        };
    }

    private string CheckFileExists()
    {
        var exists = File.Exists(_settings.Path);
        Logger.LogDebug("File existence check for '{Path}': {Exists}", _settings.Path, exists);
        return exists.ToString().ToLowerInvariant(); // "true" or "false"
    }

    private async Task<string> ReadFileContentAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_settings.Path))
        {
            Logger.LogDebug("File '{Path}' does not exist", _settings.Path);
            return Config.NormalValue ?? string.Empty;
        }

        var content = await File.ReadAllTextAsync(_settings.Path, cancellationToken);

        if (string.IsNullOrEmpty(_settings.ContentPattern))
        {
            // Return entire content (trimmed)
            return content.Trim();
        }

        // Extract value using regex pattern
        return ExtractWithPattern(content, _settings.ContentPattern);
    }

    private string ExtractWithPattern(string content, string pattern)
    {
        try
        {
            var regex = new Regex(pattern, RegexOptions.Multiline);
            var match = regex.Match(content);

            if (!match.Success)
            {
                Logger.LogDebug("Pattern '{Pattern}' did not match file content", pattern);
                return Config.NormalValue ?? string.Empty;
            }

            // If there's a capture group, return the first group; otherwise return the whole match
            return match.Groups.Count > 1
                ? match.Groups[1].Value
                : match.Value;
        }
        catch (ArgumentException ex)
        {
            Logger.LogWarning(ex, "Invalid regex pattern: {Pattern}", pattern);
            throw new InvalidOperationException($"Invalid regex pattern: {ex.Message}");
        }
    }
}
