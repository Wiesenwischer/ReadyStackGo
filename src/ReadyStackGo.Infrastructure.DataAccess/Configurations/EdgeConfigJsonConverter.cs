namespace ReadyStackGo.Infrastructure.DataAccess.Configurations;

using System.Text.Json;
using System.Text.Json.Serialization;
using ReadyStackGo.Domain.Deployment.Edge;

/// <summary>
/// JSON converter for <see cref="EdgeConfig"/>. The value object has no parameterless
/// constructor and only get-only properties, so (mirroring the maintenance-observer
/// converter) it is (de)serialized explicitly through the <c>Create</c> factory.
/// </summary>
public class EdgeConfigJsonConverter : JsonConverter<EdgeConfig>
{
    public override EdgeConfig? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        var publicHostname = GetString(root, "publicHostname") ?? string.Empty;
        var publicPort = GetInt(root, "publicPort", 443);
        var upstreamService = GetString(root, "upstreamService") ?? string.Empty;
        var upstreamPort = GetInt(root, "upstreamPort", 8080);
        var network = GetString(root, "network") ?? string.Empty;
        var image = GetString(root, "image") ?? string.Empty;

        var tlsMode = ParseEnum(GetString(root, "tlsMode"), EdgeTlsMode.None);
        var tlsCertRef = GetString(root, "tlsCertRef");
        var letsEncryptEmail = GetString(root, "letsEncryptEmail");
        var letsEncryptDnsChallenge = GetString(root, "letsEncryptDnsChallenge");

        var pageMode = ParseEnum(GetString(root, "maintenancePageMode"), EdgeMaintenancePageMode.Default);
        var bundlePath = GetString(root, "bundlePath");
        var maintenanceContainerService = GetString(root, "maintenanceContainerService");

        EdgeBranding branding = EdgeBranding.Empty;
        if (root.TryGetProperty("branding", out var b) && b.ValueKind == JsonValueKind.Object)
        {
            var locales = new List<string>();
            if (b.TryGetProperty("locales", out var loc) && loc.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in loc.EnumerateArray())
                {
                    var v = item.GetString();
                    if (!string.IsNullOrEmpty(v)) locales.Add(v);
                }
            }

            branding = new EdgeBranding(
                GetString(b, "productName"),
                GetString(b, "logoUrl"),
                GetString(b, "supportContact"),
                locales);
        }

        return EdgeConfig.Create(
            publicHostname,
            publicPort,
            upstreamService,
            upstreamPort,
            network,
            image,
            tlsMode,
            tlsCertRef,
            letsEncryptEmail,
            letsEncryptDnsChallenge,
            pageMode,
            bundlePath,
            maintenanceContainerService,
            branding);
    }

    public override void Write(Utf8JsonWriter writer, EdgeConfig value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WriteString("publicHostname", value.PublicHostname);
        writer.WriteNumber("publicPort", value.PublicPort);
        writer.WriteString("upstreamService", value.UpstreamService);
        writer.WriteNumber("upstreamPort", value.UpstreamPort);
        writer.WriteString("network", value.Network);
        writer.WriteString("image", value.Image);

        writer.WriteString("tlsMode", value.TlsMode.ToString());
        if (value.TlsCertRef != null) writer.WriteString("tlsCertRef", value.TlsCertRef);
        if (value.LetsEncryptEmail != null) writer.WriteString("letsEncryptEmail", value.LetsEncryptEmail);
        if (value.LetsEncryptDnsChallenge != null) writer.WriteString("letsEncryptDnsChallenge", value.LetsEncryptDnsChallenge);

        writer.WriteString("maintenancePageMode", value.MaintenancePageMode.ToString());
        if (value.BundlePath != null) writer.WriteString("bundlePath", value.BundlePath);
        if (value.MaintenanceContainerService != null) writer.WriteString("maintenanceContainerService", value.MaintenanceContainerService);

        writer.WriteStartObject("branding");
        if (value.Branding.ProductName != null) writer.WriteString("productName", value.Branding.ProductName);
        if (value.Branding.LogoUrl != null) writer.WriteString("logoUrl", value.Branding.LogoUrl);
        if (value.Branding.SupportContact != null) writer.WriteString("supportContact", value.Branding.SupportContact);
        writer.WriteStartArray("locales");
        foreach (var locale in value.Branding.Locales)
            writer.WriteStringValue(locale);
        writer.WriteEndArray();
        writer.WriteEndObject();

        writer.WriteEndObject();
    }

    private static string? GetString(JsonElement element, string name)
        => element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;

    private static int GetInt(JsonElement element, string name, int fallback)
        => element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.Number
            ? prop.GetInt32()
            : fallback;

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback) where TEnum : struct
        => Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) ? parsed : fallback;
}
