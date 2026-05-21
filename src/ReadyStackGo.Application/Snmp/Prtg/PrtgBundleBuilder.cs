using System.IO.Compression;
using System.Reflection;
using System.Text;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.Health;
using ReadyStackGo.Domain.Deployment.ProductDeployments;

namespace ReadyStackGo.Application.Snmp.Prtg;

/// <summary>
/// Builds the ReadyStackGo PRTG integration bundle: a single ZIP that an
/// admin unpacks into the PRTG install directory to get a ready-to-use
/// device template, the MIB, and value-lookup files generated from the
/// domain enums (so they always match the runtime data).
///
/// The ZIP layout matches PRTG's install layout so every file lands in
/// the correct subdirectory:
///   devicetemplates/readystackgo.template
///   snmplibs/READYSTACKGO-MIB.txt
///   lookups/custom/rsgo.*.ovl
///   README.txt
/// </summary>
public sealed class PrtgBundleBuilder : IPrtgBundleBuilder
{
    private const string TemplateResource =
        "ReadyStackGo.Application.Snmp.Prtg.Resources.devicetemplates.readystackgo.template";
    private const string ReadmeResource =
        "ReadyStackGo.Application.Snmp.Prtg.Resources.README.txt";

    private static readonly Assembly Assembly = typeof(PrtgBundleBuilder).Assembly;

    public PrtgBundleResult Build(PrtgBundleInput input)
    {
        if (string.IsNullOrWhiteSpace(input.RootOid))
            throw new ArgumentException("RootOid is required.", nameof(input));
        if (input.MibBytes is null || input.MibBytes.Length == 0)
            throw new ArgumentException("MIB bytes are required.", nameof(input));

        var template = ReadEmbedded(TemplateResource);
        var readme = ReadEmbedded(ReadmeResource);

        var generatedAt = input.GeneratedAtUtc.ToString("yyyy-MM-ddTHH:mm:ssZ");
        var rsgoVersion = string.IsNullOrWhiteSpace(input.RsgoVersion) ? "unknown" : input.RsgoVersion!;
        var sourceHost = string.IsNullOrWhiteSpace(input.SourceHost) ? "(unknown)" : input.SourceHost!;

        template = template.Replace("{{rootOid}}", input.RootOid);
        readme = readme
            .Replace("{{rootOid}}", input.RootOid)
            .Replace("{{generatedAt}}", generatedAt)
            .Replace("{{rsgoVersion}}", rsgoVersion)
            .Replace("{{sourceHost}}", sourceHost);

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddText(zip, "README.txt", readme);
            AddText(zip, "devicetemplates/readystackgo.template", template);
            AddBytes(zip, "snmplibs/READYSTACKGO-MIB.txt", input.MibBytes);

            // Lookups — generated from domain enums so they always match the
            // values the agent reports. PRTG matches the file's `id` attribute
            // against the `valuelookup` in the template.
            AddText(zip, "lookups/custom/rsgo.productstatus.ovl", BuildProductStatusLookup());
            AddText(zip, "lookups/custom/rsgo.stackstatus.ovl", BuildStackStatusLookup());
            AddText(zip, "lookups/custom/rsgo.healthstatus.ovl", BuildHealthStatusLookup());
            AddText(zip, "lookups/custom/rsgo.environmenttype.ovl", BuildEnvironmentTypeLookup());
            AddText(zip, "lookups/custom/rsgo.servicerunning.ovl", BuildServiceRunningLookup());
            AddText(zip, "lookups/custom/rsgo.dbhealth.ovl", BuildDbHealthLookup());
            AddText(zip, "lookups/custom/rsgo.operationmode.ovl", BuildOperationModeLookup());
        }

        var fileName = string.IsNullOrWhiteSpace(input.RsgoVersion)
            ? "readystackgo-prtg-bundle.zip"
            : $"readystackgo-prtg-bundle-{input.RsgoVersion}.zip";

        return new PrtgBundleResult
        {
            ZipBytes = ms.ToArray(),
            FileName = fileName,
        };
    }

    // ─── Lookup generation ────────────────────────────────────────────

    private static string BuildProductStatusLookup()
    {
        // Mapping derived from ProductDeploymentStatus. "PartiallyRunning"
        // is a Warning, terminal states (Removed, Superseded) are None so
        // a sensor doesn't go red for a deliberately removed product.
        var entries = new (int value, string state, string text)[]
        {
            (0, "Warning", "Deploying"),
            (1, "Ok", "Running"),
            (2, "Warning", "PartiallyRunning"),
            (3, "Warning", "Upgrading"),
            (4, "Error", "Failed"),
            (5, "Warning", "Removing"),
            (6, "None", "Removed"),
            (7, "Warning", "Stopped"),
            (8, "Warning", "Redeploying"),
            (9, "None", "Superseded"),
        };
        return RenderLookup("rsgo.productstatus",
            "ReadyStackGo Product Deployment Status (matches rsgoProductStatus enum)", entries);
    }

    private static string BuildStackStatusLookup()
    {
        var entries = new (int value, string state, string text)[]
        {
            ((int)StackDeploymentStatus.Pending,   "Warning", "Pending"),
            ((int)StackDeploymentStatus.Deploying, "Warning", "Deploying"),
            ((int)StackDeploymentStatus.Running,   "Ok",      "Running"),
            ((int)StackDeploymentStatus.Failed,    "Error",   "Failed"),
            ((int)StackDeploymentStatus.Removed,   "None",    "Removed"),
            ((int)StackDeploymentStatus.Stopped,   "Warning", "Stopped"),
        };
        return RenderLookup("rsgo.stackstatus",
            "ReadyStackGo Stack Status (matches rsgoStackStatus enum)", entries);
    }

    private static string BuildHealthStatusLookup()
    {
        // HealthStatus is a value object — use the catalog
        var entries = HealthStatus.GetAll()
            .Select(h => (h.Value, MapSeverity(h.SeverityLevel), h.Name))
            .ToArray();
        return RenderLookup("rsgo.healthstatus",
            "ReadyStackGo Service Health (matches rsgoServiceHealthStatus enum)", entries);
    }

    private static string MapSeverity(Severity severity) => severity switch
    {
        Severity.None => "Ok",
        Severity.Info => "Ok",
        Severity.Warning => "Warning",
        Severity.Critical => "Error",
        _ => "None",
    };

    private static string BuildEnvironmentTypeLookup()
    {
        var entries = new (int value, string state, string text)[]
        {
            ((int)EnvironmentType.DockerSocket, "Ok", "DockerSocket"),
            ((int)EnvironmentType.DockerTcp,    "Ok", "DockerTcp"),
            ((int)EnvironmentType.DockerAgent,  "Ok", "DockerAgent"),
            ((int)EnvironmentType.SshTunnel,    "Ok", "SshTunnel"),
        };
        return RenderLookup("rsgo.environmenttype",
            "ReadyStackGo Environment Type (matches rsgoEnvironmentType enum)", entries);
    }

    private static string BuildServiceRunningLookup()
    {
        var entries = new (int value, string state, string text)[]
        {
            (0, "Error", "stopped"),
            (1, "Ok",    "running"),
        };
        return RenderLookup("rsgo.servicerunning",
            "ReadyStackGo Service Running flag (0=stopped, 1=running)", entries);
    }

    private static string BuildDbHealthLookup()
    {
        // rsgoSystemDbHealth: 0=unknown, 1=ok, 2=fail (see MIB).
        var entries = new (int value, string state, string text)[]
        {
            (0, "Warning", "unknown"),
            (1, "Ok",      "ok"),
            (2, "Error",   "fail"),
        };
        return RenderLookup("rsgo.dbhealth",
            "ReadyStackGo database health probe result", entries);
    }

    private static string BuildOperationModeLookup()
    {
        // OperationMode: 0=Normal, 1=Maintenance. Maintenance is "None" so a
        // product in deliberate maintenance does not raise a sensor alert.
        var entries = new (int value, string state, string text)[]
        {
            (0, "Ok",   "Normal"),
            (1, "None", "Maintenance"),
        };
        return RenderLookup("rsgo.operationmode",
            "ReadyStackGo Product Operation Mode (Normal vs. Maintenance)", entries);
    }

    private static string RenderLookup(string id, string description,
        IEnumerable<(int value, string state, string text)> entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine("""<?xml version="1.0" encoding="UTF-8"?>""");
        sb.Append("<!-- ").Append(description).AppendLine(" -->");
        sb.Append("<ValueLookup id=\"").Append(id)
          .AppendLine("\" desiredValue=\"1\" undefinedState=\"Warning\">");
        sb.AppendLine("  <Lookups>");
        foreach (var (value, state, text) in entries)
        {
            sb.Append("    <SingleInt state=\"").Append(state)
              .Append("\" value=\"").Append(value).Append("\">")
              .Append(EscapeXml(text))
              .AppendLine("</SingleInt>");
        }
        sb.AppendLine("  </Lookups>");
        sb.AppendLine("</ValueLookup>");
        return sb.ToString();
    }

    // ─── Helpers ──────────────────────────────────────────────────────

    private static string ReadEmbedded(string resourceName)
    {
        using var stream = Assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
            throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static void AddText(ZipArchive zip, string entryName, string content)
    {
        var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private static void AddBytes(ZipArchive zip, string entryName, byte[] bytes)
    {
        var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
        using var stream = entry.Open();
        stream.Write(bytes, 0, bytes.Length);
    }

    private static string EscapeXml(string s) => s
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;")
        .Replace("\"", "&quot;");
}
