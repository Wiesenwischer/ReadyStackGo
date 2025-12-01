namespace ReadyStackGo.Application.UseCases.Wizard;

// Step 1: Admin Creation
public class CreateAdminRequest
{
    public required string Username { get; set; }
    public required string Password { get; set; }
}

public class CreateAdminResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
}

// Step 2: Organization Setup
public class SetOrganizationRequest
{
    public required string Id { get; set; }
    public required string Name { get; set; }
}

public class SetOrganizationResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
}

// Step 3: Install Stack
public class InstallStackRequest
{
    public string? ManifestPath { get; set; } // Optional: if not provided, use latest
}

public class InstallStackResponse
{
    public bool Success { get; set; }
    public string? StackVersion { get; set; }
    public List<string> DeployedContexts { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

// Wizard Status Query
public class WizardStatusResponse
{
    public string WizardState { get; set; } = "NotStarted";
    public bool IsCompleted { get; set; }

    /// <summary>
    /// The default Docker socket path for the server's operating system.
    /// Windows: "npipe://./pipe/docker_engine"
    /// Linux/macOS: "unix:///var/run/docker.sock"
    /// </summary>
    public string DefaultDockerSocketPath { get; set; } = string.Empty;

    /// <summary>
    /// Timeout information for the wizard window.
    /// </summary>
    public WizardTimeoutDto? Timeout { get; set; }
}

/// <summary>
/// Timeout information for the wizard setup window.
/// </summary>
public class WizardTimeoutDto
{
    /// <summary>
    /// Whether the wizard window has timed out.
    /// </summary>
    public bool IsTimedOut { get; set; }

    /// <summary>
    /// Whether the wizard is permanently locked (requires container restart).
    /// </summary>
    public bool IsLocked { get; set; }

    /// <summary>
    /// Remaining seconds until timeout. Null if already timed out.
    /// </summary>
    public int? RemainingSeconds { get; set; }

    /// <summary>
    /// When the timeout window expires (UTC).
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// The configured timeout duration in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; }
}
