using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Organizations.ProvisionOrganization;

namespace ReadyStackGo.API.Endpoints.Organizations;

/// <summary>
/// POST /api/organizations - Create an organization (authenticated).
/// Used by the onboarding checklist after admin creation.
/// </summary>
[RequireSystemAdmin]
public class CreateOrganizationEndpoint : Endpoint<CreateOrganizationRequest, CreateOrganizationResponse>
{
    private readonly IMediator _mediator;

    public CreateOrganizationEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("/api/organizations");
        Description(b => b.WithTags("Organizations"));
        PreProcessor<RbacPreProcessor<CreateOrganizationRequest>>();
    }

    public override async Task HandleAsync(CreateOrganizationRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new ProvisionOrganizationCommand(req.Name, req.Name),
            ct);

        if (!result.Success)
        {
            ThrowError(result.ErrorMessage ?? "Failed to create organization");
            return;
        }

        HttpContext.Response.StatusCode = StatusCodes.Status201Created;
        Response = new CreateOrganizationResponse
        {
            Success = true,
            OrganizationId = result.OrganizationId
        };
    }
}

public class CreateOrganizationRequest
{
    public required string Name { get; set; }
}

public class CreateOrganizationResponse
{
    public bool Success { get; set; }
    public string? OrganizationId { get; set; }
}

public class CreateOrganizationValidator : Validator<CreateOrganizationRequest>
{
    public CreateOrganizationValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Organization name is required")
            .MaximumLength(200).WithMessage("Organization name must not exceed 200 characters");
    }
}
