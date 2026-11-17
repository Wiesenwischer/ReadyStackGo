using ReadyStackGo.Domain.Deployment.Observers;
using ReadyStackGo.Domain.StackManagement.Manifests;

namespace ReadyStackGo.Application.Services;

/// <summary>
/// Maps a <see cref="RsgoMaintenanceSetter"/> (manifest) to a <see cref="MaintenanceSetterConfig"/>
/// (deployment domain), resolving ${VAR} placeholders against a variable dictionary.
/// Mirror of <see cref="MaintenanceObserverConfigMapper"/>.
/// </summary>
public static class MaintenanceSetterConfigMapper
{
    public static MaintenanceSetterConfig? Map(
        RsgoMaintenanceSetter? source,
        IReadOnlyDictionary<string, string> variables)
    {
        if (source == null)
            return null;

        var type = source.Type?.Trim().ToLowerInvariant();
        var gracePeriod = ParseTimeSpan(source.GracePeriod) ?? TimeSpan.Zero;

        if (type == "sqlextendedproperty")
        {
            var connectionString = ResolveConnectionString(source, variables);
            if (string.IsNullOrEmpty(connectionString) || string.IsNullOrWhiteSpace(source.PropertyName))
                return null;

            var settings = new SqlSetterSettings(connectionString, source.PropertyName);

            return MaintenanceSetterConfig.Create(
                SetterType.SqlExtendedProperty,
                gracePeriod,
                source.MaintenanceValue ?? "1",
                source.NormalValue ?? "0",
                settings);
        }

        if (type == "webhook")
        {
            var url = ResolveVariables(source.Url ?? string.Empty, variables);
            if (string.IsNullOrEmpty(url))
                return null;

            var secret = string.IsNullOrEmpty(source.Secret) ? null : ResolveVariables(source.Secret, variables);
            var timeout = ParseTimeSpan(source.Timeout) ?? TimeSpan.FromSeconds(10);

            var settings = new WebhookSetterSettings(url, secret, timeout, source.Retries ?? 2);

            return MaintenanceSetterConfig.Create(
                SetterType.Webhook,
                gracePeriod,
                source.MaintenanceValue ?? "maintenance",
                source.NormalValue ?? "normal",
                settings);
        }

        return null;
    }

    private static string? ResolveConnectionString(
        RsgoMaintenanceSetter source,
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

        return result.Contains("${") ? null : result;
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
