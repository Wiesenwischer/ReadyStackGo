using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.Services.Edge;
using ReadyStackGo.Application.Services.Impl;
using ReadyStackGo.Domain.Deployment.ProductDeployments;
using ReadyStackGo.Infrastructure;
using ReadyStackGo.Infrastructure.Configuration;
using ReadyStackGo.Infrastructure.Services.Edge;
using Xunit;

namespace ReadyStackGo.UnitTests.Edge;

/// <summary>
/// Guards the edge services' DI lifetimes against captive dependencies (e.g. a singleton
/// consuming the scoped IDockerService). Builds the real edge registrations with scope
/// validation + validate-on-build — exactly the check that fails at app startup if a lifetime
/// is wrong. Regression guard for the edge provisioner lifetime bug.
/// </summary>
public class EdgeServiceRegistrationTests
{
    [Fact]
    public void EdgeServices_ResolveWithScopeValidation()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpClient(); // provides IHttpClientFactory for the admin client

        // External dependencies with their REAL lifetimes (matches production registration).
        services.AddScoped(_ => new Mock<IDockerService>().Object);             // scoped (the trap)
        services.AddScoped(_ => new Mock<IProductDeploymentRepository>().Object); // scoped
        services.AddSingleton(_ => new Mock<IConfigStore>().Object);            // singleton
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton(new SniRouterOptions());

        // The actual edge registrations under test (mirror DependencyInjection.cs).
        services.AddScoped<IEdgeProvisioner, EdgeProvisioner>();
        services.AddSingleton<ICaddyAdminClient, CaddyAdminClient>();
        services.AddSingleton<IEdgeCertificateProvider, EdgeCertificateProvider>();
        services.AddSingleton<IEdgeBundleReader, EdgeBundleReader>();
        services.AddSingleton<IEdgeConfigCache, EdgeConfigCache>();
        services.AddScoped<IEdgeReconciler, EdgeReconciler>();
        services.AddScoped<ISniRouterReconciler, SniRouterReconciler>();

        // ValidateOnBuild + ValidateScopes throw on a captive dependency (singleton -> scoped).
        var ex = Record.Exception(() =>
        {
            using var provider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateScopes = true,
                ValidateOnBuild = true
            });
            using var scope = provider.CreateScope();
            _ = scope.ServiceProvider.GetRequiredService<IEdgeReconciler>();
            _ = scope.ServiceProvider.GetRequiredService<ISniRouterReconciler>();
        });

        Assert.Null(ex);
    }

    /// <summary>
    /// Regression guard for #446: every HTTP client whose target is reachable directly — a
    /// product container on the internal Docker network (edge admin, HTTP observer/setter, HTTP
    /// health checks) or the PRTG server on the customer LAN — must bypass a forward proxy
    /// (<c>HTTP_PROXY</c>/<c>HTTPS_PROXY</c>). Otherwise, in proxied environments, the call is
    /// routed to the proxy and fails: the edge admin <c>/load</c> POST then strands the edge on
    /// its holding page (the maintenance page is shown forever), and health/observer/setter/PRTG
    /// all break the same way.
    /// </summary>
    [Theory]
    [InlineData(CaddyAdminClient.HttpClientName)]
    [InlineData("MaintenanceObserver")]
    [InlineData("MaintenanceSetter")]
    [InlineData("IHttpHealthChecker")] // typed client logical name = typeof(IHttpHealthChecker).Name
    [InlineData("PrtgApiVerifyTls")]
    [InlineData("PrtgApiNoVerifyTls")]
    public void InternalHttpClients_BypassForwardProxy(string clientName)
    {
        var services = new ServiceCollection();
        services.AddInternalHttpClients();
        using var provider = services.BuildServiceProvider();

        var factory = provider.GetRequiredService<IHttpMessageHandlerFactory>();
        using var handler = factory.CreateHandler(clientName);

        var primary = PrimaryHandler(handler);
        var httpClientHandler = Assert.IsType<HttpClientHandler>(primary);
        Assert.False(httpClientHandler.UseProxy,
            $"internal/LAN client '{clientName}' must not use a forward proxy (#446)");
    }

    private static HttpMessageHandler PrimaryHandler(HttpMessageHandler handler)
    {
        var current = handler;
        while (current is DelegatingHandler delegating && delegating.InnerHandler is not null)
            current = delegating.InnerHandler;
        return current;
    }
}
