namespace ReadyStackGo.Infrastructure.DataAccess.Configurations;

using System.Text.Json;
using System.Text.Json.Serialization;
using ReadyStackGo.Domain.Deployment.Observers;

/// <summary>
/// JSON converter for MaintenanceObserverConfig that handles polymorphic IObserverSettings.
/// </summary>
public class MaintenanceObserverConfigJsonConverter : JsonConverter<MaintenanceObserverConfig>
{
    public override MaintenanceObserverConfig? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        // Read basic properties
        var typeValue = root.GetProperty("type").GetString();
        if (string.IsNullOrEmpty(typeValue) || !ObserverType.TryFromValue(typeValue, out var observerType) || observerType == null)
            return null;

        var pollingIntervalTicks = root.GetProperty("pollingIntervalTicks").GetInt64();
        var pollingInterval = TimeSpan.FromTicks(pollingIntervalTicks);

        var maintenanceValue = root.GetProperty("maintenanceValue").GetString() ?? "true";
        var normalValue = root.TryGetProperty("normalValue", out var nv) ? nv.GetString() : null;

        // Read type-specific settings
        var settingsElement = root.GetProperty("settings");
        var settingsType = settingsElement.GetProperty("settingsType").GetString();

        IObserverSettings settings = settingsType switch
        {
            "sql" => DeserializeSqlSettings(settingsElement),
            "http" => DeserializeHttpSettings(settingsElement),
            "file" => DeserializeFileSettings(settingsElement),
            _ => throw new JsonException($"Unknown settings type: {settingsType}")
        };

        return MaintenanceObserverConfig.Create(observerType, pollingInterval, maintenanceValue, normalValue, settings);
    }

    public override void Write(Utf8JsonWriter writer, MaintenanceObserverConfig value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WriteString("type", value.Type.Value);
        writer.WriteNumber("pollingIntervalTicks", value.PollingInterval.Ticks);
        writer.WriteString("maintenanceValue", value.MaintenanceValue);
        if (value.NormalValue != null)
            writer.WriteString("normalValue", value.NormalValue);

        // Write settings with type discriminator
        writer.WriteStartObject("settings");
        switch (value.Settings)
        {
            case SqlObserverSettings sql:
                writer.WriteString("settingsType", "sql");
                if (sql.ConnectionString != null) writer.WriteString("connectionString", sql.ConnectionString);
                if (sql.ConnectionName != null) writer.WriteString("connectionName", sql.ConnectionName);
                if (sql.PropertyName != null) writer.WriteString("propertyName", sql.PropertyName);
                if (sql.Query != null) writer.WriteString("query", sql.Query);
                break;

            case HttpObserverSettings http:
                writer.WriteString("settingsType", "http");
                writer.WriteString("url", http.Url);
                writer.WriteString("method", http.Method);
                writer.WriteNumber("timeoutTicks", http.Timeout.Ticks);
                if (http.JsonPath != null) writer.WriteString("jsonPath", http.JsonPath);
                if (http.Headers != null && http.Headers.Count > 0)
                {
                    writer.WriteStartObject("headers");
                    foreach (var header in http.Headers)
                    {
                        writer.WriteString(header.Key, header.Value);
                    }
                    writer.WriteEndObject();
                }
                break;

            case FileObserverSettings file:
                writer.WriteString("settingsType", "file");
                writer.WriteString("path", file.Path);
                writer.WriteString("mode", file.Mode.ToString().ToLowerInvariant());
                if (file.ContentPattern != null) writer.WriteString("contentPattern", file.ContentPattern);
                break;
        }
        writer.WriteEndObject();

        writer.WriteEndObject();
    }

    private static SqlObserverSettings DeserializeSqlSettings(JsonElement element)
    {
        var connectionString = element.TryGetProperty("connectionString", out var cs) ? cs.GetString() : null;
        var connectionName = element.TryGetProperty("connectionName", out var cn) ? cn.GetString() : null;
        var propertyName = element.TryGetProperty("propertyName", out var pn) ? pn.GetString() : null;
        var query = element.TryGetProperty("query", out var q) ? q.GetString() : null;

        if (!string.IsNullOrEmpty(propertyName))
            return SqlObserverSettings.ForExtendedProperty(propertyName, connectionString, connectionName);
        if (!string.IsNullOrEmpty(query))
            return SqlObserverSettings.ForQuery(query, connectionString, connectionName);

        throw new JsonException("SQL settings must have either propertyName or query");
    }

    private static HttpObserverSettings DeserializeHttpSettings(JsonElement element)
    {
        var url = element.GetProperty("url").GetString() ?? throw new JsonException("URL is required");
        var method = element.TryGetProperty("method", out var m) ? m.GetString() ?? "GET" : "GET";
        var timeoutTicks = element.TryGetProperty("timeoutTicks", out var t) ? t.GetInt64() : TimeSpan.FromSeconds(10).Ticks;
        var jsonPath = element.TryGetProperty("jsonPath", out var jp) ? jp.GetString() : null;

        Dictionary<string, string>? headers = null;
        if (element.TryGetProperty("headers", out var headersElement))
        {
            headers = new Dictionary<string, string>();
            foreach (var header in headersElement.EnumerateObject())
            {
                headers[header.Name] = header.Value.GetString() ?? "";
            }
        }

        return HttpObserverSettings.Create(url, method, headers, TimeSpan.FromTicks(timeoutTicks), jsonPath);
    }

    private static FileObserverSettings DeserializeFileSettings(JsonElement element)
    {
        var path = element.GetProperty("path").GetString() ?? throw new JsonException("Path is required");
        var modeStr = element.TryGetProperty("mode", out var m) ? m.GetString() : "exists";
        var contentPattern = element.TryGetProperty("contentPattern", out var cp) ? cp.GetString() : null;

        var mode = modeStr?.ToLowerInvariant() == "content" ? FileCheckMode.Content : FileCheckMode.Exists;

        return mode == FileCheckMode.Content
            ? FileObserverSettings.ForContent(path, contentPattern)
            : FileObserverSettings.ForExistence(path);
    }
}
