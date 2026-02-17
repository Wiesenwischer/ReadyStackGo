using FluentAssertions;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.ProductDeployments;

namespace ReadyStackGo.UnitTests.Domain.Deployment;

/// <summary>
/// Unit tests for ProductDeployment domain events and value objects.
/// </summary>
public class ProductDeploymentEventTests
{
    #region ProductDeploymentId

    [Fact]
    public void ProductDeploymentId_NewId_CreatesUniqueId()
    {
        var id1 = ProductDeploymentId.NewId();
        var id2 = ProductDeploymentId.NewId();

        id1.Should().NotBe(id2);
        id1.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void ProductDeploymentId_FromGuid_PreservesValue()
    {
        var guid = Guid.NewGuid();
        var id = ProductDeploymentId.FromGuid(guid);

        id.Value.Should().Be(guid);
    }

    [Fact]
    public void ProductDeploymentId_FromEmptyGuid_ThrowsArgumentException()
    {
        var act = () => ProductDeploymentId.FromGuid(Guid.Empty);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ProductDeploymentId_Equality_WorksByValue()
    {
        var guid = Guid.NewGuid();
        var id1 = ProductDeploymentId.FromGuid(guid);
        var id2 = ProductDeploymentId.FromGuid(guid);

        id1.Should().Be(id2);
        (id1 == id2).Should().BeTrue();
    }

    [Fact]
    public void ProductDeploymentId_ToString_ReturnsGuidString()
    {
        var guid = Guid.NewGuid();
        var id = ProductDeploymentId.FromGuid(guid);

        id.ToString().Should().Be(guid.ToString());
    }

    #endregion

    #region Product-Level Events

    [Fact]
    public void ProductDeploymentInitiated_ContainsCorrectData()
    {
        var pdId = ProductDeploymentId.NewId();
        var envId = EnvironmentId.NewId();

        var evt = new ProductDeploymentInitiated(pdId, envId, "myproduct", "1.0.0", 5);

        evt.ProductDeploymentId.Should().Be(pdId);
        evt.EnvironmentId.Should().Be(envId);
        evt.ProductName.Should().Be("myproduct");
        evt.ProductVersion.Should().Be("1.0.0");
        evt.TotalStacks.Should().Be(5);
        evt.OccurredOn.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        evt.EventVersion.Should().Be(1);
    }

    [Fact]
    public void ProductDeploymentCompleted_ContainsCorrectData()
    {
        var pdId = ProductDeploymentId.NewId();
        var duration = TimeSpan.FromMinutes(5);

        var evt = new ProductDeploymentCompleted(pdId, "myproduct", "1.0.0", 3, duration);

        evt.ProductDeploymentId.Should().Be(pdId);
        evt.ProductName.Should().Be("myproduct");
        evt.ProductVersion.Should().Be("1.0.0");
        evt.TotalStacks.Should().Be(3);
        evt.Duration.Should().Be(duration);
    }

    [Fact]
    public void ProductDeploymentPartiallyCompleted_ContainsCorrectData()
    {
        var pdId = ProductDeploymentId.NewId();

        var evt = new ProductDeploymentPartiallyCompleted(pdId, "myproduct", 2, 1, "Stack X failed");

        evt.RunningStacks.Should().Be(2);
        evt.FailedStacks.Should().Be(1);
        evt.Reason.Should().Be("Stack X failed");
    }

    [Fact]
    public void ProductDeploymentFailed_ContainsCorrectData()
    {
        var pdId = ProductDeploymentId.NewId();

        var evt = new ProductDeploymentFailed(pdId, "myproduct", "Critical error", 1, 2);

        evt.ErrorMessage.Should().Be("Critical error");
        evt.CompletedStacks.Should().Be(1);
        evt.FailedStacks.Should().Be(2);
    }

    [Fact]
    public void ProductUpgradeInitiated_ContainsCorrectData()
    {
        var pdId = ProductDeploymentId.NewId();

        var evt = new ProductUpgradeInitiated(pdId, "myproduct", "1.0.0", "2.0.0", 5);

        evt.PreviousVersion.Should().Be("1.0.0");
        evt.TargetVersion.Should().Be("2.0.0");
        evt.TotalStacks.Should().Be(5);
    }

    [Fact]
    public void ProductRemovalInitiated_ContainsCorrectData()
    {
        var pdId = ProductDeploymentId.NewId();

        var evt = new ProductRemovalInitiated(pdId, "myproduct", 3);

        evt.ProductName.Should().Be("myproduct");
        evt.TotalStacks.Should().Be(3);
    }

    [Fact]
    public void ProductDeploymentRemoved_ContainsCorrectData()
    {
        var pdId = ProductDeploymentId.NewId();

        var evt = new ProductDeploymentRemoved(pdId, "myproduct");

        evt.ProductDeploymentId.Should().Be(pdId);
        evt.ProductName.Should().Be("myproduct");
    }

    #endregion

    #region Stack-Level Events

    [Fact]
    public void ProductStackDeploymentStarted_ContainsCorrectData()
    {
        var pdId = ProductDeploymentId.NewId();
        var depId = DeploymentId.NewId();

        var evt = new ProductStackDeploymentStarted(pdId, "infrastructure", depId, 0, 5);

        evt.ProductDeploymentId.Should().Be(pdId);
        evt.StackName.Should().Be("infrastructure");
        evt.DeploymentId.Should().Be(depId);
        evt.StackIndex.Should().Be(0);
        evt.TotalStacks.Should().Be(5);
    }

    [Fact]
    public void ProductStackDeploymentCompleted_ContainsCorrectData()
    {
        var pdId = ProductDeploymentId.NewId();
        var depId = DeploymentId.NewId();

        var evt = new ProductStackDeploymentCompleted(pdId, "identity", depId, 2, 5);

        evt.StackName.Should().Be("identity");
        evt.CompletedStacks.Should().Be(2);
        evt.TotalStacks.Should().Be(5);
    }

    [Fact]
    public void ProductStackDeploymentFailed_ContainsCorrectData()
    {
        var pdId = ProductDeploymentId.NewId();

        var evt = new ProductStackDeploymentFailed(pdId, "business", "Timeout", 1, 5);

        evt.StackName.Should().Be("business");
        evt.ErrorMessage.Should().Be("Timeout");
        evt.CompletedStacks.Should().Be(1);
        evt.TotalStacks.Should().Be(5);
    }

    #endregion

    #region StackDeploymentConfig

    [Fact]
    public void StackDeploymentConfig_RecordEquality()
    {
        var vars = new Dictionary<string, string> { { "A", "1" } };
        var config1 = new StackDeploymentConfig("stack", "Stack", "sid", 2, vars);
        var config2 = new StackDeploymentConfig("stack", "Stack", "sid", 2, vars);

        config1.Should().Be(config2);
    }

    #endregion
}
