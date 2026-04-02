using FluentAssertions;
using ReadyStackGo.Infrastructure.Services.Deployment.Precheck;

namespace ReadyStackGo.UnitTests.Infrastructure.Precheck;

public class VariableSubstitutionTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Resolve_NullOrEmpty_ReturnsSame(string? input)
    {
        var result = VariableSubstitution.Resolve(input!, new Dictionary<string, string>());
        result.Should().Be(input);
    }

    [Fact]
    public void Resolve_NoVariables_ReturnsInput()
    {
        var result = VariableSubstitution.Resolve("8080", new Dictionary<string, string>());
        result.Should().Be("8080");
    }

    [Fact]
    public void Resolve_SimpleVariable_Substitutes()
    {
        var vars = new Dictionary<string, string> { { "PORT", "8080" } };
        var result = VariableSubstitution.Resolve("${PORT}", vars);
        result.Should().Be("8080");
    }

    [Fact]
    public void Resolve_VariableInPortMapping_Substitutes()
    {
        var vars = new Dictionary<string, string> { { "FRONTEND_PORT", "3000" } };
        var result = VariableSubstitution.Resolve("${FRONTEND_PORT}:80", vars);
        result.Should().Be("3000:80");
    }

    [Fact]
    public void Resolve_VariableWithDefault_UsesProvidedValue()
    {
        var vars = new Dictionary<string, string> { { "PORT", "9090" } };
        var result = VariableSubstitution.Resolve("${PORT:-8080}", vars);
        result.Should().Be("9090");
    }

    [Fact]
    public void Resolve_VariableWithDefault_UsesDefaultWhenMissing()
    {
        var result = VariableSubstitution.Resolve("${PORT:-8080}", new Dictionary<string, string>());
        result.Should().Be("8080");
    }

    [Fact]
    public void Resolve_MultipleVariables_ResolvesAll()
    {
        var vars = new Dictionary<string, string>
        {
            { "REGISTRY", "ghcr.io" },
            { "IMAGE", "myapp" },
            { "TAG", "v2" }
        };
        var result = VariableSubstitution.Resolve("${REGISTRY}/${IMAGE}:${TAG}", vars);
        result.Should().Be("ghcr.io/myapp:v2");
    }

    [Fact]
    public void Resolve_UnknownVariable_LeavesPlaceholder()
    {
        var result = VariableSubstitution.Resolve("${UNKNOWN_VAR}", new Dictionary<string, string>());
        result.Should().Be("${UNKNOWN_VAR}");
    }

    [Fact]
    public void Resolve_MixedResolvedAndUnresolved()
    {
        var vars = new Dictionary<string, string> { { "HOST", "localhost" } };
        var result = VariableSubstitution.Resolve("${HOST}:${PORT}", vars);
        result.Should().Be("localhost:${PORT}");
    }

    [Fact]
    public void Resolve_PortRange_ResolvesVariables()
    {
        var vars = new Dictionary<string, string>
        {
            { "PORT_START", "8080" },
            { "PORT_END", "8082" }
        };
        var result = VariableSubstitution.Resolve("${PORT_START}-${PORT_END}", vars);
        result.Should().Be("8080-8082");
    }
}
