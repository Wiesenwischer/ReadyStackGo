using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.StackManagement.Sources;

namespace ReadyStackGo.Application.UseCases.StackSources.ImportSources;

public class ImportSourcesHandler : IRequestHandler<ImportSourcesCommand, ImportSourcesResult>
{
    private readonly IProductSourceService _productSourceService;
    private readonly ILogger<ImportSourcesHandler> _logger;

    public ImportSourcesHandler(
        IProductSourceService productSourceService,
        ILogger<ImportSourcesHandler> logger)
    {
        _productSourceService = productSourceService;
        _logger = logger;
    }

    public async Task<ImportSourcesResult> Handle(
        ImportSourcesCommand command,
        CancellationToken cancellationToken)
    {
        if (command.Data.Sources.Count == 0)
        {
            return new ImportSourcesResult(true, "No sources to import", 0, 0);
        }

        var existingSources = await _productSourceService.GetSourcesAsync(cancellationToken);
        var existingGitUrls = existingSources
            .Where(s => !string.IsNullOrEmpty(s.GitUrl))
            .Select(s => s.GitUrl!.TrimEnd('/'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var created = 0;
        var skipped = 0;

        foreach (var imported in command.Data.Sources)
        {
            var type = imported.Type?.ToLowerInvariant();

            if (type is "git-repository" or "gitrepository")
            {
                if (string.IsNullOrWhiteSpace(imported.GitUrl))
                {
                    _logger.LogWarning("Skipping git source '{Name}' — missing Git URL", imported.Name);
                    skipped++;
                    continue;
                }

                if (existingGitUrls.Contains(imported.GitUrl.TrimEnd('/')))
                {
                    _logger.LogDebug("Skipping duplicate git source: {GitUrl}", imported.GitUrl);
                    skipped++;
                    continue;
                }

                var source = StackSource.CreateGitRepository(
                    StackSourceId.NewId(),
                    imported.Name,
                    imported.GitUrl,
                    imported.GitBranch ?? "main",
                    sslVerify: imported.GitSslVerify ?? true);

                if (!imported.Enabled)
                    source.Disable();

                await _productSourceService.AddSourceAsync(source, cancellationToken);
                existingGitUrls.Add(imported.GitUrl.TrimEnd('/'));
                created++;
            }
            else if (type is "local-directory" or "localdirectory")
            {
                if (string.IsNullOrWhiteSpace(imported.Path))
                {
                    _logger.LogWarning("Skipping local source '{Name}' — missing path", imported.Name);
                    skipped++;
                    continue;
                }

                var source = StackSource.CreateLocalDirectory(
                    StackSourceId.NewId(),
                    imported.Name,
                    imported.Path,
                    imported.FilePattern ?? "*.yml;*.yaml");

                if (!imported.Enabled)
                    source.Disable();

                await _productSourceService.AddSourceAsync(source, cancellationToken);
                created++;
            }
            else
            {
                _logger.LogWarning("Skipping source '{Name}' — unknown type '{Type}'", imported.Name, imported.Type);
                skipped++;
            }
        }

        _logger.LogInformation("Import complete: {Created} created, {Skipped} skipped", created, skipped);

        return new ImportSourcesResult(
            Success: true,
            Message: $"{created} source(s) imported, {skipped} skipped",
            SourcesCreated: created,
            SourcesSkipped: skipped);
    }
}
