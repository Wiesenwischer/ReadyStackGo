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
        var alreadyExists = existingSources.Any(s =>
            !string.IsNullOrEmpty(s.GitUrl) &&
            s.GitUrl.TrimEnd('/').Equals(entry.GitUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase));

        if (alreadyExists)
        {
            return new AddFromRegistryResult(false, $"A source with URL '{entry.GitUrl}' already exists");
        }

        var source = StackSource.CreateGitRepository(
            StackSourceId.NewId(),
            entry.Name,
            entry.GitUrl,
            entry.GitBranch);

        await _productSourceService.AddSourceAsync(source, cancellationToken);

        _logger.LogInformation("Created stack source from registry: {SourceName} ({GitUrl})",
            entry.Name, entry.GitUrl);

        return new AddFromRegistryResult(true, "Source added successfully", source.Id.Value);
    }
}
