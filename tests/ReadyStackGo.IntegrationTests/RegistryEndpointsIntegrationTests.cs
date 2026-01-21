using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ReadyStackGo.IntegrationTests.Infrastructure;
using Xunit;

namespace ReadyStackGo.IntegrationTests;

/// <summary>
/// Integration tests for Registry API Endpoints.
/// Tests CRUD operations for Docker registry management (v0.15 feature).
/// </summary>
public class RegistryEndpointsIntegrationTests : AuthenticatedTestBase
{
    #region List Registries

    [Fact]
    public async Task GET_ListRegistries_ReturnsSuccess()
    {
        // Act
        var response = await Client.GetAsync("/api/registries");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ListRegistriesResponse>();
        result.Should().NotBeNull();
        result!.Registries.Should().NotBeNull();
    }

    [Fact]
    public async Task GET_ListRegistries_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        using var unauthenticatedClient = CreateUnauthenticatedClient();

        // Act
        var response = await unauthenticatedClient.GetAsync("/api/registries");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GET_ListRegistries_ReturnsEmptyList_WhenNoRegistriesExist()
    {
        // Act
        var response = await Client.GetAsync("/api/registries");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ListRegistriesResponse>();
        result!.Registries.Should().NotBeNull();
        // Initially no registries exist
        result.Registries.Should().BeEmpty();
    }

    #endregion

    #region Create Registry

    [Fact]
    public async Task POST_CreateRegistry_WithValidData_ReturnsCreated()
    {
        // Arrange
        var request = new
        {
            name = "Docker Hub",
            url = "https://index.docker.io/v1/",
            username = "testuser",
            password = "testpassword",
            imagePatterns = new[] { "library/*", "myrepo/*" }
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/registries", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<RegistryResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Registry.Should().NotBeNull();
        result.Registry!.Name.Should().Be("Docker Hub");
        // Domain normalizes URL by removing trailing slash
        result.Registry.Url.Should().Be("https://index.docker.io/v1");
        result.Registry.Username.Should().Be("testuser");
        result.Registry.HasCredentials.Should().BeTrue();
        result.Registry.ImagePatterns.Should().BeEquivalentTo(new[] { "library/*", "myrepo/*" });
        result.Registry.Id.Should().NotBeNullOrEmpty();
        Guid.TryParse(result.Registry.Id, out _).Should().BeTrue();
    }

    [Fact]
    public async Task POST_CreateRegistry_WithoutCredentials_ReturnsCreated()
    {
        // Arrange
        var request = new
        {
            name = "Public Registry",
            url = "https://ghcr.io"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/registries", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<RegistryResponse>();
        result!.Success.Should().BeTrue();
        result.Registry.Should().NotBeNull();
        result.Registry!.Username.Should().BeNull();
        result.Registry.HasCredentials.Should().BeFalse();
    }

    [Fact]
    public async Task POST_CreateRegistry_WithEmptyName_ReturnsBadRequest()
    {
        // Arrange
        var request = new
        {
            name = "",
            url = "https://index.docker.io/v1/"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/registries", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task POST_CreateRegistry_WithEmptyUrl_ReturnsBadRequest()
    {
        // Arrange
        var request = new
        {
            name = "Test Registry",
            url = ""
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/registries", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task POST_CreateRegistry_WithNullName_ReturnsBadRequest()
    {
        // Arrange
        var request = new
        {
            name = (string?)null,
            url = "https://index.docker.io/v1/"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/registries", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task POST_CreateRegistry_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        using var unauthenticatedClient = CreateUnauthenticatedClient();
        var request = new
        {
            name = "Test Registry",
            url = "https://test.registry.io"
        };

        // Act
        var response = await unauthenticatedClient.PostAsJsonAsync("/api/registries", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task POST_CreateRegistry_WithEmptyImagePatterns_ReturnsCreated()
    {
        // Arrange
        var request = new
        {
            name = "Registry With Empty Patterns",
            url = "https://test.registry.io",
            imagePatterns = Array.Empty<string>()
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/registries", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<RegistryResponse>();
        result!.Success.Should().BeTrue();
        result.Registry!.ImagePatterns.Should().BeEmpty();
    }

    #endregion

    #region Get Registry

    [Fact]
    public async Task GET_GetRegistry_WithValidId_ReturnsRegistry()
    {
        // Arrange
        var registryId = await CreateTestRegistry("Get Test Registry");

        // Act
        var response = await Client.GetAsync($"/api/registries/{registryId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<RegistryResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Registry.Should().NotBeNull();
        result.Registry!.Id.Should().Be(registryId);
        result.Registry.Name.Should().Be("Get Test Registry");
    }

    [Fact]
    public async Task GET_GetRegistry_WithInvalidId_ReturnsError()
    {
        // Act
        var response = await Client.GetAsync($"/api/registries/{Guid.NewGuid()}");

        // Assert
        // Could be 404 or 200 with Success=false
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var result = await response.Content.ReadFromJsonAsync<RegistryResponse>();
            result!.Success.Should().BeFalse();
        }
        else
        {
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
    }

    [Fact]
    public async Task GET_GetRegistry_WithInvalidGuidFormat_ReturnsBadRequest()
    {
        // Act
        var response = await Client.GetAsync("/api/registries/not-a-guid");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GET_GetRegistry_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var registryId = await CreateTestRegistry("Auth Test Registry");
        using var unauthenticatedClient = CreateUnauthenticatedClient();

        // Act
        var response = await unauthenticatedClient.GetAsync($"/api/registries/{registryId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Update Registry

    [Fact]
    public async Task PUT_UpdateRegistry_WithValidData_ReturnsSuccess()
    {
        // Arrange
        var registryId = await CreateTestRegistry("Original Name");
        var request = new
        {
            name = "Updated Name",
            url = "https://updated.registry.io"
        };

        // Act
        var response = await Client.PutAsJsonAsync($"/api/registries/{registryId}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<RegistryResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Registry!.Name.Should().Be("Updated Name");
        result.Registry.Url.Should().Be("https://updated.registry.io");
        result.Registry.Id.Should().Be(registryId);
    }

    [Fact]
    public async Task PUT_UpdateRegistry_UpdateCredentials_ReturnsSuccess()
    {
        // Arrange
        var registryId = await CreateTestRegistry("Cred Test Registry");
        var request = new
        {
            username = "newuser",
            password = "newpassword"
        };

        // Act
        var response = await Client.PutAsJsonAsync($"/api/registries/{registryId}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<RegistryResponse>();
        result!.Success.Should().BeTrue();
        result.Registry!.Username.Should().Be("newuser");
        result.Registry.HasCredentials.Should().BeTrue();
    }

    [Fact]
    public async Task PUT_UpdateRegistry_ClearCredentials_RemovesCredentials()
    {
        // Arrange
        var registryId = await CreateTestRegistryWithCredentials("Clear Cred Registry");
        var request = new
        {
            clearCredentials = true
        };

        // Act
        var response = await Client.PutAsJsonAsync($"/api/registries/{registryId}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<RegistryResponse>();
        result!.Success.Should().BeTrue();
        result.Registry!.Username.Should().BeNull();
        result.Registry.HasCredentials.Should().BeFalse();
    }

    [Fact]
    public async Task PUT_UpdateRegistry_UpdateImagePatterns_ReturnsSuccess()
    {
        // Arrange
        var registryId = await CreateTestRegistry("Pattern Test Registry");
        var request = new
        {
            imagePatterns = new[] { "newpattern/*", "another/**" }
        };

        // Act
        var response = await Client.PutAsJsonAsync($"/api/registries/{registryId}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<RegistryResponse>();
        result!.Success.Should().BeTrue();
        result.Registry!.ImagePatterns.Should().BeEquivalentTo(new[] { "newpattern/*", "another/**" });
    }

    [Fact]
    public async Task PUT_UpdateRegistry_WithInvalidId_ReturnsError()
    {
        // Arrange
        var request = new
        {
            name = "Updated Name"
        };

        // Act
        var response = await Client.PutAsJsonAsync($"/api/registries/{Guid.NewGuid()}", request);

        // Assert
        var result = await response.Content.ReadFromJsonAsync<RegistryResponse>();
        result!.Success.Should().BeFalse();
    }

    [Fact]
    public async Task PUT_UpdateRegistry_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var registryId = await CreateTestRegistry("No Auth Update Registry");
        using var unauthenticatedClient = CreateUnauthenticatedClient();
        var request = new
        {
            name = "Hacked Name"
        };

        // Act
        var response = await unauthenticatedClient.PutAsJsonAsync($"/api/registries/{registryId}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PUT_UpdateRegistry_PartialUpdate_PreservesOtherFields()
    {
        // Arrange
        var createRequest = new
        {
            name = "Partial Update Registry",
            url = "https://original.registry.io",
            username = "originaluser",
            password = "originalpass",
            imagePatterns = new[] { "original/*" }
        };
        var createResponse = await Client.PostAsJsonAsync("/api/registries", createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<RegistryResponse>();
        var registryId = created!.Registry!.Id;

        // Act - only update name
        var updateRequest = new
        {
            name = "New Name Only"
        };
        var response = await Client.PutAsJsonAsync($"/api/registries/{registryId}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<RegistryResponse>();
        result!.Success.Should().BeTrue();
        result.Registry!.Name.Should().Be("New Name Only");
        result.Registry.Url.Should().Be("https://original.registry.io");
        result.Registry.Username.Should().Be("originaluser");
        result.Registry.HasCredentials.Should().BeTrue();
        result.Registry.ImagePatterns.Should().BeEquivalentTo(new[] { "original/*" });
    }

    #endregion

    #region Delete Registry

    [Fact]
    public async Task DELETE_DeleteRegistry_WithValidId_ReturnsSuccess()
    {
        // Arrange
        var registryId = await CreateTestRegistry("Delete Test Registry");

        // Act
        var response = await Client.DeleteAsync($"/api/registries/{registryId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<RegistryResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();

        // Verify it's actually deleted
        var getResponse = await Client.GetAsync($"/api/registries/{registryId}");
        if (getResponse.StatusCode == HttpStatusCode.OK)
        {
            var getResult = await getResponse.Content.ReadFromJsonAsync<RegistryResponse>();
            getResult!.Success.Should().BeFalse();
        }
    }

    [Fact]
    public async Task DELETE_DeleteRegistry_WithInvalidId_ReturnsError()
    {
        // Act
        var response = await Client.DeleteAsync($"/api/registries/{Guid.NewGuid()}");

        // Assert
        var result = await response.Content.ReadFromJsonAsync<RegistryResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeFalse();
    }

    [Fact]
    public async Task DELETE_DeleteRegistry_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var registryId = await CreateTestRegistry("No Auth Delete Registry");
        using var unauthenticatedClient = CreateUnauthenticatedClient();

        // Act
        var response = await unauthenticatedClient.DeleteAsync($"/api/registries/{registryId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DELETE_DeleteRegistry_TwiceSameId_SecondCallFails()
    {
        // Arrange
        var registryId = await CreateTestRegistry("Double Delete Registry");

        // Act
        var firstResponse = await Client.DeleteAsync($"/api/registries/{registryId}");
        var secondResponse = await Client.DeleteAsync($"/api/registries/{registryId}");

        // Assert
        var firstResult = await firstResponse.Content.ReadFromJsonAsync<RegistryResponse>();
        firstResult!.Success.Should().BeTrue();

        var secondResult = await secondResponse.Content.ReadFromJsonAsync<RegistryResponse>();
        secondResult!.Success.Should().BeFalse();
    }

    #endregion

    #region Set Default Registry

    [Fact]
    public async Task POST_SetDefaultRegistry_WithValidId_ReturnsSuccess()
    {
        // Arrange
        var registryId = await CreateTestRegistry("Default Test Registry");

        // Act
        var response = await Client.PostAsync($"/api/registries/{registryId}/default", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<RegistryResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Registry!.IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task POST_SetDefaultRegistry_ChangesDefault_FromPreviousRegistry()
    {
        // Arrange
        var firstRegistryId = await CreateTestRegistry("First Default Registry");
        var secondRegistryId = await CreateTestRegistry("Second Default Registry");

        // Set first as default
        await Client.PostAsync($"/api/registries/{firstRegistryId}/default", null);

        // Act - Set second as default
        var response = await Client.PostAsync($"/api/registries/{secondRegistryId}/default", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify first is no longer default
        var firstResponse = await Client.GetAsync($"/api/registries/{firstRegistryId}");
        var firstResult = await firstResponse.Content.ReadFromJsonAsync<RegistryResponse>();
        firstResult!.Registry!.IsDefault.Should().BeFalse();

        // Verify second is now default
        var secondResponse = await Client.GetAsync($"/api/registries/{secondRegistryId}");
        var secondResult = await secondResponse.Content.ReadFromJsonAsync<RegistryResponse>();
        secondResult!.Registry!.IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task POST_SetDefaultRegistry_WithInvalidId_ReturnsError()
    {
        // Act
        var response = await Client.PostAsync($"/api/registries/{Guid.NewGuid()}/default", null);

        // Assert
        var result = await response.Content.ReadFromJsonAsync<RegistryResponse>();
        result!.Success.Should().BeFalse();
    }

    [Fact]
    public async Task POST_SetDefaultRegistry_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var registryId = await CreateTestRegistry("No Auth Default Registry");
        using var unauthenticatedClient = CreateUnauthenticatedClient();

        // Act
        var response = await unauthenticatedClient.PostAsync($"/api/registries/{registryId}/default", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task POST_SetDefaultRegistry_AlreadyDefault_ReturnsSuccess()
    {
        // Arrange
        var registryId = await CreateTestRegistry("Already Default Registry");
        await Client.PostAsync($"/api/registries/{registryId}/default", null);

        // Act - Set same registry as default again
        var response = await Client.PostAsync($"/api/registries/{registryId}/default", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<RegistryResponse>();
        result!.Success.Should().BeTrue();
        result.Registry!.IsDefault.Should().BeTrue();
    }

    #endregion

    #region Complete CRUD Flow

    [Fact]
    public async Task RegistryFlow_CompleteCRUD_WorksCorrectly()
    {
        // Step 1: List registries (should be empty or have existing)
        var initialListResponse = await Client.GetAsync("/api/registries");
        initialListResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var initialList = await initialListResponse.Content.ReadFromJsonAsync<ListRegistriesResponse>();
        var initialCount = initialList!.Registries.Count;

        // Step 2: Create a registry
        var createRequest = new
        {
            name = "CRUD Flow Registry",
            url = "https://flow.registry.io",
            username = "flowuser",
            password = "flowpass",
            imagePatterns = new[] { "flow/*", "app/**" }
        };
        var createResponse = await Client.PostAsJsonAsync("/api/registries", createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await createResponse.Content.ReadFromJsonAsync<RegistryResponse>();
        created!.Success.Should().BeTrue();
        var registryId = created.Registry!.Id;

        // Step 3: Verify it appears in list
        var listResponse = await Client.GetAsync("/api/registries");
        var list = await listResponse.Content.ReadFromJsonAsync<ListRegistriesResponse>();
        list!.Registries.Count.Should().Be(initialCount + 1);
        list.Registries.Should().Contain(r => r.Id == registryId);

        // Step 4: Get the registry
        var getResponse = await Client.GetAsync($"/api/registries/{registryId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var getResult = await getResponse.Content.ReadFromJsonAsync<RegistryResponse>();
        getResult!.Registry!.Name.Should().Be("CRUD Flow Registry");

        // Step 5: Update the registry
        var updateRequest = new
        {
            name = "Updated CRUD Registry",
            imagePatterns = new[] { "updated/*" }
        };
        var updateResponse = await Client.PutAsJsonAsync($"/api/registries/{registryId}", updateRequest);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResponse.Content.ReadFromJsonAsync<RegistryResponse>();
        updated!.Registry!.Name.Should().Be("Updated CRUD Registry");
        updated.Registry.ImagePatterns.Should().BeEquivalentTo(new[] { "updated/*" });

        // Step 6: Set as default
        var defaultResponse = await Client.PostAsync($"/api/registries/{registryId}/default", null);
        defaultResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var defaultResult = await defaultResponse.Content.ReadFromJsonAsync<RegistryResponse>();
        defaultResult!.Registry!.IsDefault.Should().BeTrue();

        // Step 7: Delete the registry
        var deleteResponse = await Client.DeleteAsync($"/api/registries/{registryId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var deleteResult = await deleteResponse.Content.ReadFromJsonAsync<RegistryResponse>();
        deleteResult!.Success.Should().BeTrue();

        // Step 8: Verify it's gone
        var finalListResponse = await Client.GetAsync("/api/registries");
        var finalList = await finalListResponse.Content.ReadFromJsonAsync<ListRegistriesResponse>();
        finalList!.Registries.Should().NotContain(r => r.Id == registryId);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task CreateRegistry_WithSpecialCharactersInName_ReturnsCreated()
    {
        // Arrange
        var request = new
        {
            name = "Registry (Test) - Prod & Dev <v1>",
            url = "https://special.registry.io"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/registries", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<RegistryResponse>();
        result!.Registry!.Name.Should().Be("Registry (Test) - Prod & Dev <v1>");
    }

    [Fact]
    public async Task CreateRegistry_WithLongUrl_ReturnsCreated()
    {
        // Arrange
        var request = new
        {
            name = "Long URL Registry",
            url = "https://very-long-registry-hostname.example.com/with/a/very/long/path/segment/that/goes/on/and/on"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/registries", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateRegistry_WithManyImagePatterns_ReturnsCreated()
    {
        // Arrange
        var patterns = Enumerable.Range(1, 20).Select(i => $"pattern{i}/*").ToArray();
        var request = new
        {
            name = "Many Patterns Registry",
            url = "https://patterns.registry.io",
            imagePatterns = patterns
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/registries", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<RegistryResponse>();
        result!.Registry!.ImagePatterns.Should().HaveCount(20);
    }

    [Fact]
    public async Task CreateMultipleRegistries_AllAppearInList()
    {
        // Arrange
        var initialList = await Client.GetAsync("/api/registries");
        var initial = await initialList.Content.ReadFromJsonAsync<ListRegistriesResponse>();
        var initialCount = initial!.Registries.Count;

        // Act - Create 3 registries
        for (int i = 1; i <= 3; i++)
        {
            var request = new
            {
                name = $"Multi Registry {i}",
                url = $"https://multi{i}.registry.io"
            };
            var response = await Client.PostAsJsonAsync("/api/registries", request);
            response.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        // Assert
        var finalList = await Client.GetAsync("/api/registries");
        var final = await finalList.Content.ReadFromJsonAsync<ListRegistriesResponse>();
        final!.Registries.Count.Should().Be(initialCount + 3);
    }

    #endregion

    #region Helper Methods

    private async Task<string> CreateTestRegistry(string name)
    {
        var request = new
        {
            name = name,
            url = $"https://{name.ToLowerInvariant().Replace(" ", "-")}.registry.io"
        };

        var response = await Client.PostAsJsonAsync("/api/registries", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created,
            $"Failed to create test registry '{name}': {await response.Content.ReadAsStringAsync()}");

        var result = await response.Content.ReadFromJsonAsync<RegistryResponse>();
        if (result?.Registry?.Id == null)
        {
            throw new InvalidOperationException($"Failed to create test registry: {result?.Message}");
        }

        return result.Registry.Id;
    }

    private async Task<string> CreateTestRegistryWithCredentials(string name)
    {
        var request = new
        {
            name = name,
            url = $"https://{name.ToLowerInvariant().Replace(" ", "-")}.registry.io",
            username = "testuser",
            password = "testpassword"
        };

        var response = await Client.PostAsJsonAsync("/api/registries", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<RegistryResponse>();
        return result!.Registry!.Id;
    }

    #endregion

    #region Response DTOs

    private record RegistryDto(
        string Id,
        string Name,
        string Url,
        string? Username,
        bool HasCredentials,
        bool IsDefault,
        IReadOnlyList<string> ImagePatterns,
        DateTime CreatedAt,
        DateTime? UpdatedAt);

    private record RegistryResponse(
        bool Success,
        string? Message = null,
        RegistryDto? Registry = null);

    private record ListRegistriesResponse(
        IReadOnlyList<RegistryDto> Registries);

    #endregion
}
