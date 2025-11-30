using FastEndpoints;
using FluentValidation;
using ReadyStackGo.Application.UseCases.Environments;

namespace ReadyStackGo.API.Endpoints.Environments;

/// <summary>
/// Validator for CreateEnvironmentRequest
/// </summary>
public class CreateEnvironmentValidator : Validator<CreateEnvironmentRequest>
{
    public CreateEnvironmentValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Environment ID is required")
            .MaximumLength(100).WithMessage("Environment ID must not exceed 100 characters")
            .Matches("^[a-zA-Z0-9_-]+$").WithMessage("Environment ID can only contain letters, numbers, underscores, and hyphens");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Environment name is required")
            .MaximumLength(200).WithMessage("Environment name must not exceed 200 characters");

        RuleFor(x => x.SocketPath)
            .NotEmpty().WithMessage("Socket path is required");
    }
}
