using System.IO.Compression;
using System.Text;
using FluentAssertions;
using ReadyStackGo.Application.Snmp.Prtg;
using Xunit;

namespace ReadyStackGo.UnitTests.Application.Snmp.Prtg;

public class PrtgBundleBuilderTests
{
    private static readonly byte[] SampleMib =
        Encoding.UTF8.GetBytes("READYSTACKGO-MIB DEFINITIONS ::= BEGIN\nEND\n");

    private static PrtgBundleInput MakeInput(string? rootOid = null, string? version = null) => new()
    {
        RootOid = rootOid ?? "1.3.6.1.4.1.65846.1",
        MibBytes = SampleMib,
        RsgoVersion = version,
        SourceHost = "rsgo.example.local",
        GeneratedAtUtc = new DateTime(2026, 5, 21, 7, 0, 0, DateTimeKind.Utc),
    };

    private static Dictionary<string, string> ReadAllEntries(byte[] zipBytes)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        using var ms = new MemoryStream(zipBytes);
        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
        foreach (var e in zip.Entries)
        {
            using var s = e.Open();
            using var r = new StreamReader(s, Encoding.UTF8);
            result[e.FullName] = r.ReadToEnd();
        }
        return result;
    }

    [Fact]
    public void Build_ProducesExpectedZipLayout()
    {
        var builder = new PrtgBundleBuilder();

        var result = builder.Build(MakeInput(version: "0.66.0"));
        var entries = ReadAllEntries(result.ZipBytes);

        entries.Keys.Should().Contain(new[]
        {
            "README.txt",
            "devicetemplates/readystackgo.template",
            "snmplibs/READYSTACKGO-MIB.txt",
            "lookups/custom/rsgo.productstatus.ovl",
            "lookups/custom/rsgo.stackstatus.ovl",
            "lookups/custom/rsgo.healthstatus.ovl",
            "lookups/custom/rsgo.environmenttype.ovl",
            "lookups/custom/rsgo.servicerunning.ovl",
            "lookups/custom/rsgo.dbhealth.ovl",
            "lookups/custom/rsgo.operationmode.ovl",
        });

        result.FileName.Should().Be("readystackgo-prtg-bundle-0.66.0.zip");
        result.ContentType.Should().Be("application/zip");
    }

    [Fact]
    public void Build_FileNameWithoutVersion_FallsBackToGenericName()
    {
        var result = new PrtgBundleBuilder().Build(MakeInput(version: null));

        result.FileName.Should().Be("readystackgo-prtg-bundle.zip");
    }

    [Fact]
    public void Build_SubstitutesRootOidIntoTemplate()
    {
        var customRoot = "1.3.6.1.4.1.42424.1";
        var entries = ReadAllEntries(new PrtgBundleBuilder().Build(MakeInput(rootOid: customRoot)).ZipBytes);

        var template = entries["devicetemplates/readystackgo.template"];
        template.Should().Contain($"<oid>{customRoot}.1.1.0</oid>", "system version OID should be rooted at the custom PEN");
        template.Should().Contain($"<baseoid>{customRoot}.3.1</baseoid>", "product table baseoid should be rooted at the custom PEN");
        template.Should().NotContain("{{rootOid}}", "all placeholders must be substituted");
    }

    [Fact]
    public void Build_SubstitutesAllReadmePlaceholders()
    {
        var entries = ReadAllEntries(new PrtgBundleBuilder()
            .Build(MakeInput(version: "0.66.0")).ZipBytes);

        var readme = entries["README.txt"];
        readme.Should().NotContain("{{")
            .And.Contain("1.3.6.1.4.1.65846.1")
            .And.Contain("0.66.0")
            .And.Contain("rsgo.example.local")
            .And.Contain("2026-05-21T07:00:00Z");
    }

    [Fact]
    public void Build_EmbedsMibBytesVerbatim()
    {
        var entries = ReadAllEntries(new PrtgBundleBuilder().Build(MakeInput()).ZipBytes);

        entries["snmplibs/READYSTACKGO-MIB.txt"].Should().Be(Encoding.UTF8.GetString(SampleMib));
    }

    [Fact]
    public void Build_ProductStatusLookup_CoversAllProductDeploymentStatusValues()
    {
        var entries = ReadAllEntries(new PrtgBundleBuilder().Build(MakeInput()).ZipBytes);
        var lookup = entries["lookups/custom/rsgo.productstatus.ovl"];

        // ProductDeploymentStatus has values 0..9 — every one must appear.
        for (int i = 0; i <= 9; i++)
            lookup.Should().Contain($"value=\"{i}\"", $"product status value {i} must be in the lookup");

        // Running = Ok, Failed = Error, PartiallyRunning = Warning.
        lookup.Should().Contain("state=\"Ok\"").And.Contain("Running");
        lookup.Should().Contain("state=\"Error\"").And.Contain("Failed");
        lookup.Should().Contain("state=\"Warning\"").And.Contain("PartiallyRunning");
    }

    [Fact]
    public void Build_HealthStatusLookup_DrivenByDomainEnum()
    {
        var entries = ReadAllEntries(new PrtgBundleBuilder().Build(MakeInput()).ZipBytes);
        var lookup = entries["lookups/custom/rsgo.healthstatus.ovl"];

        // Adding a new HealthStatus value in the domain should automatically
        // surface in the lookup — verify a representative subset.
        lookup.Should().Contain("Healthy")
              .And.Contain("Unhealthy")
              .And.Contain("Unknown");
    }

    [Fact]
    public void Build_OperationModeLookup_MaintenanceIsNoneSoSensorsDontAlert()
    {
        var entries = ReadAllEntries(new PrtgBundleBuilder().Build(MakeInput()).ZipBytes);

        var lookup = entries["lookups/custom/rsgo.operationmode.ovl"];
        lookup.Should().Contain("value=\"1\"")
              .And.Contain("Maintenance")
              .And.Contain("state=\"None\"",
                  "products in deliberate maintenance must not flip PRTG sensors to red");
    }

    [Fact]
    public void Build_RejectsEmptyRootOid()
    {
        var builder = new PrtgBundleBuilder();
        var input = new PrtgBundleInput { RootOid = "", MibBytes = SampleMib };

        Action act = () => builder.Build(input);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Build_RejectsEmptyMib()
    {
        var builder = new PrtgBundleBuilder();
        var input = new PrtgBundleInput { RootOid = "1.3.6.1.4.1.65846.1", MibBytes = Array.Empty<byte>() };

        Action act = () => builder.Build(input);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Build_OvlFilesAreWellFormedXml()
    {
        var entries = ReadAllEntries(new PrtgBundleBuilder().Build(MakeInput()).ZipBytes);

        foreach (var (name, content) in entries.Where(e => e.Key.EndsWith(".ovl", StringComparison.Ordinal)))
        {
            content.Should().StartWith("<?xml version=\"1.0\"", $"{name} must be valid XML");
            // Parse must succeed.
            Action parse = () => global::System.Xml.Linq.XDocument.Parse(content);
            parse.Should().NotThrow($"{name} must be parseable XML");
        }
    }

    [Fact]
    public void Build_TemplateIsWellFormedXml()
    {
        var entries = ReadAllEntries(new PrtgBundleBuilder().Build(MakeInput()).ZipBytes);
        var template = entries["devicetemplates/readystackgo.template"];

        Action parse = () => global::System.Xml.Linq.XDocument.Parse(template);
        parse.Should().NotThrow();
    }
}
