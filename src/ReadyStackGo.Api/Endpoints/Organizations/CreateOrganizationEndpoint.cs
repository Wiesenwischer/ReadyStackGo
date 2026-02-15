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
        var sourceId = string.IsNullOrWhiteSpace(req.Id) ? req.Name : req.Id;
        var result = await _mediator.Send(
            new ProvisionOrganizationCommand(sourceId, req.Name),
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
    public string? Id { get; set; }
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
        RuleFor(x => x.Id)
            .Matches(@"^[a-zA-Z0-9_-]+$").WithMessage("Organization ID can only contain letters, numbers, underscores, and hyphens")
            .MinimumLength(2).WithMessage("Organization ID must be at least 2 characters")
            .MaximumLength(100).WithMessage("Organization ID must not exceed 100 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.Id));

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Organization name is required")
            .MaximumLength(200).WithMessage("Organization name must not exceed 200 characters");
    }
}
