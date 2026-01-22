using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Infrastructure.Configuration;

namespace ReadyStackGo.Infrastructure.LetsEncrypt;

/// <summary>
/// Factory for creating DNS providers based on configuration
/// </summary>
public interface IDnsProviderFactory
{
    IDnsProvider Create(DnsProviderConfig? config);
}

public class DnsProviderFactory : IDnsProviderFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILoggerFactory _loggerFactory;

    public DnsProviderFactory(
        IServiceProvider serviceProvider,
        IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory)
    {
        _serviceProvider = serviceProvider;
        _httpClientFactory = httpClientFactory;
        _loggerFactory = loggerFactory;
    }

    public IDnsProvider Create(DnsProviderConfig? config)
    {
        var providerType = config?.Type ?? DnsProviderType.Manual;

        return providerType switch
        {
            DnsProviderType.Manual => _serviceProvider.GetRequiredService<ManualDnsProvider>(),

            DnsProviderType.Cloudflare => CreateCloudflareProvider(config!),

            _ => throw new ArgumentException($"Unknown DNS provider type: {providerType}")
        };
    }

    private CloudflareDnsProvider CreateCloudflareProvider(DnsProviderConfig config)
    {
        if (string.IsNullOrEmpty(config.CloudflareApiToken))
        {
            throw new InvalidOperationException("Cloudflare API token is required");
        }

        var httpClient = _httpClientFactory.CreateClient("Cloudflare");
        var logger = _loggerFactory.CreateLogger<CloudflareDnsProvider>();

        return new CloudflareDnsProvider(
            httpClient,
            logger,
            config.CloudflareApiToken,
            config.CloudflareZoneId);
    }
}
