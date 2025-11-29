using FastEndpoints;
using FluentValidation;
using ReadyStackGo.Application.Wizard.DTOs;

namespace ReadyStackGo.API.Endpoints.Wizard;

/// <summary>
/// Validator for CreateAdminRequest
/// </summary>
public class CreateAdminValidator : Validator<CreateAdminRequest>
{
    public CreateAdminValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Username is required")
            .MinimumLength(3).WithMessage("Username must be at least 3 characters")
            .MaximumLength(50).WithMessage("Username must not exceed 50 characters")
            .Matches("^[a-zA-Z0-9_]+$").WithMessage("Username can only contain letters, numbers, and underscores");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters")
            .MaximumLength(100).WithMessage("Password must not exceed 100 characters");
    }
}
