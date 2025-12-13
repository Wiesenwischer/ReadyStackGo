using ReadyStackGo.Application.UseCases.Deployments;
using ReadyStackGo.Domain.StackManagement.Manifests;
using ReadyStackGo.Domain.StackManagement.Stacks;

namespace ReadyStackGo.Application.Services;

/// <summary>
/// Parses RSGo manifest files (YAML format) - the native ReadyStackGo stack format.
/// v0.10: RSGo Manifest Format with type validation, rich metadata, and multi-stack support.
/// </summary>
public interface IRsgoManifestParser
{
    /// <summary>
    /// Parse an RSGo manifest YAML string into a structured definition.
    /// </summary>
    /// <param name="yamlContent">The YAML content of the manifest file</param>
    /// <returns>Parsed RSGo manifest</returns>
    Task<RsgoManifest> ParseAsync(string yamlContent);

    /// <summary>
    /// Parse an RSGo manifest from a file, resolving any includes.
    /// </summary>
    /// <param name="filePath">Path to the manifest file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Parsed RSGo manifest with resolved includes</returns>
    Task<RsgoManifest> ParseFromFileAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Load all products from a directory.
    /// Only files with metadata.productVersion are loaded as products.
    /// Fragments (without productVersion) are only loaded via include.
    /// </summary>
    /// <param name="directoryPath">Directory to scan for manifests</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of parsed products</returns>
    Task<List<RsgoManifest>> LoadProductsFromDirectoryAsync(string directoryPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extract all variable definitions from the manifest, including shared variables.
    /// </summary>
    /// <param name="manifest">Parsed RSGo manifest</param>
    /// <returns>List of variables with full type information</returns>
    Task<List<Variable>> ExtractVariablesAsync(RsgoManifest manifest);

    /// <summary>
    /// Extract variables for a specific stack in a multi-stack manifest.
    /// Merges shared variables with stack-specific variables.
    /// </summary>
    /// <param name="manifest">Parsed RSGo manifest</param>
    /// <param name="stackKey">Stack key in the stacks section</param>
    /// <returns>List of variables with full type information</returns>
    Task<List<Variable>> ExtractStackVariablesAsync(RsgoManifest manifest, string stackKey);

    /// <summary>
    /// Convert an RSGo manifest to a deployment plan.
    /// Environment variables are resolved using the provided values.
    /// </summary>
    /// <param name="manifest">Parsed RSGo manifest</param>
    /// <param name="resolvedVariables">Resolved environment variable values</param>
    /// <param name="stackName">Name for the deployed stack</param>
    /// <returns>Deployment plan compatible with the deployment engine</returns>
    Task<DeploymentPlan> ConvertToDeploymentPlanAsync(
        RsgoManifest manifest,
        Dictionary<string, string> resolvedVariables,
        string stackName);

    /// <summary>
    /// Convert a specific stack from a multi-stack manifest to a deployment plan.
    /// </summary>
    /// <param name="manifest">Parsed RSGo manifest</param>
    /// <param name="stackKey">Stack key in the stacks section</param>
    /// <param name="resolvedVariables">Resolved environment variable values</param>
    /// <param name="stackName">Name for the deployed stack</param>
    /// <returns>Deployment plan compatible with the deployment engine</returns>
    Task<DeploymentPlan> ConvertStackToDeploymentPlanAsync(
        RsgoManifest manifest,
        string stackKey,
        Dictionary<string, string> resolvedVariables,
        string stackName);

    /// <summary>
    /// Validate an RSGo manifest YAML file.
    /// </summary>
    /// <param name="yamlContent">The YAML content of the manifest file</param>
    /// <returns>Validation result with any errors or warnings</returns>
    Task<RsgoManifestValidationResult> ValidateAsync(string yamlContent);

    /// <summary>
    /// Validate resolved variable values against their definitions.
    /// </summary>
    /// <param name="manifest">Parsed RSGo manifest</param>
    /// <param name="values">Variable values to validate</param>
    /// <returns>Validation result with per-variable errors</returns>
    Task<VariableValidationResult> ValidateVariablesAsync(
        RsgoManifest manifest,
        Dictionary<string, string> values);

    /// <summary>
    /// Detect the manifest format from content.
    /// </summary>
    /// <param name="yamlContent">YAML content to analyze</param>
    /// <returns>Detected manifest format</returns>
    ManifestFormat DetectFormat(string yamlContent);
}

/// <summary>
/// Result of RSGo manifest validation.
/// </summary>
public class RsgoManifestValidationResult
{
    /// <summary>
    /// Whether the manifest is valid.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// List of validation errors.
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// List of validation warnings (non-blocking).
    /// </summary>
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Result of variable value validation.
/// </summary>
public class VariableValidationResult
{
    /// <summary>
    /// Whether all variables are valid.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Validation errors per variable.
    /// </summary>
    public Dictionary<string, List<string>> VariableErrors { get; set; } = new();

    /// <summary>
    /// List of missing required variables.
    /// </summary>
    public List<string> MissingRequired { get; set; } = new();
}

/// <summary>
/// Detected manifest format.
/// </summary>
public enum ManifestFormat
{
    /// <summary>
    /// Unknown format.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Docker Compose format (has 'services' at root level).
    /// </summary>
    DockerCompose = 1,

    /// <summary>
    /// RSGo manifest format (has 'version' with value starting with "rsgo" or has 'metadata' section).
    /// </summary>
    RsgoManifest = 2
}
