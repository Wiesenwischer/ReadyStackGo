using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.StackManagement.Sources;

namespace ReadyStackGo.Application.UseCases.StackSources.CreateStackSource;

public class CreateStackSourceHandler : IRequestHandler<CreateStackSourceCommand, CreateStackSourceResult>
{
    private readonly IProductSourceService _productSourceService;
    private readonly ILogger<CreateStackSourceHandler> _logger;

    public CreateStackSourceHandler(
        IProductSourceService productSourceService,
        ILogger<CreateStackSourceHandler> logger)
    {
        _productSourceService = productSourceService;
        _logger = logger;
    }

    public async Task<CreateStackSourceResult> Handle(CreateStackSourceCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;

        // Validate required fields
        if (string.IsNullOrWhiteSpace(request.Id))
        {
            return new CreateStackSourceResult(false, "Source ID is required");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return new CreateStackSourceResult(false, "Source name is required");
        }

        try
        {
            var sourceId = new StackSourceId(request.Id);
            StackSource source;

            switch (request.Type?.ToLowerInvariant())
            {
                case "localdirectory":
                case "local-directory":
                    if (string.IsNullOrWhiteSpace(request.Path))
                    {
                        return new CreateStackSourceResult(false, "Path is required for local directory source");
                    }

                    source = StackSource.CreateLocalDirectory(
                        sourceId,
                        request.Name,
                        request.Path,
                        request.FilePattern ?? "*.yml;*.yaml");
                    break;

                case "gitrepository":
                case "git-repository":
                    if (string.IsNullOrWhiteSpace(request.GitUrl))
                    {
                        return new CreateStackSourceResult(false, "Git URL is required for Git repository source");
                    }

                    // Validate Git URL format
                    if (!Uri.TryCreate(request.GitUrl, UriKind.Absolute, out var uri) ||
                        (uri.Scheme != "https" && uri.Scheme != "http" && uri.Scheme != "git"))
                    {
                        return new CreateStackSourceResult(false, "Invalid Git URL format. Use https://, http://, or git:// URL");
                    }

                    source = StackSource.CreateGitRepository(
                        sourceId,
                        request.Name,
                        request.GitUrl,
                        request.Branch ?? "main",
                        request.Path,
                        request.FilePattern ?? "*.yml;*.yaml",
                        request.GitUsername,
                        request.GitPassword);
                    break;

                default:
                    return new CreateStackSourceResult(false, $"Unknown source type: {request.Type}. Use 'LocalDirectory' or 'GitRepository'");
            }

            await _productSourceService.AddSourceAsync(source, cancellationToken);

            _logger.LogInformation("Created stack source {SourceId} '{SourceName}' of type {SourceType}",
                source.Id, source.Name, source.Type);

            return new CreateStackSourceResult(true, "Stack source created successfully", source.Id.Value);
        }
        catch (InvalidOperationException ex)
        {
            return new CreateStackSourceResult(false, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create stack source '{Name}'", request.Name);
            return new CreateStackSourceResult(false, $"Failed to create stack source: {ex.Message}");
        }
    }
}
