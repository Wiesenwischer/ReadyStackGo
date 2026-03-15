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
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Environment name is required")
            .MaximumLength(200).WithMessage("Environment name must not exceed 200 characters");

        RuleFor(x => x.Type)
            .Must(t => t is "DockerSocket" or "SshTunnel")
            .WithMessage("Type must be 'DockerSocket' or 'SshTunnel'");

        // DockerSocket: require socketPath
        RuleFor(x => x.SocketPath)
            .NotEmpty().WithMessage("Socket path is required")
            .When(x => x.Type != "SshTunnel");

        // SshTunnel: require SSH fields
        RuleFor(x => x.SshHost)
            .NotEmpty().WithMessage("SSH host is required")
            .When(x => x.Type == "SshTunnel");

        RuleFor(x => x.SshUsername)
            .NotEmpty().WithMessage("SSH username is required")
            .When(x => x.Type == "SshTunnel");

        RuleFor(x => x.SshSecret)
            .NotEmpty().WithMessage("SSH credential (password or private key) is required")
            .When(x => x.Type == "SshTunnel");

        RuleFor(x => x.SshPort)
            .InclusiveBetween(1, 65535).WithMessage("SSH port must be between 1 and 65535")
            .When(x => x.Type == "SshTunnel" && x.SshPort.HasValue);
    }
}
