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

// Step 3: Connections Configuration (Simple Mode)
public class SetConnectionsRequest
{
    public required string Transport { get; set; }
    public required string Persistence { get; set; }
    public string? EventStore { get; set; }
}

public class SetConnectionsResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
}

// Step 4: Install Stack
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
}
