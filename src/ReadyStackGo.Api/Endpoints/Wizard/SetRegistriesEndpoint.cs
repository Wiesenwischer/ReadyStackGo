using FastEndpoints;
using MediatR;
using ReadyStackGo.Application.UseCases.Wizard.SetRegistries;

namespace ReadyStackGo.API.Endpoints.Wizard;

/// <summary>
/// POST /api/wizard/registries - Bulk create registry configurations from wizard input.
/// Anonymous access (wizard runs before login).
/// </summary>
public class SetRegistriesEndpoint : Endpoint<SetRegistriesRequest, SetRegistriesResponse>
{
    private readonly IMediator _mediator;

    public SetRegistriesEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("/api/wizard/registries");
        AllowAnonymous();
        PreProcessor<WizardTimeoutPreProcessor<SetRegistriesRequest>>();
    }

    public override async Task HandleAsync(SetRegistriesRequest req, CancellationToken ct)
    {
        var command = new SetRegistriesCommand(
            req.Registries.Select(r => new RegistryInput(
                Name: r.Name,
                Host: r.Host,
                Pattern: r.Pattern,
                RequiresAuth: r.RequiresAuth,
                Username: r.Username,
                Password: r.Password
            )).ToList());

        var result = await _mediator.Send(command, ct);

        Response = new SetRegistriesResponse
        {
            Success = result.Success,
            RegistriesCreated = result.RegistriesCreated,
            RegistriesSkipped = result.RegistriesSkipped
        };
    }
}

public class SetRegistriesRequest
{
    public IReadOnlyList<RegistryInputDto> Registries { get; init; } = [];
}

public class RegistryInputDto
{
    public required string Name { get; init; }
    public required string Host { get; init; }
    public required string Pattern { get; init; }
    public bool RequiresAuth { get; init; }
    public string? Username { get; init; }
    public string? Password { get; init; }
}

public class SetRegistriesResponse
{
    public bool Success { get; init; }
    public int RegistriesCreated { get; init; }
    public int RegistriesSkipped { get; init; }
}
