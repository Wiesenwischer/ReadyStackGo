using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Application.UseCases.StackSources.DeleteStackSource;

public class DeleteStackSourceHandler : IRequestHandler<DeleteStackSourceCommand, DeleteStackSourceResult>
{
    private readonly IProductSourceService _productSourceService;
    private readonly ILogger<DeleteStackSourceHandler> _logger;

    public DeleteStackSourceHandler(
        IProductSourceService productSourceService,
        ILogger<DeleteStackSourceHandler> logger)
    {
        _productSourceService = productSourceService;
        _logger = logger;
    }

    public async Task<DeleteStackSourceResult> Handle(DeleteStackSourceCommand command, CancellationToken cancellationToken)
    {
        try
        {
            await _productSourceService.RemoveSourceAsync(command.Id, cancellationToken);

            _logger.LogInformation("Deleted stack source {SourceId}", command.Id);

            return new DeleteStackSourceResult(true, "Stack source deleted successfully");
        }
        catch (InvalidOperationException ex)
        {
            return new DeleteStackSourceResult(false, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete stack source '{Id}'", command.Id);
            return new DeleteStackSourceResult(false, $"Failed to delete stack source: {ex.Message}");
        }
    }
}
