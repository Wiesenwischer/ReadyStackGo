using FluentAssertions;
using Moq;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Stacks.GetProductReleaseNotes;
using ReadyStackGo.Domain.StackManagement.Stacks;

namespace ReadyStackGo.UnitTests.Application.Stacks;

public class GetCatalogProductReleaseNotesHandlerTests
{
    private readonly Mock<IProductSourceService> _productSourceMock = new();
    private readonly GetCatalogProductReleaseNotesHandler _handler;

    public GetCatalogProductReleaseNotesHandlerTests()
    {
        _handler = new GetCatalogProductReleaseNotesHandler(_productSourceMock.Object);
    }

    private static ProductDefinition CreateProduct(
        string productId, string version, string? changelog = null,
        IReadOnlyDictionary<string, string>? localized = null, string? url = null)
    {
        var pid = new ProductId(productId);
        var stack = new StackDefinition(
            "stacks", "stack-0", pid,
            services: new[] { new ServiceTemplate { Name = "svc", Image = "test:latest" } },
            variables: new[] { new Variable("VAR", "default") },
            productName: "test-product", productDisplayName: "Test", productVersion: version);

        return new ProductDefinition(
            "stacks", "test-product", "Test", new[] { stack }, productVersion: version,
            productId: productId, releaseNotesUrl: url, changelogMarkdown: changelog,
            localizedChangelogs: localized);
    }

    private void SetupCatalog(string productId, ProductDefinition? definition)
    {
        _productSourceMock
            .Setup(s => s.GetProductAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(definition);
    }

    [Fact]
    public async Task Handle_EmptyProductId_Fails()
    {
        var result = await _handler.Handle(
            new GetCatalogProductReleaseNotesQuery(""), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Mode.Should().Be("none");
    }

    [Fact]
    public async Task Handle_ProductNotFound_Fails()
    {
        SetupCatalog("com.example:2.0.0", null);

        var result = await _handler.Handle(
            new GetCatalogProductReleaseNotesQuery("com.example:2.0.0"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_ChangelogPresent_ReturnsMarkdown()
    {
        SetupCatalog("com.example:2.0.0", CreateProduct("com.example:2.0.0", "2.0.0", changelog: "# v2"));

        var result = await _handler.Handle(
            new GetCatalogProductReleaseNotesQuery("com.example:2.0.0"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Mode.Should().Be("markdown");
        result.Content.Should().Contain("# v2");
        result.Version.Should().Be("2.0.0");
    }

    [Fact]
    public async Task Handle_LocalizedChangelog_ReturnsRequestedLanguage_AndListsAvailable()
    {
        var localized = new Dictionary<string, string> { ["de"] = "# DE", ["en"] = "# EN" };
        SetupCatalog("com.example:2.0.0", CreateProduct("com.example:2.0.0", "2.0.0", localized: localized));

        var result = await _handler.Handle(
            new GetCatalogProductReleaseNotesQuery("com.example:2.0.0", "en"), CancellationToken.None);

        result.Mode.Should().Be("markdown");
        result.Content.Should().Contain("EN");
        result.Locale.Should().Be("en");
        result.AvailableLocales.Should().BeEquivalentTo(new[] { "de", "en" });
    }

    [Fact]
    public async Task Handle_OnlyUrl_ReturnsUrlMode()
    {
        SetupCatalog("com.example:2.0.0", CreateProduct("com.example:2.0.0", "2.0.0", url: "https://example.com/v2"));

        var result = await _handler.Handle(
            new GetCatalogProductReleaseNotesQuery("com.example:2.0.0"), CancellationToken.None);

        result.Mode.Should().Be("url");
        result.Url.Should().Be("https://example.com/v2");
    }

    [Fact]
    public async Task Handle_NoReleaseNotes_Fails()
    {
        SetupCatalog("com.example:2.0.0", CreateProduct("com.example:2.0.0", "2.0.0"));

        var result = await _handler.Handle(
            new GetCatalogProductReleaseNotesQuery("com.example:2.0.0"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Mode.Should().Be("none");
    }
}
