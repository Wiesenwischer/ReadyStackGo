using FluentAssertions;
using Moq;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Containers;
using ReadyStackGo.Application.UseCases.Containers.GetContainerContext;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment;
using ReadyStackGo.Domain.StackManagement.Stacks;

namespace ReadyStackGo.UnitTests.Application.Containers;

public class GetContainerContextHandlerTests
{
    private readonly Mock<IDockerService> _dockerServiceMock;
    private readonly Mock<IDeploymentRepository> _deploymentRepositoryMock;
    private readonly Mock<IProductCache> _productCacheMock;
    private readonly GetContainerContextHandler _handler;

    private static readonly Guid EnvGuid = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private static readonly string EnvId = EnvGuid.ToString();
    private static readonly EnvironmentId EnvironmentId = new(EnvGuid);

    public GetContainerContextHandlerTests()
    {
        _dockerServiceMock = new Mock<IDockerService>();
        _deploymentRepositoryMock = new Mock<IDeploymentRepository>();
        _productCacheMock = new Mock<IProductCache>();
        _handler = new GetContainerContextHandler(
            _dockerServiceMock.Object,
            _deploymentRepositoryMock.Object,
            _productCacheMock.Object);
    }

    private static ContainerDto MakeContainer(string id, string name, Dictionary<string, string>? labels = null) =>
        new() { Id = id, Name = name, Image = "test:latest", State = "running", Status = "Up 2 hours", Labels = labels ?? new() };

    private static Deployment MakeDeployment(string stackName, string stackId)
    {
        return Deployment.StartInstallation(
            new DeploymentId(Guid.NewGuid()),
            EnvironmentId,
            stackId,
            stackName,
            $"project-{stackName}",
            UserId.Create());
    }

    [Fact]
    public async Task Handle_AllStacksHaveDeployments_AllContextsPopulated()
    {
        var containers = new[]
        {
            MakeContainer("c1", "wp-app", new() { ["rsgo.stack"] = "wordpress" }),
            MakeContainer("c2", "wp-db", new() { ["rsgo.stack"] = "wordpress" }),
            MakeContainer("c3", "redis-1", new() { ["rsgo.stack"] = "redis" }),
        };
        _dockerServiceMock
            .Setup(s => s.ListContainersAsync(EnvId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(containers);

        var wpDeployment = MakeDeployment("wordpress", "source1:wordpress-product:wordpress");
        var redisDeployment = MakeDeployment("redis", "source1:redis-product:redis");
        _deploymentRepositoryMock
            .Setup(r => r.GetByStackName(EnvironmentId, "wordpress")).Returns(wpDeployment);
        _deploymentRepositoryMock
            .Setup(r => r.GetByStackName(EnvironmentId, "redis")).Returns(redisDeployment);

        var wpStack = new StackDefinition("source1", "wordpress", ProductId.FromName("wordpress-product"),
            productName: "WordPress", productDisplayName: "WordPress CMS");
        var redisStack = new StackDefinition("source1", "redis", ProductId.FromName("redis-product"),
            productName: "Redis", productDisplayName: "Redis Cache");
        _productCacheMock.Setup(c => c.GetStack("source1:wordpress-product:wordpress")).Returns(wpStack);
        _productCacheMock.Setup(c => c.GetStack("source1:redis-product:redis")).Returns(redisStack);

        var result = await _handler.Handle(new GetContainerContextQuery(EnvId), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Stacks.Should().HaveCount(2);
        result.Stacks["wordpress"].DeploymentExists.Should().BeTrue();
        result.Stacks["wordpress"].ProductDisplayName.Should().Be("WordPress CMS");
        result.Stacks["redis"].DeploymentExists.Should().BeTrue();
        result.Stacks["redis"].ProductDisplayName.Should().Be("Redis Cache");
    }

    [Fact]
    public async Task Handle_ContainersWithoutLabels_NotIncludedInStacks()
    {
        var containers = new[]
        {
            MakeContainer("c1", "wp-app", new() { ["rsgo.stack"] = "wordpress" }),
            MakeContainer("c2", "portainer", new() { ["other.label"] = "value" }),
            MakeContainer("c3", "traefik"),
        };
        _dockerServiceMock
            .Setup(s => s.ListContainersAsync(EnvId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(containers);

        _deploymentRepositoryMock
            .Setup(r => r.GetByStackName(EnvironmentId, "wordpress"))
            .Returns(MakeDeployment("wordpress", "src:wp:wordpress"));
        _productCacheMock
            .Setup(c => c.GetStack(It.IsAny<string>()))
            .Returns((StackDefinition?)null);

        var result = await _handler.Handle(new GetContainerContextQuery(EnvId), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Stacks.Should().HaveCount(1);
        result.Stacks.Should().ContainKey("wordpress");
    }

    [Fact]
    public async Task Handle_StackWithoutDeployment_DeploymentExistsFalse()
    {
        var containers = new[]
        {
            MakeContainer("c1", "orphan-app", new() { ["rsgo.stack"] = "orphan-stack" }),
        };
        _dockerServiceMock
            .Setup(s => s.ListContainersAsync(EnvId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(containers);

        _deploymentRepositoryMock
            .Setup(r => r.GetByStackName(EnvironmentId, "orphan-stack"))
            .Returns((Deployment?)null);

        var result = await _handler.Handle(new GetContainerContextQuery(EnvId), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Stacks["orphan-stack"].DeploymentExists.Should().BeFalse();
        result.Stacks["orphan-stack"].DeploymentId.Should().BeNull();
        result.Stacks["orphan-stack"].ProductName.Should().BeNull();
        result.Stacks["orphan-stack"].ProductDisplayName.Should().BeNull();
    }

    [Fact]
    public async Task Handle_StackWithDeployment_ProductInfoResolved()
    {
        var containers = new[]
        {
            MakeContainer("c1", "app", new() { ["rsgo.stack"] = "mystack" }),
        };
        _dockerServiceMock
            .Setup(s => s.ListContainersAsync(EnvId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(containers);

        var deployment = MakeDeployment("mystack", "local:myproduct:mystack");
        _deploymentRepositoryMock
            .Setup(r => r.GetByStackName(EnvironmentId, "mystack")).Returns(deployment);

        var stackDef = new StackDefinition("local", "mystack", ProductId.FromName("myproduct"),
            productName: "MyProduct", productDisplayName: "My Product Display");
        _productCacheMock.Setup(c => c.GetStack("local:myproduct:mystack")).Returns(stackDef);

        var result = await _handler.Handle(new GetContainerContextQuery(EnvId), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Stacks["mystack"].DeploymentExists.Should().BeTrue();
        result.Stacks["mystack"].ProductName.Should().Be("MyProduct");
        result.Stacks["mystack"].ProductDisplayName.Should().Be("My Product Display");
        result.Stacks["mystack"].DeploymentId.Should().Be(deployment.Id.ToString());
    }

    [Fact]
    public async Task Handle_StackWithDeployment_ProductNotInCache_ProductNameNull()
    {
        var containers = new[]
        {
            MakeContainer("c1", "app", new() { ["rsgo.stack"] = "mystack" }),
        };
        _dockerServiceMock
            .Setup(s => s.ListContainersAsync(EnvId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(containers);

        var deployment = MakeDeployment("mystack", "local:myproduct:mystack");
        _deploymentRepositoryMock
            .Setup(r => r.GetByStackName(EnvironmentId, "mystack")).Returns(deployment);

        _productCacheMock.Setup(c => c.GetStack("local:myproduct:mystack")).Returns((StackDefinition?)null);

        var result = await _handler.Handle(new GetContainerContextQuery(EnvId), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Stacks["mystack"].DeploymentExists.Should().BeTrue();
        result.Stacks["mystack"].ProductName.Should().BeNull();
        result.Stacks["mystack"].ProductDisplayName.Should().BeNull();
    }

    [Fact]
    public async Task Handle_DockerServiceThrows_ReturnsError()
    {
        _dockerServiceMock
            .Setup(s => s.ListContainersAsync(EnvId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Docker not available"));

        var result = await _handler.Handle(new GetContainerContextQuery(EnvId), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Docker not available");
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("")]
    [InlineData("12345")]
    public async Task Handle_InvalidEnvironmentId_ReturnsError(string invalidEnvId)
    {
        var result = await _handler.Handle(new GetContainerContextQuery(invalidEnvId), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid environment ID");
    }

    [Fact]
    public async Task Handle_DuplicateStackLabels_DeduplicatedCorrectly()
    {
        var containers = new[]
        {
            MakeContainer("c1", "wp-app", new() { ["rsgo.stack"] = "wordpress" }),
            MakeContainer("c2", "wp-db", new() { ["rsgo.stack"] = "wordpress" }),
            MakeContainer("c3", "wp-cache", new() { ["rsgo.stack"] = "WordPress" }),
        };
        _dockerServiceMock
            .Setup(s => s.ListContainersAsync(EnvId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(containers);

        _deploymentRepositoryMock
            .Setup(r => r.GetByStackName(EnvironmentId, It.IsAny<string>()))
            .Returns((Deployment?)null);

        var result = await _handler.Handle(new GetContainerContextQuery(EnvId), CancellationToken.None);

        result.Success.Should().BeTrue();
        // "wordpress" and "WordPress" should be deduplicated (case-insensitive)
        result.Stacks.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_NoContainers_ReturnsEmptyStacks()
    {
        _dockerServiceMock
            .Setup(s => s.ListContainersAsync(EnvId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ContainerDto>());

        var result = await _handler.Handle(new GetContainerContextQuery(EnvId), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Stacks.Should().BeEmpty();
    }
}
