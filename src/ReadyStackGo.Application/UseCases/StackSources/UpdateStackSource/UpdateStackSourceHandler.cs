using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Domain.StackManagement.Sources;

namespace ReadyStackGo.Application.UseCases.StackSources.UpdateStackSource;

public class UpdateStackSourceHandler : IRequestHandler<UpdateStackSourceCommand, UpdateStackSourceResult>
{
    private readonly IStackSourceRepository _repository;
    private readonly ILogger<UpdateStackSourceHandler> _logger;

    public UpdateStackSourceHandler(
        IStackSourceRepository repository,
        ILogger<UpdateStackSourceHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<UpdateStackSourceResult> Handle(UpdateStackSourceCommand command, CancellationToken cancellationToken)
    {
        var sourceId = new StackSourceId(command.Id);
        var source = await _repository.GetByIdAsync(sourceId, cancellationToken);

        if (source == null)
        {
            return new UpdateStackSourceResult(false, "Stack source not found");
        }

        try
        {
            var request = command.Request;
            var hasChanges = false;

            // Update name if provided
            if (!string.IsNullOrWhiteSpace(request.Name) && request.Name != source.Name)
            {
                source.UpdateName(request.Name);
                hasChanges = true;
            }

            // Update enabled status if provided
            if (request.Enabled.HasValue)
            {
                if (request.Enabled.Value && !source.Enabled)
                {
                    source.Enable();
                    hasChanges = true;
                }
                else if (!request.Enabled.Value && source.Enabled)
                {
                    source.Disable();
                    hasChanges = true;
                }
            }

            if (hasChanges)
            {
                await _repository.UpdateAsync(source, cancellationToken);
                await _repository.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Updated stack source {SourceId} '{SourceName}'", sourceId, source.Name);
            }

            return new UpdateStackSourceResult(true, "Stack source updated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update stack source '{Id}'", command.Id);
            return new UpdateStackSourceResult(false, $"Failed to update stack source: {ex.Message}");
        }
    }
}
