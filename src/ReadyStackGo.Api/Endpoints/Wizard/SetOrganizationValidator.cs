using FastEndpoints;
using FluentValidation;
using ReadyStackGo.Application.UseCases.Wizard;

namespace ReadyStackGo.API.Endpoints.Wizard;

/// <summary>
/// Validator for SetOrganizationRequest
/// </summary>
public class SetOrganizationValidator : Validator<SetOrganizationRequest>
{
    public SetOrganizationValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Organization ID is required")
            .MaximumLength(100).WithMessage("Organization ID must not exceed 100 characters")
            .Matches("^[a-zA-Z0-9_-]+$").WithMessage("Organization ID can only contain letters, numbers, underscores, and hyphens");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Organization name is required")
            .MaximumLength(200).WithMessage("Organization name must not exceed 200 characters");
    }
}
