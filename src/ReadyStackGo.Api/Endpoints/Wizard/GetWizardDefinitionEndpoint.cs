using FastEndpoints;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.API.Endpoints.Wizard;

/// <summary>
/// GET /api/wizard/definition - Get the setup wizard step definitions for the current distribution
/// </summary>
public class GetWizardDefinitionEndpoint : EndpointWithoutRequest<WizardDefinitionResponse>
{
    private readonly ISetupWizardDefinitionProvider _provider;

    public GetWizardDefinitionEndpoint(ISetupWizardDefinitionProvider provider)
    {
        _provider = provider;
    }

    public override void Configure()
    {
        Get("/api/wizard/definition");
        Description(b => b.WithTags("Wizard"));
    }

    public override Task HandleAsync(CancellationToken ct)
    {
        var definition = _provider.GetDefinition();

        Response = new WizardDefinitionResponse
        {
            DistributionId = definition.Id,
            Steps = definition.Steps
                .OrderBy(s => s.Order)
                .Select(s => new WizardStepDto
                {
                    Id = s.Id,
                    Title = s.Title,
                    Description = s.Description,
                    ComponentType = s.ComponentType,
                    Required = s.Required,
                    Order = s.Order
                })
                .ToList()
        };

        return Task.CompletedTask;
    }
}

public class WizardDefinitionResponse
{
    public required string DistributionId { get; set; }
    public required List<WizardStepDto> Steps { get; set; }
}

public class WizardStepDto
{
    public required string Id { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public required string ComponentType { get; set; }
    public bool Required { get; set; }
    public int Order { get; set; }
}
