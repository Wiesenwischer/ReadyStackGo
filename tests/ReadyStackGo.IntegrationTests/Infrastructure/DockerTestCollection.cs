using System.Diagnostics;
using Xunit;

namespace ReadyStackGo.IntegrationTests.Infrastructure;

/// <summary>
/// Collection definition for Docker-dependent tests.
/// Tests in this collection require Docker to be running and accessible via Testcontainers.
/// To run only non-Docker tests: dotnet test --filter "Category!=Docker"
/// </summary>
[CollectionDefinition("Docker")]
public class DockerTestCollection : ICollectionFixture<DockerTestFixture>
{
}

/// <summary>
/// Fixture for Docker tests that checks if Docker is available.
/// </summary>
public class DockerTestFixture : IDisposable
{
    public bool IsDockerAvailable { get; }

    public DockerTestFixture()
    {
        IsDockerAvailable = CheckDockerAvailability();
    }

    private static bool CheckDockerAvailability()
    {
        try
        {
            // Actually test Docker connectivity by running docker info
            var psi = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "info",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return false;

            process.WaitForExit(5000); // 5 second timeout
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        // No cleanup needed
    }
}
