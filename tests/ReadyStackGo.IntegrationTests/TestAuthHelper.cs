using System.Net.Http.Json;

namespace ReadyStackGo.IntegrationTests;

public static class TestAuthHelper
{
    public static async Task<string> GetAdminTokenAsync(HttpClient client)
    {
        var loginRequest = new
        {
            username = "admin",
            password = "admin"
        };

        var response = await client.PostAsJsonAsync("/api/auth/login", loginRequest);
        response.EnsureSuccessStatusCode();

        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
        return loginResponse!.Token;
    }

    public static void AddAuthToken(HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    private record LoginResponse(string Token, string Username, string Role);
}
