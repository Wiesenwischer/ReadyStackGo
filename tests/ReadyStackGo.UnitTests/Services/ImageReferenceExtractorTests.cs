using FluentAssertions;
using ReadyStackGo.Infrastructure.Services;

namespace ReadyStackGo.UnitTests.Services;

public class ImageReferenceExtractorTests
{
    private readonly ImageReferenceExtractor _extractor = new();

    #region Parse - Docker Hub (default host)

    [Fact]
    public void Parse_SimpleImage_ReturnsDockerHubLibrary()
    {
        var result = _extractor.Parse("nginx");

        result.Host.Should().Be("docker.io");
        result.Namespace.Should().Be("library");
        result.Repository.Should().Be("nginx");
        result.Tag.Should().BeNull();
    }

    [Fact]
    public void Parse_SimpleImageWithTag_ExtractsTag()
    {
        var result = _extractor.Parse("nginx:latest");

        result.Host.Should().Be("docker.io");
        result.Namespace.Should().Be("library");
        result.Repository.Should().Be("nginx");
        result.Tag.Should().Be("latest");
    }

    [Fact]
    public void Parse_UserImage_ReturnsDockerHubWithNamespace()
    {
        var result = _extractor.Parse("amssolution/ams-api:0.5.0");

        result.Host.Should().Be("docker.io");
        result.Namespace.Should().Be("amssolution");
        result.Repository.Should().Be("ams-api");
        result.Tag.Should().Be("0.5.0");
    }

    [Fact]
    public void Parse_UserImageWithoutTag_NoTag()
    {
        var result = _extractor.Parse("amssolution/ams-api");

        result.Host.Should().Be("docker.io");
        result.Namespace.Should().Be("amssolution");
        result.Repository.Should().Be("ams-api");
        result.Tag.Should().BeNull();
    }

    #endregion

    #region Parse - Explicit Docker Hub hosts

    [Theory]
    [InlineData("docker.io/amssolution/ams-api:1.0")]
    [InlineData("index.docker.io/amssolution/ams-api:1.0")]
    [InlineData("registry-1.docker.io/amssolution/ams-api:1.0")]
    [InlineData("registry.hub.docker.com/amssolution/ams-api:1.0")]
    public void Parse_ExplicitDockerHubHosts_NormalizedToDockerIo(string imageRef)
    {
        var result = _extractor.Parse(imageRef);

        result.Host.Should().Be("docker.io");
        result.Namespace.Should().Be("amssolution");
        result.Repository.Should().Be("ams-api");
        result.Tag.Should().Be("1.0");
    }

    #endregion

    #region Parse - Custom registries

    [Fact]
    public void Parse_GhcrImage_ExtractsHostAndNamespace()
    {
        var result = _extractor.Parse("ghcr.io/wiesenwischer/ams-project-api:0.5.0");

        result.Host.Should().Be("ghcr.io");
        result.Namespace.Should().Be("wiesenwischer");
        result.Repository.Should().Be("ams-project-api");
        result.Tag.Should().Be("0.5.0");
    }

    [Fact]
    public void Parse_CustomRegistryWithPort_ExtractsHostWithPort()
    {
        var result = _extractor.Parse("registry.example.com:5000/myorg/myimage:v1");

        result.Host.Should().Be("registry.example.com:5000");
        result.Namespace.Should().Be("myorg");
        result.Repository.Should().Be("myimage");
        result.Tag.Should().Be("v1");
    }

    [Fact]
    public void Parse_CustomRegistryNoNamespace_UsesLibrary()
    {
        var result = _extractor.Parse("ghcr.io/myimage:v1");

        result.Host.Should().Be("ghcr.io");
        result.Namespace.Should().Be("library");
        result.Repository.Should().Be("myimage");
        result.Tag.Should().Be("v1");
    }

    [Fact]
    public void Parse_GitLabRegistry_ExtractsCorrectly()
    {
        var result = _extractor.Parse("registry.gitlab.com/mygroup/myproject:latest");

        result.Host.Should().Be("registry.gitlab.com");
        result.Namespace.Should().Be("mygroup");
        result.Repository.Should().Be("myproject");
        result.Tag.Should().Be("latest");
    }

    [Fact]
    public void Parse_QuayRegistry_ExtractsCorrectly()
    {
        var result = _extractor.Parse("quay.io/coreos/etcd:v3.5.0");

        result.Host.Should().Be("quay.io");
        result.Namespace.Should().Be("coreos");
        result.Repository.Should().Be("etcd");
        result.Tag.Should().Be("v3.5.0");
    }

    [Fact]
    public void Parse_DeepNestedPath_ExtractsFirstAsNamespace()
    {
        var result = _extractor.Parse("ghcr.io/org/sub/project:v1");

        result.Host.Should().Be("ghcr.io");
        result.Namespace.Should().Be("org");
        result.Repository.Should().Be("sub/project");
        result.Tag.Should().Be("v1");
    }

    #endregion

    #region Parse - Digest references

    [Fact]
    public void Parse_DigestReference_IgnoresDigest()
    {
        var result = _extractor.Parse("nginx@sha256:abc123def456");

        result.Host.Should().Be("docker.io");
        result.Namespace.Should().Be("library");
        result.Repository.Should().Be("nginx");
        result.Tag.Should().BeNull();
    }

    [Fact]
    public void Parse_TagAndDigest_ExtractsTagIgnoresDigest()
    {
        var result = _extractor.Parse("nginx:1.25@sha256:abc123def456");

        result.Host.Should().Be("docker.io");
        result.Namespace.Should().Be("library");
        result.Repository.Should().Be("nginx");
        result.Tag.Should().Be("1.25");
    }

    #endregion

    #region Parse - Variable references

    [Fact]
    public void Parse_VariableTag_ExtractsVariableAsTag()
    {
        var result = _extractor.Parse("amssolution/ams-api:${VERSION}");

        result.Host.Should().Be("docker.io");
        result.Namespace.Should().Be("amssolution");
        result.Repository.Should().Be("ams-api");
        result.Tag.Should().Be("${VERSION}");
    }

    [Fact]
    public void Parse_VariableInImage_ExtractsAsIs()
    {
        var result = _extractor.Parse("${REGISTRY}/myimage:latest");

        // Variable substitution not resolved — parse as-is
        result.OriginalReference.Should().Be("${REGISTRY}/myimage:latest");
    }

    #endregion

    #region Parse - Edge cases

    [Fact]
    public void Parse_EmptyString_ReturnsDefaults()
    {
        var result = _extractor.Parse("");

        result.Host.Should().Be("docker.io");
        result.Namespace.Should().Be("library");
        result.Repository.Should().BeEmpty();
    }

    [Fact]
    public void Parse_WhitespaceString_ReturnsDefaults()
    {
        var result = _extractor.Parse("   ");

        result.Host.Should().Be("docker.io");
        result.Namespace.Should().Be("library");
        result.Repository.Should().BeEmpty();
    }

    [Fact]
    public void Parse_PreservesOriginalReference()
    {
        var result = _extractor.Parse("  ghcr.io/org/image:v1  ");

        result.OriginalReference.Should().Be("ghcr.io/org/image:v1");
    }

    [Fact]
    public void Parse_LocalhostWithPort_ExtractsCorrectly()
    {
        var result = _extractor.Parse("localhost:5000/myapp:dev");

        result.Host.Should().Be("localhost:5000");
        result.Namespace.Should().Be("library");
        result.Repository.Should().Be("myapp");
        result.Tag.Should().Be("dev");
    }

    #endregion

    #region GroupByRegistryArea - Basic grouping

    [Fact]
    public void GroupByRegistryArea_EmptyList_ReturnsEmpty()
    {
        var result = _extractor.GroupByRegistryArea([]);

        result.Should().BeEmpty();
    }

    [Fact]
    public void GroupByRegistryArea_SingleImage_ReturnsSingleArea()
    {
        var result = _extractor.GroupByRegistryArea(["nginx:latest"]);

        result.Should().HaveCount(1);
        result[0].Host.Should().Be("docker.io");
        result[0].Namespace.Should().Be("library");
        result[0].Images.Should().ContainSingle().Which.Should().Be("nginx:latest");
    }

    [Fact]
    public void GroupByRegistryArea_SameNamespace_GroupedTogether()
    {
        var images = new[]
        {
            "amssolution/ams-api:0.5.0",
            "amssolution/ams-worker:0.5.0",
            "amssolution/ams-web:latest"
        };

        var result = _extractor.GroupByRegistryArea(images);

        result.Should().HaveCount(1);
        result[0].Namespace.Should().Be("amssolution");
        result[0].Images.Should().HaveCount(3);
    }

    [Fact]
    public void GroupByRegistryArea_DifferentNamespaces_SeparateAreas()
    {
        var images = new[]
        {
            "amssolution/ams-api:0.5.0",
            "nginx:latest",
            "ghcr.io/wiesenwischer/app:1.0"
        };

        var result = _extractor.GroupByRegistryArea(images);

        result.Should().HaveCount(3);
        result.Select(a => a.Namespace).Should()
            .Contain("amssolution")
            .And.Contain("library")
            .And.Contain("wiesenwischer");
    }

    [Fact]
    public void GroupByRegistryArea_DuplicateImages_Deduplicated()
    {
        var images = new[]
        {
            "nginx:latest",
            "nginx:latest",
            "nginx:1.25"
        };

        var result = _extractor.GroupByRegistryArea(images);

        result.Should().HaveCount(1);
        result[0].Images.Should().HaveCount(2);
    }

    [Fact]
    public void GroupByRegistryArea_CaseInsensitiveGrouping()
    {
        var images = new[]
        {
            "Amssolution/ams-api:1.0",
            "amssolution/ams-worker:1.0"
        };

        var result = _extractor.GroupByRegistryArea(images);

        result.Should().HaveCount(1);
        result[0].Images.Should().HaveCount(2);
    }

    #endregion

    #region GroupByRegistryArea - Suggested patterns

    [Fact]
    public void GroupByRegistryArea_DockerHub_SuggestsNamespacePattern()
    {
        var result = _extractor.GroupByRegistryArea(["amssolution/ams-api:1.0"]);

        result[0].SuggestedPattern.Should().Be("amssolution/*");
    }

    [Fact]
    public void GroupByRegistryArea_DockerHubLibrary_SuggestsLibraryPattern()
    {
        var result = _extractor.GroupByRegistryArea(["nginx:latest"]);

        result[0].SuggestedPattern.Should().Be("library/*");
    }

    [Fact]
    public void GroupByRegistryArea_CustomRegistry_IncludesHostInPattern()
    {
        var result = _extractor.GroupByRegistryArea(["ghcr.io/myorg/app:v1"]);

        result[0].SuggestedPattern.Should().Be("ghcr.io/myorg/*");
    }

    #endregion

    #region GroupByRegistryArea - Suggested names

    [Fact]
    public void GroupByRegistryArea_DockerHubUser_NameIncludesNamespace()
    {
        var result = _extractor.GroupByRegistryArea(["amssolution/ams-api:1.0"]);

        result[0].SuggestedName.Should().Be("Docker Hub – amssolution");
    }

    [Fact]
    public void GroupByRegistryArea_DockerHubLibrary_NameIsOfficialImages()
    {
        var result = _extractor.GroupByRegistryArea(["nginx:latest"]);

        result[0].SuggestedName.Should().Be("Docker Hub (Official Images)");
    }

    [Fact]
    public void GroupByRegistryArea_CustomRegistry_NameIncludesHost()
    {
        var result = _extractor.GroupByRegistryArea(["ghcr.io/myorg/app:v1"]);

        result[0].SuggestedName.Should().Be("ghcr.io – myorg");
    }

    #endregion

    #region GroupByRegistryArea - IsLikelyPublic

    [Fact]
    public void GroupByRegistryArea_DockerHubLibrary_IsLikelyPublic()
    {
        var result = _extractor.GroupByRegistryArea(["nginx:latest", "redis:7", "postgres:16"]);

        result[0].IsLikelyPublic.Should().BeTrue();
    }

    [Fact]
    public void GroupByRegistryArea_DockerHubUser_IsLikelyPublic()
    {
        var result = _extractor.GroupByRegistryArea(["amssolution/ams-api:1.0"]);

        result[0].IsLikelyPublic.Should().BeTrue("Docker Hub images are mostly public");
    }

    [Theory]
    [InlineData("ghcr.io/myorg/app:v1", "GitHub Container Registry")]
    [InlineData("quay.io/coreos/etcd:v3", "Quay.io")]
    [InlineData("registry.gitlab.com/mygroup/myproject:latest", "GitLab Registry")]
    public void GroupByRegistryArea_MostlyPublicRegistries_IsLikelyPublic(string image, string reason)
    {
        var result = _extractor.GroupByRegistryArea([image]);

        result[0].IsLikelyPublic.Should().BeTrue($"{reason} is a mostly-public registry");
    }

    [Theory]
    [InlineData("mcr.microsoft.com/dotnet/aspnet:9.0", "Microsoft Container Registry")]
    [InlineData("registry.k8s.io/kube-apiserver:v1.30", "Kubernetes Registry")]
    [InlineData("public.ecr.aws/lambda/python:3.12", "AWS Public ECR")]
    [InlineData("lscr.io/linuxserver/nginx:latest", "LinuxServer.io")]
    public void GroupByRegistryArea_AlwaysPublicRegistries_IsLikelyPublic(string image, string reason)
    {
        var result = _extractor.GroupByRegistryArea([image]);

        result[0].IsLikelyPublic.Should().BeTrue($"{reason} is an always-public registry");
    }

    [Fact]
    public void GroupByRegistryArea_UnknownPrivateRegistry_NotLikelyPublic()
    {
        var result = _extractor.GroupByRegistryArea(["registry.internal.corp:5000/myteam/app:v1"]);

        result[0].IsLikelyPublic.Should().BeFalse("unknown registries default to private");
    }

    #endregion

    #region GroupByRegistryArea - Variable filtering

    [Fact]
    public void GroupByRegistryArea_VariableInTag_FilteredOut()
    {
        var result = _extractor.GroupByRegistryArea(["wordpress:${WORDPRESS_VERSION}"]);

        result.Should().BeEmpty("images with unresolved variables should be filtered out");
    }

    [Fact]
    public void GroupByRegistryArea_VariableInHost_FilteredOut()
    {
        var result = _extractor.GroupByRegistryArea(["${REGISTRY}/myimage:latest"]);

        result.Should().BeEmpty("images with unresolved variables should be filtered out");
    }

    [Fact]
    public void GroupByRegistryArea_MalformedVariable_FilteredOut()
    {
        var result = _extractor.GroupByRegistryArea(["{$REGISTRY}/myimage:latest"]);

        result.Should().BeEmpty("images with malformed variable syntax should be filtered out");
    }

    [Fact]
    public void GroupByRegistryArea_VariableWithDefault_FilteredOut()
    {
        var result = _extractor.GroupByRegistryArea(["myapp:${VERSION:-latest}"]);

        result.Should().BeEmpty("images with unresolved variable defaults should be filtered out");
    }

    [Fact]
    public void GroupByRegistryArea_MixedResolvedAndUnresolved_OnlyResolvedKept()
    {
        var images = new[]
        {
            "nginx:latest",
            "wordpress:${WORDPRESS_VERSION}",
            "redis:7",
            "${REGISTRY}/api:${VERSION}"
        };

        var result = _extractor.GroupByRegistryArea(images);

        result.Should().HaveCount(1, "only the resolved images should remain");
        result[0].Images.Should().HaveCount(2);
        result[0].Images.Should().Contain("nginx:latest");
        result[0].Images.Should().Contain("redis:7");
    }

    [Fact]
    public void GroupByRegistryArea_AllVariables_ReturnsEmpty()
    {
        var images = new[]
        {
            "wordpress:${WORDPRESS_VERSION}",
            "mysql:${MYSQL_VERSION}",
            "${REGISTRY}/api:${VERSION}"
        };

        var result = _extractor.GroupByRegistryArea(images);

        result.Should().BeEmpty("all images contain unresolved variables");
    }

    #endregion

    #region GroupByRegistryArea - Edge cases

    [Fact]
    public void GroupByRegistryArea_NullAndEmptyStrings_Ignored()
    {
        var images = new[] { "", "  ", "nginx:latest" };

        var result = _extractor.GroupByRegistryArea(images);

        result.Should().HaveCount(1);
        result[0].Images.Should().ContainSingle();
    }

    [Fact]
    public void GroupByRegistryArea_MixedRegistries_CorrectGrouping()
    {
        var images = new[]
        {
            "nginx:latest",
            "redis:7",
            "amssolution/ams-api:1.0",
            "amssolution/ams-worker:1.0",
            "ghcr.io/wiesenwischer/app:v1",
            "ghcr.io/wiesenwischer/web:v2"
        };

        var result = _extractor.GroupByRegistryArea(images);

        result.Should().HaveCount(3);

        var dockerHubLibrary = result.First(a => a.Namespace == "library");
        dockerHubLibrary.Images.Should().HaveCount(2);
        dockerHubLibrary.IsLikelyPublic.Should().BeTrue();

        var dockerHubAms = result.First(a => a.Namespace == "amssolution");
        dockerHubAms.Images.Should().HaveCount(2);
        dockerHubAms.IsLikelyPublic.Should().BeTrue();

        var ghcr = result.First(a => a.Host == "ghcr.io");
        ghcr.Images.Should().HaveCount(2);
        ghcr.IsLikelyPublic.Should().BeTrue();
    }

    [Fact]
    public void GroupByRegistryArea_ResultsAreSortedByHostThenNamespace()
    {
        var images = new[]
        {
            "ghcr.io/zorg/app:v1",
            "nginx:latest",
            "ghcr.io/aorg/app:v1",
            "amssolution/ams-api:1.0"
        };

        var result = _extractor.GroupByRegistryArea(images);

        result.Should().HaveCount(4);
        var keys = result.Select(a => $"{a.Host}/{a.Namespace}").ToList();
        keys.Should().BeInAscendingOrder();
    }

    #endregion
}
