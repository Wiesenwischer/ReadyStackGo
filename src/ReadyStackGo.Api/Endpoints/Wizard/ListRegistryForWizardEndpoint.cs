using FastEndpoints;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.API.Endpoints.Wizard;

/// <summary>
/// GET /api/wizard/registry - List available registry sources during wizard setup.
/// Anonymous access (wizard runs before login).
/// </summary>
public class ListRegistryForWizardEndpoint : EndpointWithoutRequest<IEnumerable<WizardRegistrySourceDto>>
{
    private readonly ISourceRegistryService _registryService;

    public ListRegistryForWizardEndpoint(ISourceRegistryService registryService)
    {
        _registryService = registryService;
    }

    public override void Configure()
    {
        Get("/api/wizard/registry");
        AllowAnonymous();
    }

    public override Task HandleAsync(CancellationToken ct)
    {
        var entries = _registryService.GetAll();

        Response = entries.Select(e => new WizardRegistrySourceDto
        {
            Id = e.Id,
            Name = e.Name,
            Description = e.Description,
            Type = e.Type,
            Category = e.Category,
            Tags = e.Tags,
            Featured = e.Featured,
            StackCount = e.StackCount
        });

        return Task.CompletedTask;
    }
}

public class WizardRegistrySourceDto
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public string Type { get; init; } = "git-repository";
    public required string Category { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
    public bool Featured { get; init; }
    public int StackCount { get; init; }
}
