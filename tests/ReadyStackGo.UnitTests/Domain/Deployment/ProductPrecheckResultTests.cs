using FluentAssertions;
using ReadyStackGo.Domain.Deployment.Precheck;

namespace ReadyStackGo.UnitTests.Domain.Deployment;

public class ProductPrecheckResultTests
{
    [Fact]
    public void Empty_ReturnsResultWithNoStacks()
    {
        var result = ProductPrecheckResult.Empty;

        result.Stacks.Should().BeEmpty();
        result.CanDeploy.Should().BeTrue();
        result.HasErrors.Should().BeFalse();
        result.HasWarnings.Should().BeFalse();
        result.TotalErrorCount.Should().Be(0);
        result.TotalWarningCount.Should().Be(0);
    }

    [Fact]
    public void CanDeploy_AllStacksOK_ReturnsTrue()
    {
        var result = new ProductPrecheckResult([
            new ProductPrecheckStackResult("s1", "Stack1", new PrecheckResult([
                new PrecheckItem("Test", PrecheckSeverity.OK, "All good")])),
            new ProductPrecheckStackResult("s2", "Stack2", new PrecheckResult([
                new PrecheckItem("Test", PrecheckSeverity.OK, "All good")]))
        ]);

        result.CanDeploy.Should().BeTrue();
        result.HasErrors.Should().BeFalse();
        result.HasWarnings.Should().BeFalse();
        result.Summary.Should().Contain("2 stack(s)");
    }

    [Fact]
    public void CanDeploy_OneStackHasError_ReturnsFalse()
    {
        var result = new ProductPrecheckResult([
            new ProductPrecheckStackResult("s1", "Stack1", new PrecheckResult([
                new PrecheckItem("Test", PrecheckSeverity.OK, "All good")])),
            new ProductPrecheckStackResult("s2", "Stack2", new PrecheckResult([
                new PrecheckItem("Port", PrecheckSeverity.Error, "Port conflict")]))
        ]);

        result.CanDeploy.Should().BeFalse();
        result.HasErrors.Should().BeTrue();
        result.TotalErrorCount.Should().Be(1);
        result.Summary.Should().Contain("1 stack(s) have errors");
    }

    [Fact]
    public void CanDeploy_AllStacksHaveErrors_ReturnsFalse()
    {
        var result = new ProductPrecheckResult([
            new ProductPrecheckStackResult("s1", "Stack1", new PrecheckResult([
                new PrecheckItem("Port", PrecheckSeverity.Error, "Port conflict")])),
            new ProductPrecheckStackResult("s2", "Stack2", new PrecheckResult([
                new PrecheckItem("Image", PrecheckSeverity.Error, "Image not found")]))
        ]);

        result.CanDeploy.Should().BeFalse();
        result.HasErrors.Should().BeTrue();
        result.TotalErrorCount.Should().Be(2);
        result.Summary.Should().Contain("2 stack(s) have errors");
    }

    [Fact]
    public void CanDeploy_WarningsOnly_ReturnsTrue()
    {
        var result = new ProductPrecheckResult([
            new ProductPrecheckStackResult("s1", "Stack1", new PrecheckResult([
                new PrecheckItem("Volume", PrecheckSeverity.Warning, "Existing volume")])),
            new ProductPrecheckStackResult("s2", "Stack2", new PrecheckResult([
                new PrecheckItem("Test", PrecheckSeverity.OK, "All good")]))
        ]);

        result.CanDeploy.Should().BeTrue();
        result.HasWarnings.Should().BeTrue();
        result.HasErrors.Should().BeFalse();
        result.TotalWarningCount.Should().Be(1);
        result.Summary.Should().Contain("warnings in 1 stack(s)");
    }

    [Fact]
    public void TotalCounts_AggregateAcrossStacks()
    {
        var result = new ProductPrecheckResult([
            new ProductPrecheckStackResult("s1", "Stack1", new PrecheckResult([
                new PrecheckItem("Port", PrecheckSeverity.Error, "Port conflict"),
                new PrecheckItem("Volume", PrecheckSeverity.Warning, "Existing volume")])),
            new ProductPrecheckStackResult("s2", "Stack2", new PrecheckResult([
                new PrecheckItem("Image", PrecheckSeverity.Error, "Image not found"),
                new PrecheckItem("Network", PrecheckSeverity.Warning, "Custom network"),
                new PrecheckItem("Test", PrecheckSeverity.OK, "All good")]))
        ]);

        result.TotalErrorCount.Should().Be(2);
        result.TotalWarningCount.Should().Be(2);
    }

    [Fact]
    public void Summary_MixedErrorsAndWarnings_ReportsErrors()
    {
        var result = new ProductPrecheckResult([
            new ProductPrecheckStackResult("s1", "Stack1", new PrecheckResult([
                new PrecheckItem("Port", PrecheckSeverity.Error, "Port conflict"),
                new PrecheckItem("Volume", PrecheckSeverity.Warning, "Existing volume")])),
            new ProductPrecheckStackResult("s2", "Stack2", new PrecheckResult([
                new PrecheckItem("Volume", PrecheckSeverity.Warning, "Existing volume")]))
        ]);

        // Error takes priority over warning in summary
        result.Summary.Should().Contain("error");
        result.CanDeploy.Should().BeFalse();
    }
}
