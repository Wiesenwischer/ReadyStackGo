namespace ReadyStackGo.Infrastructure.DataAccess.Configurations;

using System.Text.Json;
using System.Text.Json.Serialization;
using ReadyStackGo.Domain.Deployment.Environments;

/// <summary>
/// JSON converter for polymorphic ConnectionConfig serialization.
/// Uses "configType" as discriminator to resolve subtypes.
/// </summary>
public class ConnectionConfigJsonConverter : JsonConverter<ConnectionConfig>
{
    public override ConnectionConfig? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        var configType = root.GetProperty("configType").GetString();

        return configType switch
        {
            "DockerSocket" => DeserializeDockerSocket(root),
            "SshTunnel" => DeserializeSshTunnel(root),
            _ => throw new JsonException($"Unknown ConnectionConfig type: {configType}")
        };
    }

    public override void Write(Utf8JsonWriter writer, ConnectionConfig value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("configType", value.ConfigType);

        switch (value)
        {
            case DockerSocketConfig socket:
                writer.WriteString("socketPath", socket.SocketPath);
                break;

            case SshTunnelConfig ssh:
                writer.WriteString("host", ssh.Host);
                writer.WriteNumber("port", ssh.Port);
                writer.WriteString("username", ssh.Username);
                writer.WriteString("authMethod", ssh.AuthMethod.ToString());
                writer.WriteString("remoteSocketPath", ssh.RemoteSocketPath);
                break;
        }

        writer.WriteEndObject();
    }

    private static DockerSocketConfig DeserializeDockerSocket(JsonElement root)
    {
        var socketPath = root.GetProperty("socketPath").GetString()
            ?? throw new JsonException("socketPath is required for DockerSocket config");
        return DockerSocketConfig.Create(socketPath);
    }

    private static SshTunnelConfig DeserializeSshTunnel(JsonElement root)
    {
        var host = root.GetProperty("host").GetString()
            ?? throw new JsonException("host is required for SshTunnel config");
        var port = root.GetProperty("port").GetInt32();
        var username = root.GetProperty("username").GetString()
            ?? throw new JsonException("username is required for SshTunnel config");
        var authMethodStr = root.GetProperty("authMethod").GetString()
            ?? throw new JsonException("authMethod is required for SshTunnel config");
        var remoteSocketPath = root.TryGetProperty("remoteSocketPath", out var rsp)
            ? rsp.GetString() ?? "/var/run/docker.sock"
            : "/var/run/docker.sock";

        if (!Enum.TryParse<SshAuthMethod>(authMethodStr, out var authMethod))
            throw new JsonException($"Unknown SshAuthMethod: {authMethodStr}");

        return SshTunnelConfig.Create(host, port, username, authMethod, remoteSocketPath);
    }
}
