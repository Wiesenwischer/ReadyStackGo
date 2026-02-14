using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.StackManagement.Sources;

namespace ReadyStackGo.Application.UseCases.Wizard.SetSources;

public class SetSourcesHandler : IRequestHandler<SetSourcesCommand, SetSourcesResult>
{
    private readonly ISourceRegistryService _registryService;
    private readonly IProductSourceService _productSourceService;
    private readonly ILogger<SetSourcesHandler> _logger;

    public SetSourcesHandler(
        ISourceRegistryService registryService,
        IProductSourceService productSourceService,
        ILogger<SetSourcesHandler> logger)
    {
        _registryService = registryService;
        _productSourceService = productSourceService;
        _logger = logger;
    }

    public async Task<SetSourcesResult> Handle(SetSourcesCommand command, CancellationToken cancellationToken)
    {
        if (command.RegistrySourceIds.Count == 0)
        {
            return new SetSourcesResult(true, "No sources selected", 0);
        }

        var existingSources = await _productSourceService.GetSourcesAsync(cancellationToken);
        var existingGitUrls = existingSources
            .Where(s => !string.IsNullOrEmpty(s.GitUrl))
            .Select(s => s.GitUrl!.TrimEnd('/'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingPaths = existingSources
            .Where(s => !string.IsNullOrEmpty(s.Path))
            .Select(s => s.Path!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var created = 0;

        foreach (var registryId in command.RegistrySourceIds)
        {
            var entry = _registryService.GetById(registryId);
            if (entry is null)
            {
                _logger.LogWarning("Registry entry not found: {RegistryId}", registryId);
                continue;
            }

            if (entry.IsLocalDirectory)
            {
                if (string.IsNullOrWhiteSpace(entry.Path))
                {
                    _logger.LogWarning("Skipping local registry entry with no path: {RegistryId}", registryId);
                    continue;
                }

                if (existingPaths.Contains(entry.Path))
                {
                    _logger.LogDebug("Skipping already added local source: {Path}", entry.Path);
                    continue;
                }

                var localSource = StackSource.CreateLocalDirectory(
                    StackSourceId.NewId(),
                    entry.Name,
                    entry.Path,
                    entry.FilePattern ?? "*.yml;*.yaml");

                await _productSourceService.AddSourceAsync(localSource, cancellationToken);
                existingPaths.Add(entry.Path);
                created++;

                _logger.LogInformation("Created local stack source from registry: {SourceName} ({Path})",
                    entry.Name, entry.Path);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(entry.GitUrl))
                {
                    _logger.LogWarning("Skipping git registry entry with no URL: {RegistryId}", registryId);
                    continue;
                }

                if (existingGitUrls.Contains(entry.GitUrl.TrimEnd('/')))
                {
                    _logger.LogDebug("Skipping already added source: {GitUrl}", entry.GitUrl);
                    continue;
                }

                var gitSource = StackSource.CreateGitRepository(
                    StackSourceId.NewId(),
                    entry.Name,
                    entry.GitUrl,
                    entry.GitBranch);

                await _productSourceService.AddSourceAsync(gitSource, cancellationToken);
                existingGitUrls.Add(entry.GitUrl.TrimEnd('/'));
                created++;

                _logger.LogInformation("Created stack source from registry: {SourceName} ({GitUrl})",
                    entry.Name, entry.GitUrl);
            }
        }

        return new SetSourcesResult(true, $"{created} source(s) added", created);
    }
}
