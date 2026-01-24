using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ReadyStackGo.Domain.StackManagement.Sources;
using ReadyStackGo.Infrastructure.Parsing;
using ReadyStackGo.Infrastructure.Services.StackSources;

namespace ReadyStackGo.UnitTests.Services;

public class LocalDirectoryProductSourceProviderTests
{
    private readonly LocalDirectoryProductSourceProvider _provider;
    private readonly RsgoManifestParser _parser;

    public LocalDirectoryProductSourceProviderTests()
    {
        var parserLogger = new Mock<ILogger<RsgoManifestParser>>();
        var providerLogger = new Mock<ILogger<LocalDirectoryProductSourceProvider>>();

        _parser = new RsgoManifestParser(parserLogger.Object);
        _provider = new LocalDirectoryProductSourceProvider(providerLogger.Object, _parser);
    }

    [Fact]
    public async Task LoadProductsAsync_MultiStackManifestWithIncludes_LoadsServicesFromIncludeFiles()
    {
        // Arrange - Create temporary test directory structure
        var tempDir = Path.Combine(Path.GetTempPath(), $"rsgo-test-provider-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        var contextsDir = Path.Combine(tempDir, "Contexts");
        Directory.CreateDirectory(contextsDir);

        try
        {
            // Create the main manifest file
            var mainManifest = @"
metadata:
  name: Business Services
  productId: business-services
  description: Business Services - all bounded context services
  productVersion: '3.1.0-pre'
  category: Business

sharedVariables:
  REDIS_CONNECTION:
    label: Redis Connection
    type: String
    default: cachedata:6379

stacks:
  projectmanagement:
    include: Contexts/projectmanagement.yaml
  memo:
    include: Contexts/memo.yaml
";
            await File.WriteAllTextAsync(Path.Combine(tempDir, "business-services.yaml"), mainManifest);

            // Create include files
            var projectManagementFragment = @"
metadata:
  name: ProjectManagement
  description: Project Management bounded context

services:
  project-api:
    image: amssolution/project-api:latest
    containerName: project-api
    ports:
      - '7700:8080'

  project-web:
    image: amssolution/project-web:latest
    containerName: project-web
    ports:
      - '7701:3000'
";
            await File.WriteAllTextAsync(Path.Combine(contextsDir, "projectmanagement.yaml"), projectManagementFragment);

            var memoFragment = @"
metadata:
  name: Memo
  description: Memo bounded context

services:
  memo-api:
    image: amssolution/memo-api:latest
    containerName: memo-api
    ports:
      - '7702:8080'
";
            await File.WriteAllTextAsync(Path.Combine(contextsDir, "memo.yaml"), memoFragment);

            // Create StackSource
            var source = StackSource.CreateLocalDirectory(
                id: StackSourceId.Create("test-source"),
                name: "Test Source",
                path: tempDir
            );

            // Act
            var products = await _provider.LoadProductsAsync(source);
            var productsList = products.ToList();

            // Assert
            productsList.Should().HaveCount(1, "should load one product from business-services.yaml");

            var product = productsList[0];
            product.Name.Should().Be("Business Services");
            product.ProductVersion.Should().Be("3.1.0-pre");
            product.IsMultiStack.Should().BeTrue();
            product.Stacks.Should().HaveCount(2, "product should have 2 stacks");

            // Verify that services from includes are loaded
            var projectManagementStack = product.Stacks.FirstOrDefault(s => s.Name == "ProjectManagement");
            projectManagementStack.Should().NotBeNull("projectmanagement stack should exist");
            projectManagementStack!.Services.Should().HaveCount(2, "projectmanagement stack should have 2 services from include file");
            projectManagementStack.Services.Should().Contain(s => s.Name == "project-api");
            projectManagementStack.Services.Should().Contain(s => s.Name == "project-web");

            var memoStack = product.Stacks.FirstOrDefault(s => s.Name == "Memo");
            memoStack.Should().NotBeNull("memo stack should exist");
            memoStack!.Services.Should().HaveCount(1, "memo stack should have 1 service from include file");
            memoStack.Services.Should().Contain(s => s.Name == "memo-api");

            // Verify TotalServices count
            product.TotalServices.Should().Be(3, "product should have 3 total services (2 from projectmanagement + 1 from memo)");
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
