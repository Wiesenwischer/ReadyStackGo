using Xunit;
using Xunit.Abstractions;

namespace ReadyStackGo.UnitTests;

/// <summary>
/// Utility test to generate BCrypt hash for E2E test fixtures
/// </summary>
public class GenerateTestHash
{
    private readonly ITestOutputHelper _output;

    public GenerateTestHash(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void GenerateBCryptHashForAdmin()
    {
        var password = "admin";
        var hash = BCrypt.Net.BCrypt.HashPassword(password, 12);

        _output.WriteLine($"Password: {password}");
        _output.WriteLine($"Hash: {hash}");
        _output.WriteLine($"Verify: {BCrypt.Net.BCrypt.Verify(password, hash)}");

        // Also verify the old hash from fixture doesn't work
        var oldHash = "$2a$11$rWh6RaKniBtoOGffQ26WhOFxM7tdoy2Qb50Jf8kNTXAV8O9sUuLzW";
        _output.WriteLine($"Old hash verifies: {BCrypt.Net.BCrypt.Verify(password, oldHash)}");
    }
}
