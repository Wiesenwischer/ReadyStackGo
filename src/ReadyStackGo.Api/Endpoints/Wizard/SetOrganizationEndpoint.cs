using FastEndpoints;
using MediatR;
using ReadyStackGo.Application.UseCases.Organizations.ProvisionOrganization;
using ReadyStackGo.Application.UseCases.Wizard;

namespace ReadyStackGo.API.Endpoints.Wizard;

/// <summary>
/// POST /api/wizard/organization - Step 2: Set organization
/// </summary>
public class SetOrganizationEndpoint : Endpoint<SetOrganizationRequest, SetOrganizationResponse>
{
    private readonly IMediator _mediator;

    public SetOrganizationEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("/api/wizard/organization");
        AllowAnonymous();
    }

    public override async Task HandleAsync(SetOrganizationRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new ProvisionOrganizationCommand(req.Name, req.Name),
            ct);

        if (!result.Success)
        {
            ThrowError(result.ErrorMessage ?? "Failed to create organization");
            return;
        }

        Response = new SetOrganizationResponse
        {
            Success = true,
            Message = "Organization set successfully"
        };
    }
}
