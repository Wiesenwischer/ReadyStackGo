using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using ReadyStackGo.Domain.Deployment.Edge;
using ReadyStackGo.Infrastructure.Configuration;
using ReadyStackGo.Infrastructure.Services.Edge;
using Xunit;

namespace ReadyStackGo.UnitTests.Edge;

/// <summary>
/// Unit tests for the edge certificate provider: self-signed generation, persistence/reuse,
/// and the inert (null) case when TLS is disabled.
/// </summary>
public class EdgeCertificateProviderTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "rsgo-edge-cert-" + Guid.NewGuid().ToString("N"));

    private EdgeCertificateProvider CreateSut()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConfigPath"] = _tempDir })
            .Build();
        return new EdgeCertificateProvider(
            new Mock<IConfigStore>().Object, config, new Mock<ILogger<EdgeCertificateProvider>>().Object);
    }

    private static EdgeConfig Cfg(EdgeTlsMode mode, string host = "app.test") => EdgeConfig.Create(
        host, 443, "bff", 8080, "edge-net", "caddy:2.8.4", tlsMode: mode);

    [Fact]
    public async Task TlsModeNone_ReturnsNull()
    {
        (await CreateSut().GetCertificateAsync(Cfg(EdgeTlsMode.None))).Should().BeNull();
    }

    [Fact]
    public async Task SelfSigned_GeneratesValidPemForHostname()
    {
        var material = await CreateSut().GetCertificateAsync(Cfg(EdgeTlsMode.SelfSigned, "project.customer.tld"));

        material.Should().NotBeNull();
        material!.CertificatePem.Should().Contain("BEGIN CERTIFICATE");
        material.PrivateKeyPem.Should().Contain("BEGIN PRIVATE KEY");
        material.NotAfterUtc.Should().BeAfter(DateTime.UtcNow.AddDays(300));
        material.Thumbprint.Should().NotBeNullOrEmpty();

        using var cert = X509Certificate2.CreateFromPem(material.CertificatePem);
        cert.Subject.Should().Contain("CN=project.customer.tld");
    }

    [Fact]
    public async Task SelfSigned_IsPersisted_AndReusedAcrossCalls()
    {
        var sut = CreateSut();
        var first = await sut.GetCertificateAsync(Cfg(EdgeTlsMode.SelfSigned));
        var second = await sut.GetCertificateAsync(Cfg(EdgeTlsMode.SelfSigned));

        second!.Thumbprint.Should().Be(first!.Thumbprint, "a persisted cert is reused, not regenerated each cycle");
    }

    [Fact]
    public async Task Custom_WithMissingCertRef_FallsBackToSelfSigned()
    {
        var cfg = EdgeConfig.Create("app.test", 443, "bff", 8080, "edge-net", "caddy:2.8.4",
            tlsMode: EdgeTlsMode.Custom, tlsCertRef: "does-not-exist");

        var material = await CreateSut().GetCertificateAsync(cfg);

        material.Should().NotBeNull("missing custom cert falls back to a self-signed cert so the edge still terminates TLS");
        using var cert = X509Certificate2.CreateFromPem(material!.CertificatePem);
        cert.Subject.Should().Contain("CN=app.test");
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true); } catch { /* ignore */ }
    }
}
