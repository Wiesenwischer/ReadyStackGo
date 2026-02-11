using System.Security.Cryptography;
using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Domain.IdentityAccess.ApiKeys;
using ReadyStackGo.Domain.IdentityAccess.Organizations;

namespace ReadyStackGo.Application.UseCases.ApiKeys.CreateApiKey;

public record CreateApiKeyCommand(CreateApiKeyRequest Request) : IRequest<CreateApiKeyResponse>;

public class CreateApiKeyHandler : IRequestHandler<CreateApiKeyCommand, CreateApiKeyResponse>
{
    private readonly IApiKeyRepository _apiKeyRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ILogger<CreateApiKeyHandler> _logger;

    private const string KeyPrefix = "rsgo_";
    private const int RandomPartLength = 32;
    private const string AlphanumericChars = "abcdefghijklmnopqrstuvwxyz0123456789";

    public CreateApiKeyHandler(
        IApiKeyRepository apiKeyRepository,
        IOrganizationRepository organizationRepository,
        ILogger<CreateApiKeyHandler> logger)
    {
        _apiKeyRepository = apiKeyRepository;
        _organizationRepository = organizationRepository;
        _logger = logger;
    }

    public Task<CreateApiKeyResponse> Handle(CreateApiKeyCommand command, CancellationToken cancellationToken)
    {
        var organization = _organizationRepository.GetAll().FirstOrDefault();
        if (organization == null)
        {
            return Task.FromResult(new CreateApiKeyResponse(false, "Organization not set."));
        }

        var request = command.Request;

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Task.FromResult(new CreateApiKeyResponse(false, "API key name is required."));
        }

        if (request.Permissions.Count == 0)
        {
            return Task.FromResult(new CreateApiKeyResponse(false, "At least one permission is required."));
        }

        // Check for duplicate name within organization
        var existingKeys = _apiKeyRepository.GetByOrganization(organization.Id);
        if (existingKeys.Any(k => k.Name == request.Name && !k.IsRevoked))
        {
            return Task.FromResult(new CreateApiKeyResponse(false, $"An active API key with name '{request.Name}' already exists."));
        }

        Guid? environmentId = null;
        if (!string.IsNullOrEmpty(request.EnvironmentId))
        {
            if (!Guid.TryParse(request.EnvironmentId, out var envGuid))
            {
                return Task.FromResult(new CreateApiKeyResponse(false, "Invalid environment ID format."));
            }
            environmentId = envGuid;
        }

        try
        {
            // Generate the raw key: rsgo_ + 32 random alphanumeric characters
            var rawKey = GenerateRawKey();
            var keyHash = ComputeSha256Hash(rawKey);
            var displayPrefix = rawKey[..12];

            var apiKeyId = ApiKeyId.Create();
            var apiKey = ApiKey.Create(
                apiKeyId,
                organization.Id,
                request.Name,
                keyHash,
                displayPrefix,
                request.Permissions,
                environmentId,
                request.ExpiresAt);

            _apiKeyRepository.Add(apiKey);
            _apiKeyRepository.SaveChanges();

            _logger.LogInformation("Created API key '{Name}' (prefix: {Prefix}) for organization {OrgId}",
                request.Name, displayPrefix, organization.Id.Value);

            var dto = new ApiKeyCreatedDto(
                Id: apiKey.Id.Value.ToString(),
                Name: apiKey.Name,
                KeyPrefix: apiKey.KeyPrefix,
                FullKey: rawKey);

            return Task.FromResult(new CreateApiKeyResponse(true, "API key created successfully.", dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create API key '{Name}'", request.Name);
            return Task.FromResult(new CreateApiKeyResponse(false, $"Failed to create API key: {ex.Message}"));
        }
    }

    internal static string GenerateRawKey()
    {
        var randomPart = new char[RandomPartLength];
        for (var i = 0; i < RandomPartLength; i++)
        {
            randomPart[i] = AlphanumericChars[RandomNumberGenerator.GetInt32(AlphanumericChars.Length)];
        }
        return KeyPrefix + new string(randomPart);
    }

    internal static string ComputeSha256Hash(string rawKey)
    {
        var bytes = global::System.Security.Cryptography.SHA256.HashData(
            global::System.Text.Encoding.UTF8.GetBytes(rawKey));
        return Convert.ToHexStringLower(bytes);
    }
}
