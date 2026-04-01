using FluentAssertions;
using ReadyStackGo.Domain.Deployment.Precheck;

namespace ReadyStackGo.UnitTests.Domain.Precheck;

public class PrecheckResultTests
{
    #region Empty Result

    [Fact]
    public void Empty_ReturnsResultWithNoChecks()
    {
        var result = PrecheckResult.Empty;

        result.Checks.Should().BeEmpty();
        result.HasErrors.Should().BeFalse();
        result.HasWarnings.Should().BeFalse();
        result.CanDeploy.Should().BeTrue();
        result.ErrorCount.Should().Be(0);
        result.WarningCount.Should().Be(0);
    }

    [Fact]
    public void Empty_Summary_ReturnsAllPassed()
    {
        PrecheckResult.Empty.Summary.Should().Be("All checks passed");
    }

    #endregion

    #region HasErrors / CanDeploy

    [Fact]
    public void HasErrors_WithErrorItem_ReturnsTrue()
    {
        var result = new PrecheckResult([
            new PrecheckItem("Test", PrecheckSeverity.Error, "Failure")
        ]);

        result.HasErrors.Should().BeTrue();
        result.CanDeploy.Should().BeFalse();
    }

    [Fact]
    public void HasErrors_WithOnlyOKItems_ReturnsFalse()
    {
        var result = new PrecheckResult([
            new PrecheckItem("Test", PrecheckSeverity.OK, "All good")
        ]);

        result.HasErrors.Should().BeFalse();
        result.CanDeploy.Should().BeTrue();
    }

    [Fact]
    public void HasErrors_WithOnlyWarnings_ReturnsFalse()
    {
        var result = new PrecheckResult([
            new PrecheckItem("Test", PrecheckSeverity.Warning, "Caution")
        ]);

        result.HasErrors.Should().BeFalse();
        result.CanDeploy.Should().BeTrue();
    }

    #endregion

    #region HasWarnings

    [Fact]
    public void HasWarnings_WithWarningItem_ReturnsTrue()
    {
        var result = new PrecheckResult([
            new PrecheckItem("Test", PrecheckSeverity.Warning, "Caution")
        ]);

        result.HasWarnings.Should().BeTrue();
    }

    [Fact]
    public void HasWarnings_WithOnlyOK_ReturnsFalse()
    {
        var result = new PrecheckResult([
            new PrecheckItem("Test", PrecheckSeverity.OK, "All good")
        ]);

        result.HasWarnings.Should().BeFalse();
    }

    #endregion

    #region Counts

    [Fact]
    public void ErrorCount_CountsOnlyErrors()
    {
        var result = new PrecheckResult([
            new PrecheckItem("A", PrecheckSeverity.Error, "E1"),
            new PrecheckItem("B", PrecheckSeverity.Warning, "W1"),
            new PrecheckItem("C", PrecheckSeverity.Error, "E2"),
            new PrecheckItem("D", PrecheckSeverity.OK, "OK1")
        ]);

        result.ErrorCount.Should().Be(2);
        result.WarningCount.Should().Be(1);
    }

    #endregion

    #region Summary

    [Fact]
    public void Summary_WithErrors_IndicatesBlocked()
    {
        var result = new PrecheckResult([
            new PrecheckItem("Test", PrecheckSeverity.Error, "Fail")
        ]);

        result.Summary.Should().Contain("error").And.Contain("blocked");
    }

    [Fact]
    public void Summary_WithWarningsOnly_IndicatesPassedWithWarnings()
    {
        var result = new PrecheckResult([
            new PrecheckItem("Test", PrecheckSeverity.Warning, "Warn"),
            new PrecheckItem("Test2", PrecheckSeverity.OK, "OK")
        ]);

        result.Summary.Should().Contain("warning");
    }

    [Fact]
    public void Summary_AllOK_IndicatesAllPassed()
    {
        var result = new PrecheckResult([
            new PrecheckItem("Test", PrecheckSeverity.OK, "OK")
        ]);

        result.Summary.Should().Be("All checks passed");
    }

    #endregion

    #region Mixed Errors and Warnings

    [Fact]
    public void MixedResult_ErrorsTakePrecedence()
    {
        var result = new PrecheckResult([
            new PrecheckItem("A", PrecheckSeverity.OK, "OK"),
            new PrecheckItem("B", PrecheckSeverity.Warning, "Warn"),
            new PrecheckItem("C", PrecheckSeverity.Error, "Error")
        ]);

        result.HasErrors.Should().BeTrue();
        result.HasWarnings.Should().BeTrue();
        result.CanDeploy.Should().BeFalse();
        result.ErrorCount.Should().Be(1);
        result.WarningCount.Should().Be(1);
    }

    #endregion

    #region GetByRule / GetBySeverity

    [Fact]
    public void GetByRule_FiltersCorrectly()
    {
        var result = new PrecheckResult([
            new PrecheckItem("ImageAvailability", PrecheckSeverity.OK, "OK"),
            new PrecheckItem("PortConflict", PrecheckSeverity.Error, "Fail"),
            new PrecheckItem("ImageAvailability", PrecheckSeverity.Warning, "Warn")
        ]);

        result.GetByRule("ImageAvailability").Should().HaveCount(2);
        result.GetByRule("PortConflict").Should().HaveCount(1);
        result.GetByRule("NonExistent").Should().BeEmpty();
    }

    [Fact]
    public void GetBySeverity_FiltersCorrectly()
    {
        var result = new PrecheckResult([
            new PrecheckItem("A", PrecheckSeverity.OK, "OK"),
            new PrecheckItem("B", PrecheckSeverity.Error, "E1"),
            new PrecheckItem("C", PrecheckSeverity.Error, "E2")
        ]);

        result.GetBySeverity(PrecheckSeverity.Error).Should().HaveCount(2);
        result.GetBySeverity(PrecheckSeverity.OK).Should().HaveCount(1);
        result.GetBySeverity(PrecheckSeverity.Warning).Should().BeEmpty();
    }

    #endregion
}
