using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Domain.IdentityAccess.ApiKeys;

namespace ReadyStackGo.Application.UseCases.ApiKeys.RevokeApiKey;

public record RevokeApiKeyCommand(string ApiKeyId, string? Reason) : IRequest<RevokeApiKeyResponse>;

public class RevokeApiKeyHandler : IRequestHandler<RevokeApiKeyCommand, RevokeApiKeyResponse>
{
    private readonly IApiKeyRepository _apiKeyRepository;
    private readonly ILogger<RevokeApiKeyHandler> _logger;

    public RevokeApiKeyHandler(
        IApiKeyRepository apiKeyRepository,
        ILogger<RevokeApiKeyHandler> logger)
    {
        _apiKeyRepository = apiKeyRepository;
        _logger = logger;
    }

    public Task<RevokeApiKeyResponse> Handle(RevokeApiKeyCommand command, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(command.ApiKeyId, out var apiKeyGuid))
        {
            return Task.FromResult(new RevokeApiKeyResponse(false, "Invalid API key ID format."));
        }

        var apiKey = _apiKeyRepository.GetById(new ApiKeyId(apiKeyGuid));
        if (apiKey == null)
        {
            return Task.FromResult(new RevokeApiKeyResponse(false, "API key not found."));
        }

        if (apiKey.IsRevoked)
        {
            return Task.FromResult(new RevokeApiKeyResponse(false, "API key is already revoked."));
        }

        try
        {
            apiKey.Revoke(command.Reason);
            _apiKeyRepository.Update(apiKey);
            _apiKeyRepository.SaveChanges();

            _logger.LogInformation("Revoked API key '{Name}' ({Id})", apiKey.Name, command.ApiKeyId);

            return Task.FromResult(new RevokeApiKeyResponse(true, $"API key '{apiKey.Name}' revoked successfully."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to revoke API key {Id}", command.ApiKeyId);
            return Task.FromResult(new RevokeApiKeyResponse(false, $"Failed to revoke API key: {ex.Message}"));
        }
    }
}
