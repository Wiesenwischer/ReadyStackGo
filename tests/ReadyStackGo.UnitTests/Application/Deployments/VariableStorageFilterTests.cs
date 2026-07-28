using FluentAssertions;
using ReadyStackGo.Application.UseCases.Deployments;

namespace ReadyStackGo.UnitTests.Application.Deployments;

/// <summary>
/// Unit tests for VariableStorageFilter — the per-variable "save value" opt-out.
///
/// Regression for #465: for product deployments the opt-out was dropped before it reached the
/// backend, so every variable was persisted including passwords the user explicitly excluded.
/// </summary>
public class VariableStorageFilterTests
{
    private static readonly Dictionary<string, string> Variables = new()
    {
        ["DB_SERVER"] = "db.example.local",
        ["DB_ADMIN_PASSWORD"] = "s3cret",
        ["API_PORT"] = "5000",
    };

    [Fact]
    public void ForStorage_ExcludedVariable_IsDropped()
    {
        var result = VariableStorageFilter.ForStorage(
            Variables, new HashSet<string> { "DB_ADMIN_PASSWORD" });

        result.Should().NotContainKey("DB_ADMIN_PASSWORD");
        result.Should().ContainKey("DB_SERVER");
        result.Should().ContainKey("API_PORT");
    }

    [Fact]
    public void ForStorage_NoExclusions_KeepsEverything()
    {
        var result = VariableStorageFilter.ForStorage(Variables, null);

        result.Should().BeEquivalentTo(Variables);
    }

    [Fact]
    public void ForStorage_EmptyExclusionSet_KeepsEverything()
    {
        var result = VariableStorageFilter.ForStorage(Variables, new HashSet<string>());

        result.Should().BeEquivalentTo(Variables);
    }

    [Fact]
    public void ForStorage_AllExcluded_ReturnsEmpty()
    {
        var result = VariableStorageFilter.ForStorage(
            Variables, new HashSet<string>(Variables.Keys));

        result.Should().BeEmpty();
    }

    [Fact]
    public void ForStorage_UnknownExclusion_IsIgnored()
    {
        var result = VariableStorageFilter.ForStorage(
            Variables, new HashSet<string> { "NOT_A_VARIABLE" });

        result.Should().BeEquivalentTo(Variables);
    }

    [Fact]
    public void ForStorage_EmptyVariables_ReturnsEmpty()
    {
        var result = VariableStorageFilter.ForStorage(
            new Dictionary<string, string>(), new HashSet<string> { "DB_ADMIN_PASSWORD" });

        result.Should().BeEmpty();
    }

    [Fact]
    public void ForStorage_DoesNotMutateTheInput()
    {
        var input = new Dictionary<string, string>(Variables);

        VariableStorageFilter.ForStorage(input, new HashSet<string> { "DB_ADMIN_PASSWORD" });

        input.Should().ContainKey("DB_ADMIN_PASSWORD",
            "the full set is still deployed to Docker after filtering for storage");
    }

    [Fact]
    public void ForStorage_ExclusionIsCaseSensitiveLikeVariableNames()
    {
        // Variable names are matched exactly elsewhere in the deploy path; a differently-cased entry
        // must not silently fail to exclude, so callers pass the names they received.
        var result = VariableStorageFilter.ForStorage(
            Variables, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "db_admin_password" });

        result.Should().NotContainKey("DB_ADMIN_PASSWORD",
            "an ordinal-ignore-case set from the caller is honoured");
    }
}
