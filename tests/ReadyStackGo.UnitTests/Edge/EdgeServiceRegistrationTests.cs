using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.Services.Edge;
using ReadyStackGo.Application.Services.Impl;
using ReadyStackGo.Domain.Deployment.ProductDeployments;
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
}
