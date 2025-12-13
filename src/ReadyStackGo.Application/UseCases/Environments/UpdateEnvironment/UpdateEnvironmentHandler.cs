using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Domain.Deployment.Environments;

namespace ReadyStackGo.Application.UseCases.Environments.UpdateEnvironment;

public class UpdateEnvironmentHandler : IRequestHandler<UpdateEnvironmentCommand, UpdateEnvironmentResponse>
{
    private readonly IEnvironmentRepository _environmentRepository;
    private readonly ILogger<UpdateEnvironmentHandler> _logger;

    public UpdateEnvironmentHandler(
        IEnvironmentRepository environmentRepository,
        ILogger<UpdateEnvironmentHandler> logger)
    {
        _environmentRepository = environmentRepository;
        _logger = logger;
    }

    public Task<UpdateEnvironmentResponse> Handle(UpdateEnvironmentCommand request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Updating environment: {Id}", request.EnvironmentId);

            if (!Guid.TryParse(request.EnvironmentId, out var guid))
            {
                return Task.FromResult(new UpdateEnvironmentResponse
                {
                    Success = false,
                    Message = "Invalid environment ID."
                });
            }

            var environment = _environmentRepository.Get(new EnvironmentId(guid));

            if (environment == null)
            {
                return Task.FromResult(new UpdateEnvironmentResponse
                {
                    Success = false,
                    Message = $"Environment '{request.EnvironmentId}' not found."
                });
            }

            // Check for duplicate name (exclude current environment)
            var existingByName = _environmentRepository.GetByName(environment.OrganizationId, request.Name);
            if (existingByName != null && existingByName.Id != environment.Id)
            {
                return Task.FromResult(new UpdateEnvironmentResponse
                {
                    Success = false,
                    Message = $"Environment with name '{request.Name}' already exists."
                });
            }

            // Update via domain methods
            environment.UpdateName(request.Name);
            environment.UpdateConnectionConfig(ConnectionConfig.DockerSocket(request.SocketPath));

            _environmentRepository.Update(environment);
            _environmentRepository.SaveChanges();

            _logger.LogInformation("Environment updated successfully: {Id}", request.EnvironmentId);

            return Task.FromResult(new UpdateEnvironmentResponse
            {
                Success = true,
                Message = "Environment updated successfully",
                Environment = EnvironmentMapper.ToResponse(environment)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update environment: {Id}", request.EnvironmentId);
            return Task.FromResult(new UpdateEnvironmentResponse
            {
                Success = false,
                Message = $"Failed to update environment: {ex.Message}"
            });
        }
    }
}
