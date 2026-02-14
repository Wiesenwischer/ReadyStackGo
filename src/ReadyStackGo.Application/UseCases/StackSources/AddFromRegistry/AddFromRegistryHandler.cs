using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.StackManagement.Sources;

namespace ReadyStackGo.Application.UseCases.StackSources.AddFromRegistry;

public class AddFromRegistryHandler : IRequestHandler<AddFromRegistryCommand, AddFromRegistryResult>
{
    private readonly ISourceRegistryService _registryService;
    private readonly IProductSourceService _productSourceService;
    private readonly ILogger<AddFromRegistryHandler> _logger;

    public AddFromRegistryHandler(
        ISourceRegistryService registryService,
        IProductSourceService productSourceService,
        ILogger<AddFromRegistryHandler> logger)
    {
        _registryService = registryService;
        _productSourceService = productSourceService;
        _logger = logger;
    }

    public async Task<AddFromRegistryResult> Handle(
        AddFromRegistryCommand command,
        CancellationToken cancellationToken)
    {
        var entry = _registryService.GetById(command.RegistrySourceId);
        if (entry is null)
        {
            return new AddFromRegistryResult(false, $"Registry source '{command.RegistrySourceId}' not found");
        }

        var existingSources = await _productSourceService.GetSourcesAsync(cancellationToken);

        StackSource source;

        if (entry.IsLocalDirectory)
        {
            if (string.IsNullOrWhiteSpace(entry.Path))
                return new AddFromRegistryResult(false, "Registry entry has no path configured");

            var pathAlreadyExists = existingSources.Any(s =>
                !string.IsNullOrEmpty(s.Path) &&
                s.Path.Equals(entry.Path, StringComparison.OrdinalIgnoreCase));

            if (pathAlreadyExists)
                return new AddFromRegistryResult(false, $"A source with path '{entry.Path}' already exists");

            source = StackSource.CreateLocalDirectory(
                StackSourceId.NewId(),
                entry.Name,
                entry.Path,
                entry.FilePattern ?? "*.yml;*.yaml");

            _logger.LogInformation("Created local stack source from registry: {SourceName} ({Path})",
                entry.Name, entry.Path);
        }
        else
        {
            var gitAlreadyExists = existingSources.Any(s =>
                !string.IsNullOrEmpty(s.GitUrl) &&
                s.GitUrl.TrimEnd('/').Equals(entry.GitUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase));

            if (gitAlreadyExists)
                return new AddFromRegistryResult(false, $"A source with URL '{entry.GitUrl}' already exists");

            source = StackSource.CreateGitRepository(
                StackSourceId.NewId(),
                entry.Name,
                entry.GitUrl,
                entry.GitBranch);

            _logger.LogInformation("Created stack source from registry: {SourceName} ({GitUrl})",
                entry.Name, entry.GitUrl);
        }

        await _productSourceService.AddSourceAsync(source, cancellationToken);

        return new AddFromRegistryResult(true, "Source added successfully", source.Id.Value);
    }
}
