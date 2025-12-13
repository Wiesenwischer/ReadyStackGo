using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Domain.Deployment.Environments;

namespace ReadyStackGo.Application.UseCases.Environments.SetDefaultEnvironment;

public class SetDefaultEnvironmentHandler : IRequestHandler<SetDefaultEnvironmentCommand, SetDefaultEnvironmentResponse>
{
    private readonly IEnvironmentRepository _environmentRepository;
    private readonly ILogger<SetDefaultEnvironmentHandler> _logger;

    public SetDefaultEnvironmentHandler(
        IEnvironmentRepository environmentRepository,
        ILogger<SetDefaultEnvironmentHandler> logger)
    {
        _environmentRepository = environmentRepository;
        _logger = logger;
    }

    public Task<SetDefaultEnvironmentResponse> Handle(SetDefaultEnvironmentCommand request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Setting default environment: {Id}", request.EnvironmentId);

            if (!Guid.TryParse(request.EnvironmentId, out var guid))
            {
                return Task.FromResult(new SetDefaultEnvironmentResponse
                {
                    Success = false,
                    Message = "Invalid environment ID."
                });
            }

            var environment = _environmentRepository.Get(new EnvironmentId(guid));

            if (environment == null)
            {
                return Task.FromResult(new SetDefaultEnvironmentResponse
                {
                    Success = false,
                    Message = $"Environment '{request.EnvironmentId}' not found."
                });
            }

            // Unset current default
            var currentDefault = _environmentRepository.GetDefault(environment.OrganizationId);
            if (currentDefault != null && currentDefault.Id != environment.Id)
            {
                currentDefault.UnsetAsDefault();
                _environmentRepository.Update(currentDefault);
            }

            // Set new default via domain method
            environment.SetAsDefault();
            _environmentRepository.Update(environment);
            _environmentRepository.SaveChanges();

            _logger.LogInformation("Default environment set successfully: {Id}", request.EnvironmentId);

            return Task.FromResult(new SetDefaultEnvironmentResponse
            {
                Success = true,
                Message = "Default environment set successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set default environment: {Id}", request.EnvironmentId);
            return Task.FromResult(new SetDefaultEnvironmentResponse
            {
                Success = false,
                Message = $"Failed to set default environment: {ex.Message}"
            });
        }
    }
}
