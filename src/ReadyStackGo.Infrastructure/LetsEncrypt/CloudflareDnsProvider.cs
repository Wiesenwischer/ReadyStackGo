using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Infrastructure.Configuration;

namespace ReadyStackGo.Infrastructure.LetsEncrypt;

/// <summary>
/// Cloudflare DNS provider for automated DNS-01 challenges
/// </summary>
public class CloudflareDnsProvider : IDnsProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CloudflareDnsProvider> _logger;
    private readonly string? _zoneId;

    private const string CloudflareApiBase = "https://api.cloudflare.com/client/v4";

    public DnsProviderType ProviderType => DnsProviderType.Cloudflare;

    public CloudflareDnsProvider(
        HttpClient httpClient,
        ILogger<CloudflareDnsProvider> logger,
        string apiToken,
        string? zoneId = null)
    {
        _httpClient = httpClient;
        _logger = logger;
        _zoneId = zoneId;

        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", apiToken);
    }

    public async Task<string> CreateTxtRecordAsync(string domain, string value, CancellationToken cancellationToken = default)
    {
        var zoneId = _zoneId ?? await GetZoneIdForDomainAsync(domain, cancellationToken);

        var request = new CloudflareCreateRecordRequest
        {
            Type = "TXT",
            Name = domain,
            Content = value,
            Ttl = 120
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"{CloudflareApiBase}/zones/{zoneId}/dns_records",
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Failed to create Cloudflare TXT record: {error}");
        }

        var result = await response.Content.ReadFromJsonAsync<CloudflareResponse<CloudflareDnsRecord>>(cancellationToken);

        var recordId = result?.Result?.Id ?? throw new InvalidOperationException("Failed to get record ID from Cloudflare response");

        _logger.LogInformation("Created Cloudflare TXT record {RecordId} for {Domain}", recordId, domain);

        return recordId;
    }

    public async Task DeleteTxtRecordAsync(string domain, string recordId, CancellationToken cancellationToken = default)
    {
        var zoneId = _zoneId ?? await GetZoneIdForDomainAsync(domain, cancellationToken);

        var response = await _httpClient.DeleteAsync(
            $"{CloudflareApiBase}/zones/{zoneId}/dns_records/{recordId}",
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Failed to delete Cloudflare TXT record {RecordId}", recordId);
            return;
        }

        _logger.LogInformation("Deleted Cloudflare TXT record {RecordId}", recordId);
    }

    public async Task<DnsProviderValidationResult> ValidateConfigurationAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"{CloudflareApiBase}/user/tokens/verify",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return DnsProviderValidationResult.Error("Invalid Cloudflare API token");
            }

            var result = await response.Content.ReadFromJsonAsync<CloudflareResponse<CloudflareTokenVerify>>(cancellationToken);

            if (result?.Success != true)
            {
                return DnsProviderValidationResult.Error("Cloudflare API token verification failed");
            }

            return DnsProviderValidationResult.Success();
        }
        catch (Exception ex)
        {
            return DnsProviderValidationResult.Error($"Failed to validate Cloudflare configuration: {ex.Message}");
        }
    }

    private async Task<string> GetZoneIdForDomainAsync(string domain, CancellationToken cancellationToken)
    {
        // Extract base domain from _acme-challenge.subdomain.example.com -> example.com
        var cleanDomain = domain.Replace("_acme-challenge.", "");
        var parts = cleanDomain.Split('.');

        // Try progressively larger domain parts to find the zone
        for (int i = 0; i < parts.Length - 1; i++)
        {
            var baseDomain = string.Join(".", parts.Skip(i));

            var response = await _httpClient.GetAsync(
                $"{CloudflareApiBase}/zones?name={baseDomain}",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
                continue;

            var result = await response.Content.ReadFromJsonAsync<CloudflareResponse<List<CloudflareZone>>>(cancellationToken);

            if (result?.Result?.Count > 0)
            {
                _logger.LogDebug("Found Cloudflare zone {ZoneId} for domain {Domain}", result.Result[0].Id, baseDomain);
                return result.Result[0].Id;
            }
        }

        throw new InvalidOperationException($"Could not find Cloudflare zone for domain {domain}");
    }
}

#region Cloudflare API Models

internal class CloudflareResponse<T>
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("result")]
    public T? Result { get; set; }

    [JsonPropertyName("errors")]
    public List<CloudflareError>? Errors { get; set; }
}

internal class CloudflareError
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

internal class CloudflareZone
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

internal class CloudflareDnsRecord
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
}

internal class CloudflareCreateRecordRequest
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("ttl")]
    public int Ttl { get; set; }
}

internal class CloudflareTokenVerify
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
}

#endregion
