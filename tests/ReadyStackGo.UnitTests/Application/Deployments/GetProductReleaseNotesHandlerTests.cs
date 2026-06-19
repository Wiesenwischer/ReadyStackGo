using FluentAssertions;
using Moq;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Deployments.GetProductReleaseNotes;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.ProductDeployments;
using ReadyStackGo.Domain.StackManagement.Stacks;
using UserId = ReadyStackGo.Domain.Deployment.UserId;

namespace ReadyStackGo.UnitTests.Application.Deployments;

public class GetProductReleaseNotesHandlerTests
{
    private readonly Mock<IProductDeploymentRepository> _repositoryMock = new();
    private readonly Mock<IProductSourceService> _productSourceMock = new();
    private readonly GetProductReleaseNotesHandler _handler;

    public GetProductReleaseNotesHandlerTests()
    {
        _handler = new GetProductReleaseNotesHandler(_repositoryMock.Object, _productSourceMock.Object);
    }

    private static ProductDefinition CreateProduct(
        string version, string? releaseNotesUrl = null, string? changelog = null)
    {
        var productId = new ProductId("stacks:test-product");
        var stack = new StackDefinition(
            "stacks", "stack-0", productId,
            services: new[] { new ServiceTemplate { Name = "svc", Image = "test:latest" } },
            variables: new[] { new Variable("VAR", "default") },
            productName: "test-product", productDisplayName: "Test", productVersion: version);

        return new ProductDefinition(
            "stacks", "test-product", "Test", new[] { stack }, productVersion: version,
            releaseNotesUrl: releaseNotesUrl, changelogMarkdown: changelog);
    }

    private ProductDeployment SetupDeployment(ProductDefinition product)
    {
        var stackConfigs = product.Stacks.Select(s => new StackDeploymentConfig(
            s.Name, s.Name, s.Id.Value, s.Services.Count, new Dictionary<string, string>())).ToList();

        var deployment = ProductDeployment.InitiateDeployment(
            ProductDeploymentId.NewId(), new EnvironmentId(Guid.NewGuid()),
            product.GroupId, product.Id, product.Name, product.DisplayName,
            product.ProductVersion ?? "1.0.0", UserId.Create(), "test-deployment",
            stackConfigs, new Dictionary<string, string>());

        _repositoryMock.Setup(r => r.Get(It.IsAny<ProductDeploymentId>())).Returns(deployment);
        return deployment;
    }

    private void SetupCatalog(string groupId, params ProductDefinition[] versions)
    {
        _productSourceMock
            .Setup(s => s.GetProductVersionsAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(versions);
    }

    [Fact]
    public async Task Handle_ChangelogPresent_ReturnsMarkdownMode()
    {
        var product = CreateProduct("1.0.0");
        var deployment = SetupDeployment(product);
        SetupCatalog(deployment.ProductGroupId, CreateProduct("2.0.0", changelog: "# v2\n- new"));

        var result = await _handler.Handle(
            new GetProductReleaseNotesQuery(deployment.Id.Value.ToString(), "2.0.0"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Mode.Should().Be("markdown");
        result.Content.Should().Contain("# v2");
    }

    [Fact]
    public async Task Handle_OnlyUrlPresent_ReturnsUrlMode()
    {
        var product = CreateProduct("1.0.0");
        var deployment = SetupDeployment(product);
        SetupCatalog(deployment.ProductGroupId, CreateProduct("2.0.0", releaseNotesUrl: "https://example.com/v2"));

        var result = await _handler.Handle(
            new GetProductReleaseNotesQuery(deployment.Id.Value.ToString(), "2.0.0"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Mode.Should().Be("url");
        result.Url.Should().Be("https://example.com/v2");
    }

    [Fact]
    public async Task Handle_ChangelogPreferredOverUrl()
    {
        var product = CreateProduct("1.0.0");
        var deployment = SetupDeployment(product);
        SetupCatalog(deployment.ProductGroupId, CreateProduct("2.0.0", releaseNotesUrl: "https://example.com/v2", changelog: "# v2"));

        var result = await _handler.Handle(
            new GetProductReleaseNotesQuery(deployment.Id.Value.ToString(), "2.0.0"), CancellationToken.None);

        result.Mode.Should().Be("markdown");
    }

    [Fact]
    public async Task Handle_NoReleaseNotes_Fails()
    {
        var product = CreateProduct("1.0.0");
        var deployment = SetupDeployment(product);
        SetupCatalog(deployment.ProductGroupId, CreateProduct("2.0.0"));

        var result = await _handler.Handle(
            new GetProductReleaseNotesQuery(deployment.Id.Value.ToString(), "2.0.0"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Mode.Should().Be("none");
    }

    [Fact]
    public async Task Handle_VersionNotInCatalog_Fails()
    {
        var product = CreateProduct("1.0.0");
        var deployment = SetupDeployment(product);
        SetupCatalog(deployment.ProductGroupId, CreateProduct("2.0.0", changelog: "x"));

        var result = await _handler.Handle(
            new GetProductReleaseNotesQuery(deployment.Id.Value.ToString(), "9.9.9"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_DeploymentNotFound_Fails()
    {
        _repositoryMock.Setup(r => r.Get(It.IsAny<ProductDeploymentId>())).Returns((ProductDeployment?)null);

        var result = await _handler.Handle(
            new GetProductReleaseNotesQuery(Guid.NewGuid().ToString(), "1.0.0"), CancellationToken.None);

        result.Success.Should().BeFalse();
    }

    [Theory]
    [InlineData("not-a-guid", "1.0.0")]
    [InlineData("11111111-1111-1111-1111-111111111111", "")]
    public async Task Handle_InvalidInput_Fails(string id, string version)
    {
        var result = await _handler.Handle(
            new GetProductReleaseNotesQuery(id, version), CancellationToken.None);

        result.Success.Should().BeFalse();
    }
}
