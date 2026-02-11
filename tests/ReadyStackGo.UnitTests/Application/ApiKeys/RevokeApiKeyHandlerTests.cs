using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ReadyStackGo.Application.UseCases.ApiKeys.RevokeApiKey;
using ReadyStackGo.Domain.IdentityAccess.ApiKeys;
using ReadyStackGo.Domain.IdentityAccess.Organizations;

namespace ReadyStackGo.UnitTests.Application.ApiKeys;

public class RevokeApiKeyHandlerTests
{
    private readonly Mock<IApiKeyRepository> _apiKeyRepositoryMock;
    private readonly Mock<ILogger<RevokeApiKeyHandler>> _loggerMock;
    private readonly RevokeApiKeyHandler _handler;

    public RevokeApiKeyHandlerTests()
    {
        _apiKeyRepositoryMock = new Mock<IApiKeyRepository>();
        _loggerMock = new Mock<ILogger<RevokeApiKeyHandler>>();
        _handler = new RevokeApiKeyHandler(
            _apiKeyRepositoryMock.Object,
            _loggerMock.Object);
    }

    private static ApiKey CreateTestApiKey(string name = "Test Key")
    {
        return ApiKey.Create(
            ApiKeyId.Create(),
            OrganizationId.Create(),
            name,
            "a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2",
            "rsgo_testkey",
            new List<string> { "Hooks.Redeploy" });
    }

    [Fact]
    public async Task Handle_ValidKey_RevokesSuccessfully()
    {
        var apiKey = CreateTestApiKey();
        _apiKeyRepositoryMock.Setup(r => r.GetById(apiKey.Id)).Returns(apiKey);

        var result = await _handler.Handle(
            new RevokeApiKeyCommand(apiKey.Id.Value.ToString(), "No longer needed"),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Message.Should().Contain("revoked successfully");
        apiKey.IsRevoked.Should().BeTrue();
        _apiKeyRepositoryMock.Verify(r => r.Update(apiKey), Times.Once);
        _apiKeyRepositoryMock.Verify(r => r.SaveChanges(), Times.Once);
    }

    [Fact]
    public async Task Handle_InvalidIdFormat_ReturnsFailure()
    {
        var result = await _handler.Handle(
            new RevokeApiKeyCommand("not-a-guid", null),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Invalid API key ID");
    }

    [Fact]
    public async Task Handle_KeyNotFound_ReturnsFailure()
    {
        var id = Guid.NewGuid();
        _apiKeyRepositoryMock.Setup(r => r.GetById(It.IsAny<ApiKeyId>())).Returns((ApiKey?)null);

        var result = await _handler.Handle(
            new RevokeApiKeyCommand(id.ToString(), null),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_AlreadyRevoked_ReturnsFailure()
    {
        var apiKey = CreateTestApiKey();
        apiKey.Revoke("First revocation");
        _apiKeyRepositoryMock.Setup(r => r.GetById(apiKey.Id)).Returns(apiKey);

        var result = await _handler.Handle(
            new RevokeApiKeyCommand(apiKey.Id.Value.ToString(), "Second attempt"),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("already revoked");
    }

    [Fact]
    public async Task Handle_WithoutReason_RevokesSuccessfully()
    {
        var apiKey = CreateTestApiKey();
        _apiKeyRepositoryMock.Setup(r => r.GetById(apiKey.Id)).Returns(apiKey);

        var result = await _handler.Handle(
            new RevokeApiKeyCommand(apiKey.Id.Value.ToString(), null),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        apiKey.IsRevoked.Should().BeTrue();
    }
}
