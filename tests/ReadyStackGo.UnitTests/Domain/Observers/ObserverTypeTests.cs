using FluentAssertions;
using ReadyStackGo.Domain.Deployment.Observers;

namespace ReadyStackGo.UnitTests.Domain.Observers;

/// <summary>
/// Unit tests for ObserverType enumeration class.
/// </summary>
public class ObserverTypeTests
{
    #region Static Instances

    [Fact]
    public void ObserverType_HasFourDefinedTypes()
    {
        var allTypes = ObserverType.GetAll().ToList();

        allTypes.Should().HaveCount(4);
        allTypes.Should().Contain(ObserverType.SqlExtendedProperty);
        allTypes.Should().Contain(ObserverType.SqlQuery);
        allTypes.Should().Contain(ObserverType.Http);
        allTypes.Should().Contain(ObserverType.File);
    }

    [Fact]
    public void ObserverType_Values_AreUnique()
    {
        var allTypes = ObserverType.GetAll();
        var values = allTypes.Select(t => t.Value).ToList();

        values.Should().OnlyHaveUniqueItems();
    }

    #endregion

    #region FromValue

    [Theory]
    [InlineData("sqlExtendedProperty")]
    [InlineData("sqlQuery")]
    [InlineData("http")]
    [InlineData("file")]
    public void FromValue_ValidValue_ReturnsCorrectType(string value)
    {
        var type = ObserverType.FromValue(value);

        type.Should().NotBeNull();
        type.Value.Should().Be(value);
    }

    [Theory]
    [InlineData("SQLEXTENDEDPROPERTY")]
    [InlineData("SqlQuery")]
    [InlineData("HTTP")]
    [InlineData("FILE")]
    public void FromValue_IsCaseInsensitive(string value)
    {
        var type = ObserverType.FromValue(value);

        type.Should().NotBeNull();
    }

    [Fact]
    public void FromValue_InvalidValue_ThrowsArgumentException()
    {
        var act = () => ObserverType.FromValue("invalid");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Unknown ObserverType*");
    }

    #endregion

    #region TryFromValue

    [Theory]
    [InlineData("sqlExtendedProperty", true)]
    [InlineData("sqlQuery", true)]
    [InlineData("http", true)]
    [InlineData("file", true)]
    [InlineData("invalid", false)]
    [InlineData("", false)]
    public void TryFromValue_ReturnsExpectedResult(string value, bool expectedSuccess)
    {
        var success = ObserverType.TryFromValue(value, out var type);

        success.Should().Be(expectedSuccess);
        if (expectedSuccess)
        {
            type.Should().NotBeNull();
        }
    }

    #endregion

    #region RequiresConnection

    [Theory]
    [InlineData("sqlExtendedProperty", true)]
    [InlineData("sqlQuery", true)]
    [InlineData("http", false)]
    [InlineData("file", false)]
    public void RequiresConnection_ReturnsCorrectValue(string value, bool expected)
    {
        var type = ObserverType.FromValue(value);

        type.RequiresConnection.Should().Be(expected);
    }

    #endregion

    #region RequiresUrl

    [Theory]
    [InlineData("sqlExtendedProperty", false)]
    [InlineData("sqlQuery", false)]
    [InlineData("http", true)]
    [InlineData("file", false)]
    public void RequiresUrl_ReturnsCorrectValue(string value, bool expected)
    {
        var type = ObserverType.FromValue(value);

        type.RequiresUrl.Should().Be(expected);
    }

    #endregion

    #region RequiresFilePath

    [Theory]
    [InlineData("sqlExtendedProperty", false)]
    [InlineData("sqlQuery", false)]
    [InlineData("http", false)]
    [InlineData("file", true)]
    public void RequiresFilePath_ReturnsCorrectValue(string value, bool expected)
    {
        var type = ObserverType.FromValue(value);

        type.RequiresFilePath.Should().Be(expected);
    }

    #endregion

    #region Equality

    [Fact]
    public void Equality_SameType_AreEqual()
    {
        var type1 = ObserverType.SqlExtendedProperty;
        var type2 = ObserverType.SqlExtendedProperty;

        type1.Should().Be(type2);
        (type1 == type2).Should().BeTrue();
    }

    [Fact]
    public void Equality_DifferentTypes_AreNotEqual()
    {
        var type1 = ObserverType.SqlExtendedProperty;
        var type2 = ObserverType.Http;

        type1.Should().NotBe(type2);
        (type1 != type2).Should().BeTrue();
    }

    #endregion

    #region ToString

    [Fact]
    public void ToString_ReturnsValue()
    {
        ObserverType.SqlExtendedProperty.ToString().Should().Be("sqlExtendedProperty");
        ObserverType.Http.ToString().Should().Be("http");
    }

    #endregion

    #region DisplayName

    [Theory]
    [InlineData("sqlExtendedProperty", "SQL Extended Property")]
    [InlineData("sqlQuery", "SQL Query")]
    [InlineData("http", "HTTP Endpoint")]
    [InlineData("file", "File")]
    public void DisplayName_ReturnsHumanReadableName(string value, string expectedDisplayName)
    {
        var type = ObserverType.FromValue(value);

        type.DisplayName.Should().Be(expectedDisplayName);
    }

    #endregion
}
