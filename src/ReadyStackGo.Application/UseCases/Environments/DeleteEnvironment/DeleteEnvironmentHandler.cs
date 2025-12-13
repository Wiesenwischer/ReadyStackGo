using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Domain.Deployment.Environments;

namespace ReadyStackGo.Application.UseCases.Environments.DeleteEnvironment;

public class DeleteEnvironmentHandler : IRequestHandler<DeleteEnvironmentCommand, DeleteEnvironmentResponse>
{
    private readonly IEnvironmentRepository _environmentRepository;
    private readonly ILogger<DeleteEnvironmentHandler> _logger;

    public DeleteEnvironmentHandler(
        IEnvironmentRepository environmentRepository,
        ILogger<DeleteEnvironmentHandler> logger)
    {
        _environmentRepository = environmentRepository;
        _logger = logger;
    }

    public Task<DeleteEnvironmentResponse> Handle(DeleteEnvironmentCommand request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Deleting environment: {Id}", request.EnvironmentId);

            if (!Guid.TryParse(request.EnvironmentId, out var guid))
            {
                return Task.FromResult(new DeleteEnvironmentResponse
                {
                    Success = false,
                    Message = "Invalid environment ID."
                });
            }

            var environment = _environmentRepository.Get(new EnvironmentId(guid));

            if (environment == null)
            {
                return Task.FromResult(new DeleteEnvironmentResponse
                {
                    Success = false,
                    Message = $"Environment '{request.EnvironmentId}' not found."
                });
            }

            if (environment.IsDefault)
            {
                return Task.FromResult(new DeleteEnvironmentResponse
                {
                    Success = false,
                    Message = "Cannot delete the default environment. Set another environment as default first."
                });
            }

            _environmentRepository.Remove(environment);
            _environmentRepository.SaveChanges();

            _logger.LogInformation("Environment deleted successfully: {Id}", request.EnvironmentId);

            return Task.FromResult(new DeleteEnvironmentResponse
            {
                Success = true,
                Message = "Environment deleted successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete environment: {Id}", request.EnvironmentId);
            return Task.FromResult(new DeleteEnvironmentResponse
            {
                Success = false,
                Message = $"Failed to delete environment: {ex.Message}"
            });
        }
    }
}
