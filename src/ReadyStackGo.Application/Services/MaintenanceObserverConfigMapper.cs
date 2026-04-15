using ReadyStackGo.Domain.Deployment.Observers;
using ReadyStackGo.Domain.StackManagement.Manifests;

namespace ReadyStackGo.Application.Services;

/// <summary>
/// Maps a RsgoMaintenanceObserver (StackManagement domain) to a MaintenanceObserverConfig
/// (Deployment domain), resolving ${VAR} placeholders against a variable dictionary.
/// </summary>
public static class MaintenanceObserverConfigMapper
{
    public static MaintenanceObserverConfig? Map(
        RsgoMaintenanceObserver? source,
        IReadOnlyDictionary<string, string> variables)
    {
        if (source == null)
            return null;

        if (!ObserverType.TryFromValue(source.Type, out var observerType) || observerType == null)
            return null;

        var pollingInterval = ParseTimeSpan(source.PollingInterval) ?? TimeSpan.FromSeconds(30);

        IObserverSettings settings;

        if (observerType == ObserverType.SqlExtendedProperty)
        {
            var connectionString = ResolveConnectionString(source, variables);
            if (string.IsNullOrEmpty(connectionString))
                return null;

            settings = SqlObserverSettings.ForExtendedProperty(
                source.PropertyName ?? throw new InvalidOperationException("PropertyName required"),
                connectionString);
        }
        else if (observerType == ObserverType.SqlQuery)
        {
            var connectionString = ResolveConnectionString(source, variables);
            if (string.IsNullOrEmpty(connectionString))
                return null;

            settings = SqlObserverSettings.ForQuery(
                source.Query ?? throw new InvalidOperationException("Query required"),
                connectionString);
        }
        else if (observerType == ObserverType.Http)
        {
            var timeout = ParseTimeSpan(source.Timeout) ?? TimeSpan.FromSeconds(10);
            var url = ResolveVariables(
                source.Url ?? throw new InvalidOperationException("URL required"), variables);
            if (string.IsNullOrEmpty(url))
                return null;

            settings = HttpObserverSettings.Create(
                url,
                source.Method ?? "GET",
                null,
                timeout,
                source.JsonPath);
        }
        else if (observerType == ObserverType.File)
        {
            var mode = source.Mode?.ToLowerInvariant() == "content"
                ? FileCheckMode.Content
                : FileCheckMode.Exists;

            var path = ResolveVariables(
                source.Path ?? throw new InvalidOperationException("Path required"), variables);
            if (string.IsNullOrEmpty(path))
                return null;

            settings = mode == FileCheckMode.Content
                ? FileObserverSettings.ForContent(path, source.ContentPattern)
                : FileObserverSettings.ForExistence(path);
        }
        else
        {
            return null;
        }

        return MaintenanceObserverConfig.Create(
            observerType,
            pollingInterval,
            source.MaintenanceValue,
            source.NormalValue,
            settings);
    }

    private static string? ResolveConnectionString(
        RsgoMaintenanceObserver source,
        IReadOnlyDictionary<string, string> variables)
    {
        if (!string.IsNullOrEmpty(source.ConnectionString))
            return ResolveVariables(source.ConnectionString, variables);

        if (!string.IsNullOrEmpty(source.ConnectionName) &&
            variables.TryGetValue(source.ConnectionName, out var connectionString))
        {
            return connectionString;
        }

        return null;
    }

    private static string? ResolveVariables(string template, IReadOnlyDictionary<string, string> variables)
    {
        if (string.IsNullOrEmpty(template))
            return template;

        var result = template;
        foreach (var kvp in variables)
        {
            result = result.Replace($"${{{kvp.Key}}}", kvp.Value);
        }

        if (result.Contains("${"))
            return null;

        return result;
    }

    private static TimeSpan? ParseTimeSpan(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        value = value.Trim().ToLowerInvariant();

        if (value.EndsWith('s') && int.TryParse(value[..^1], out var seconds))
            return TimeSpan.FromSeconds(seconds);

        if (value.EndsWith('m') && int.TryParse(value[..^1], out var minutes))
            return TimeSpan.FromMinutes(minutes);

        if (value.EndsWith('h') && int.TryParse(value[..^1], out var hours))
            return TimeSpan.FromHours(hours);

        return null;
    }
}
