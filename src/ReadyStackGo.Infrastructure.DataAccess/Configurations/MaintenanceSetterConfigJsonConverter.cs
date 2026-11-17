namespace ReadyStackGo.Infrastructure.DataAccess.Configurations;

using System.Text.Json;
using System.Text.Json.Serialization;
using ReadyStackGo.Domain.Deployment.Observers;

/// <summary>
/// JSON converter for MaintenanceSetterConfig that handles polymorphic ISetterSettings.
/// Mirror of <see cref="MaintenanceObserverConfigJsonConverter"/>.
/// </summary>
public class MaintenanceSetterConfigJsonConverter : JsonConverter<MaintenanceSetterConfig>
{
    public override MaintenanceSetterConfig? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        var typeValue = root.GetProperty("type").GetString();
        if (string.IsNullOrEmpty(typeValue) || !Enum.TryParse<SetterType>(typeValue, ignoreCase: true, out var setterType))
            return null;

        var gracePeriod = TimeSpan.FromTicks(
            root.TryGetProperty("gracePeriodTicks", out var gp) ? gp.GetInt64() : 0);
        var maintenanceValue = root.TryGetProperty("maintenanceValue", out var mv) ? mv.GetString() ?? "" : "";
        var normalValue = root.TryGetProperty("normalValue", out var nv) ? nv.GetString() ?? "" : "";

        var settingsElement = root.GetProperty("settings");
        var settingsType = settingsElement.GetProperty("settingsType").GetString();

        ISetterSettings settings = settingsType switch
        {
            "sql" => DeserializeSqlSettings(settingsElement),
            "webhook" => DeserializeWebhookSettings(settingsElement),
            _ => throw new JsonException($"Unknown setter settings type: {settingsType}")
        };

        return MaintenanceSetterConfig.Create(setterType, gracePeriod, maintenanceValue, normalValue, settings);
    }

    public override void Write(Utf8JsonWriter writer, MaintenanceSetterConfig value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WriteString("type", value.Type.ToString());
        writer.WriteNumber("gracePeriodTicks", value.GracePeriod.Ticks);
        writer.WriteString("maintenanceValue", value.MaintenanceValue);
        writer.WriteString("normalValue", value.NormalValue);

        writer.WriteStartObject("settings");
        switch (value.Settings)
        {
            case SqlSetterSettings sql:
                writer.WriteString("settingsType", "sql");
                writer.WriteString("connectionString", sql.ConnectionString);
                writer.WriteString("propertyName", sql.PropertyName);
                break;

            case WebhookSetterSettings webhook:
                writer.WriteString("settingsType", "webhook");
                writer.WriteString("url", webhook.Url);
                if (webhook.Secret != null) writer.WriteString("secret", webhook.Secret);
                writer.WriteNumber("timeoutTicks", webhook.Timeout.Ticks);
                writer.WriteNumber("maxRetries", webhook.MaxRetries);
                break;
        }
        writer.WriteEndObject();

        writer.WriteEndObject();
    }

    private static SqlSetterSettings DeserializeSqlSettings(JsonElement element)
    {
        var connectionString = element.TryGetProperty("connectionString", out var cs) ? cs.GetString() ?? "" : "";
        var propertyName = element.TryGetProperty("propertyName", out var pn) ? pn.GetString() ?? "" : "";
        return new SqlSetterSettings(connectionString, propertyName);
    }

    private static WebhookSetterSettings DeserializeWebhookSettings(JsonElement element)
    {
        var url = element.GetProperty("url").GetString() ?? throw new JsonException("URL is required");
        var secret = element.TryGetProperty("secret", out var s) ? s.GetString() : null;
        var timeoutTicks = element.TryGetProperty("timeoutTicks", out var t) ? t.GetInt64() : TimeSpan.FromSeconds(10).Ticks;
        var maxRetries = element.TryGetProperty("maxRetries", out var r) ? r.GetInt32() : 2;
        return new WebhookSetterSettings(url, secret, TimeSpan.FromTicks(timeoutTicks), maxRetries);
    }
}
