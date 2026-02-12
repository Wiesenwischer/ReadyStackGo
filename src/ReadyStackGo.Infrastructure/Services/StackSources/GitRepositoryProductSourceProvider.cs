using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.StackManagement.Sources;
using ReadyStackGo.Domain.StackManagement.Stacks;

namespace ReadyStackGo.Infrastructure.Services.StackSources;

/// <summary>
/// Provider that loads products from a Git repository.
/// Delegates to LocalDirectoryProductSourceProvider after cloning/updating the repo.
/// </summary>
public class GitRepositoryProductSourceProvider : IProductSourceProvider
{
    private readonly ILogger<GitRepositoryProductSourceProvider> _logger;
    private readonly IRsgoManifestParser _manifestParser;
    private readonly LocalDirectoryProductSourceProvider _localDirectoryProvider;

    public string SourceType => "git-repository";

    public GitRepositoryProductSourceProvider(
        ILogger<GitRepositoryProductSourceProvider> logger,
        IRsgoManifestParser manifestParser,
        ILogger<LocalDirectoryProductSourceProvider> localDirectoryLogger)
    {
        _logger = logger;
        _manifestParser = manifestParser;
        // Reuse LocalDirectoryProvider for parsing after checkout
        _localDirectoryProvider = new LocalDirectoryProductSourceProvider(localDirectoryLogger, manifestParser);
    }

    public bool CanHandle(StackSource source)
    {
        return source.Type == StackSourceType.GitRepository;
    }

    public async Task<IEnumerable<ProductDefinition>> LoadProductsAsync(StackSource source, CancellationToken cancellationToken = default)
    {
        if (source.Type != StackSourceType.GitRepository)
        {
            throw new ArgumentException($"Expected GitRepository source but got {source.Type}");
        }

        if (!source.Enabled)
        {
            _logger.LogDebug("Product source {SourceId} is disabled, skipping", source.Id);
            return Enumerable.Empty<ProductDefinition>();
        }

        // Clone or update the repository to a local directory
        var localPath = await CloneOrUpdateRepositoryAsync(source, cancellationToken);

        if (string.IsNullOrEmpty(localPath) || !Directory.Exists(localPath))
        {
            _logger.LogWarning("Failed to clone repository {GitUrl}", source.GitUrl);
            return Enumerable.Empty<ProductDefinition>();
        }

        // Apply sub-path if specified
        var productsPath = localPath;
        if (!string.IsNullOrEmpty(source.Path))
        {
            productsPath = Path.Combine(localPath, source.Path);
        }

        // Create a temporary LocalDirectory source for parsing
        var localSource = StackSource.CreateLocalDirectory(
            source.Id,
            source.Name,
            productsPath,
            source.FilePattern ?? "*.yml;*.yaml");

        // Delegate to LocalDirectoryProvider for actual loading
        var products = await _localDirectoryProvider.LoadProductsAsync(localSource, cancellationToken);

        _logger.LogInformation("Loaded {Count} products from Git repository {GitUrl}",
            products.Count(), source.GitUrl);

        return products;
    }

    /// <summary>
    /// Clones or updates a Git repository to a local cache directory.
    /// Returns the local path where the repository was cloned.
    /// </summary>
    private async Task<string?> CloneOrUpdateRepositoryAsync(StackSource source, CancellationToken cancellationToken)
    {
        // Cache directory: ~/.rsgo/git-cache/{source-id}/
        var cacheRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".rsgo",
            "git-cache");

        var repoDir = Path.Combine(cacheRoot, source.Id.Value);

        try
        {
            Directory.CreateDirectory(cacheRoot);

            if (Directory.Exists(repoDir))
            {
                // Repository exists - do a git pull
                _logger.LogDebug("Updating existing repository at {Path}", repoDir);
                await RunGitCommandAsync(source, repoDir, ["checkout", source.GitBranch!], cancellationToken);
                await RunGitCommandAsync(source, repoDir, ["pull", "--ff-only"], cancellationToken);
            }
            else
            {
                // Clone the repository
                _logger.LogInformation("Cloning repository {GitUrl} to {Path}", source.GitUrl, repoDir);
                await RunGitCommandAsync(
                    source,
                    cacheRoot,
                    ["clone", "--branch", source.GitBranch!, "--single-branch", "--depth", "1", source.GitUrl!, source.Id.Value],
                    cancellationToken);
            }

            return repoDir;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Git operation failed for {GitUrl}", source.GitUrl);

            // If clone failed, try to use existing cached version
            if (Directory.Exists(repoDir))
            {
                _logger.LogWarning("Using cached version of repository {GitUrl}", source.GitUrl);
                return repoDir;
            }

            return null;
        }
    }

    /// <summary>
    /// Builds git -c config arguments for authentication and SSL verification.
    /// Uses http.extraHeader with Basic Auth instead of URL-embedded credentials,
    /// which works universally including TFS/Azure DevOps Server.
    /// </summary>
    private static List<string> BuildGitConfigArgs(StackSource source)
    {
        var args = new List<string>();

        if (!source.GitSslVerify)
        {
            args.Add("-c");
            args.Add("http.sslVerify=false");
        }

        if (!string.IsNullOrEmpty(source.GitUsername) && !string.IsNullOrEmpty(source.GitPassword))
        {
            var credentials = $"{source.GitUsername}:{source.GitPassword}";
            var base64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(credentials));
            args.Add("-c");
            args.Add($"http.extraHeader=Authorization: Basic {base64}");
        }

        return args;
    }

    private async Task RunGitCommandAsync(StackSource source, string workingDirectory, string[] commandArgs, CancellationToken cancellationToken)
    {
        var configArgs = BuildGitConfigArgs(source);

        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Use ArgumentList to avoid quoting issues (each arg is passed as-is)
        foreach (var arg in configArgs)
            startInfo.ArgumentList.Add(arg);
        foreach (var arg in commandArgs)
            startInfo.ArgumentList.Add(arg);

        using var process = new System.Diagnostics.Process { StartInfo = startInfo };

        // Log without credentials
        _logger.LogDebug("Running: git {Arguments} in {Directory}", string.Join(" ", commandArgs), workingDirectory);

        process.Start();

        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = await process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Git command failed: {error}");
        }

        if (!string.IsNullOrWhiteSpace(output))
        {
            _logger.LogDebug("Git output: {Output}", output.Trim());
        }
    }
}
