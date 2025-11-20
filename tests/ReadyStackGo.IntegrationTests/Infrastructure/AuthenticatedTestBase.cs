using System.Net.Http.Json;

namespace ReadyStackGo.IntegrationTests.Infrastructure;

/// <summary>
/// Base class for integration tests that require authentication.
/// Provides complete wizard setup and authenticated HTTP client.
/// </summary>
public abstract class AuthenticatedTestBase : IAsyncLifetime
{
    protected readonly CustomWebApplicationFactory Factory;
    protected readonly HttpClient Client;

    protected string AdminUsername { get; private set; } = string.Empty;
    protected string AdminPassword { get; private set; } = string.Empty;
    protected string OrganizationId { get; private set; } = string.Empty;
    protected string AuthToken { get; private set; } = string.Empty;

    protected AuthenticatedTestBase()
    {
        Factory = new CustomWebApplicationFactory();
        Client = Factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await CompleteWizardSetupAsync();
        await AuthenticateAsync();
        await OnInitializedAsync();
    }

    public async Task DisposeAsync()
    {
        await OnDisposingAsync();
        Client.Dispose();
        await Factory.DisposeAsync();
    }

    /// <summary>
    /// Override this to perform additional initialization after wizard and auth setup
    /// </summary>
    protected virtual Task OnInitializedAsync() => Task.CompletedTask;

    /// <summary>
    /// Override this to perform cleanup before disposal
    /// </summary>
    protected virtual Task OnDisposingAsync() => Task.CompletedTask;

    private async Task CompleteWizardSetupAsync()
    {
        // Generate unique credentials for this test run
        var testId = Guid.NewGuid().ToString("N")[..8];
        AdminUsername = $"admin_{testId}";
        AdminPassword = "TestPassword123!";
        OrganizationId = $"org-{testId}";

        // Step 1: Create admin
        var adminRequest = new
        {
            username = AdminUsername,
            password = AdminPassword
        };
        var adminResponse = await Client.PostAsJsonAsync("/api/wizard/admin", adminRequest);

        if (!adminResponse.IsSuccessStatusCode)
        {
            var content = await adminResponse.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Failed to create admin: {adminResponse.StatusCode} - {content}");
        }

        // Step 2: Set organization
        var orgRequest = new
        {
            id = OrganizationId,
            name = $"Test Organization {testId}"
        };
        var orgResponse = await Client.PostAsJsonAsync("/api/wizard/organization", orgRequest);

        if (!orgResponse.IsSuccessStatusCode)
        {
            var content = await orgResponse.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Failed to set organization: {orgResponse.StatusCode} - {content}");
        }

        // Step 3: Complete wizard (v0.4 has only 3 steps)
        var installRequest = new
        {
            manifestPath = (string?)null
        };
        var installResponse = await Client.PostAsJsonAsync("/api/wizard/install", installRequest);

        if (!installResponse.IsSuccessStatusCode)
        {
            var content = await installResponse.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Failed to complete wizard: {installResponse.StatusCode} - {content}");
        }
    }

    private async Task AuthenticateAsync()
    {
        var loginRequest = new
        {
            username = AdminUsername,
            password = AdminPassword
        };

        var response = await Client.PostAsJsonAsync("/api/auth/login", loginRequest);

        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Failed to authenticate: {response.StatusCode} - {content}");
        }

        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
        AuthToken = loginResponse?.Token ?? throw new InvalidOperationException("No token in login response");

        // Add token to default request headers
        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AuthToken);
    }

    /// <summary>
    /// Creates a new HTTP client without authentication
    /// </summary>
    protected HttpClient CreateUnauthenticatedClient()
    {
        return Factory.CreateClient();
    }

    /// <summary>
    /// Creates a new HTTP client with the test's auth token
    /// </summary>
    protected HttpClient CreateAuthenticatedClient()
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AuthToken);
        return client;
    }

    private record LoginResponse(string Token, string Username, string Role);
}
