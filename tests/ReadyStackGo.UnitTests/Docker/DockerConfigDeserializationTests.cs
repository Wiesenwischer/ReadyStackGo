using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;

namespace ReadyStackGo.UnitTests.Docker;

/// <summary>
/// Tests for Docker config.json deserialization.
/// These tests ensure that the Docker credential config can be properly parsed
/// from the lowercase JSON format used by Docker.
/// </summary>
public class DockerConfigDeserializationTests
{
    /// <summary>
    /// Docker config.json structure - mirrors the private class in DockerService
    /// </summary>
    private class DockerConfigFile
    {
        [JsonPropertyName("auths")]
        public Dictionary<string, DockerAuthEntry>? Auths { get; set; }
    }

    private class DockerAuthEntry
    {
        [JsonPropertyName("auth")]
        public string? Auth { get; set; }

        [JsonPropertyName("username")]
        public string? Username { get; set; }

        [JsonPropertyName("password")]
        public string? Password { get; set; }
    }

    [Fact]
    public void DockerConfig_DeserializesFromLowercaseJson()
    {
        // Arrange - Actual Docker config.json format (lowercase keys)
        var json = @"{
            ""auths"": {
                ""https://index.docker.io/v1/"": {
                    ""auth"": ""dXNlcm5hbWU6cGFzc3dvcmQ=""
                }
            }
        }";

        // Act
        var config = JsonSerializer.Deserialize<DockerConfigFile>(json);

        // Assert
        config.Should().NotBeNull();
        config!.Auths.Should().NotBeNull();
        config.Auths.Should().ContainKey("https://index.docker.io/v1/");
        config.Auths!["https://index.docker.io/v1/"].Auth.Should().Be("dXNlcm5hbWU6cGFzc3dvcmQ=");
    }

    [Fact]
    public void DockerConfig_WithoutJsonPropertyName_FailsToDeserialize()
    {
        // Arrange - Test class WITHOUT JsonPropertyName attributes
        var json = @"{
            ""auths"": {
                ""https://index.docker.io/v1/"": {
                    ""auth"": ""dXNlcm5hbWU6cGFzc3dvcmQ=""
                }
            }
        }";

        // Act - Deserialize with a class that has PascalCase properties without attributes
        var config = JsonSerializer.Deserialize<DockerConfigFileWithoutAttributes>(json);

        // Assert - Without JsonPropertyName, the Auths property will be null
        config.Should().NotBeNull();
        config!.Auths.Should().BeNull("System.Text.Json is case-sensitive by default");
    }

    /// <summary>
    /// Test class WITHOUT JsonPropertyName attributes to prove the bug
    /// </summary>
    private class DockerConfigFileWithoutAttributes
    {
        public Dictionary<string, DockerAuthEntryWithoutAttributes>? Auths { get; set; }
    }

    private class DockerAuthEntryWithoutAttributes
    {
        public string? Auth { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
    }

    [Fact]
    public void DockerConfig_DeserializesMultipleRegistries()
    {
        // Arrange
        var json = @"{
            ""auths"": {
                ""https://index.docker.io/v1/"": {
                    ""auth"": ""ZG9ja2VyaHViOnBhc3M=""
                },
                ""ghcr.io"": {
                    ""auth"": ""Z2l0aHViOnRva2Vu""
                },
                ""myregistry.azurecr.io"": {
                    ""username"": ""azure-user"",
                    ""password"": ""azure-pass""
                }
            }
        }";

        // Act
        var config = JsonSerializer.Deserialize<DockerConfigFile>(json);

        // Assert
        config.Should().NotBeNull();
        config!.Auths.Should().HaveCount(3);
        config.Auths.Should().ContainKey("https://index.docker.io/v1/");
        config.Auths.Should().ContainKey("ghcr.io");
        config.Auths.Should().ContainKey("myregistry.azurecr.io");

        // Docker Hub uses base64 auth
        config.Auths!["https://index.docker.io/v1/"].Auth.Should().Be("ZG9ja2VyaHViOnBhc3M=");

        // Azure uses username/password directly
        config.Auths["myregistry.azurecr.io"].Username.Should().Be("azure-user");
        config.Auths["myregistry.azurecr.io"].Password.Should().Be("azure-pass");
    }

    [Fact]
    public void DockerConfig_DecodesBase64Auth()
    {
        // Arrange - base64("username:password") = "dXNlcm5hbWU6cGFzc3dvcmQ="
        var json = @"{
            ""auths"": {
                ""https://index.docker.io/v1/"": {
                    ""auth"": ""dXNlcm5hbWU6cGFzc3dvcmQ=""
                }
            }
        }";

        // Act
        var config = JsonSerializer.Deserialize<DockerConfigFile>(json);
        var auth = config!.Auths!["https://index.docker.io/v1/"].Auth!;
        var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(auth));
        var parts = decoded.Split(':', 2);

        // Assert
        parts.Should().HaveCount(2);
        parts[0].Should().Be("username");
        parts[1].Should().Be("password");
    }

    [Fact]
    public void DockerConfig_HandlesEmptyAuths()
    {
        // Arrange
        var json = @"{
            ""auths"": {}
        }";

        // Act
        var config = JsonSerializer.Deserialize<DockerConfigFile>(json);

        // Assert
        config.Should().NotBeNull();
        config!.Auths.Should().NotBeNull();
        config.Auths.Should().BeEmpty();
    }

    [Fact]
    public void DockerConfig_HandlesTabsAndNewlines()
    {
        // Arrange - Real Docker config format with tabs
        var json = "{\n\t\"auths\": {\n\t\t\"https://index.docker.io/v1/\": {\n\t\t\t\"auth\": \"YW1zbWFkOlpvcm5ib2xkNzch\"\n\t\t}\n\t}\n}";

        // Act
        var config = JsonSerializer.Deserialize<DockerConfigFile>(json);

        // Assert
        config.Should().NotBeNull();
        config!.Auths.Should().ContainKey("https://index.docker.io/v1/");
        config.Auths!["https://index.docker.io/v1/"].Auth.Should().Be("YW1zbWFkOlpvcm5ib2xkNzch");
    }

    [Fact]
    public void DockerConfig_RegistryUrlWithAndWithoutTrailingSlash()
    {
        // Arrange - Some configs have trailing slash, some don't
        var json = @"{
            ""auths"": {
                ""https://index.docker.io/v1/"": {
                    ""auth"": ""d2l0aHNsYXNoOnBhc3M=""
                },
                ""https://index.docker.io/v1"": {
                    ""auth"": ""bm9zbGFzaDpwYXNz""
                }
            }
        }";

        // Act
        var config = JsonSerializer.Deserialize<DockerConfigFile>(json);

        // Assert - Both formats should be preserved
        config.Should().NotBeNull();
        config!.Auths.Should().ContainKey("https://index.docker.io/v1/");
        config.Auths.Should().ContainKey("https://index.docker.io/v1");
    }

    [Fact]
    public void DockerConfig_CredHelpers_AreIgnored()
    {
        // Arrange - Config with credsStore (like on macOS with keychain)
        var json = @"{
            ""auths"": {
                ""https://index.docker.io/v1/"": {}
            },
            ""credsStore"": ""desktop"",
            ""credHelpers"": {
                ""gcr.io"": ""gcloud""
            }
        }";

        // Act
        var config = JsonSerializer.Deserialize<DockerConfigFile>(json);

        // Assert - Should still parse, credHelpers are just ignored
        config.Should().NotBeNull();
        config!.Auths.Should().ContainKey("https://index.docker.io/v1/");
    }

    [Theory]
    [InlineData("nginx", "https://index.docker.io/v1/")]
    [InlineData("library/nginx", "https://index.docker.io/v1/")]
    [InlineData("amssolution/myimage", "https://index.docker.io/v1/")]
    [InlineData("ghcr.io/owner/image", "ghcr.io")]
    [InlineData("myregistry.azurecr.io/image", "myregistry.azurecr.io")]
    [InlineData("localhost:5000/image", "localhost:5000")]
    public void GetRegistryFromImage_ExtractsCorrectRegistry(string image, string expectedRegistry)
    {
        // This tests the same logic as DockerService.GetRegistryFromImage
        var registry = GetRegistryFromImage(image);
        registry.Should().Be(expectedRegistry);
    }

    /// <summary>
    /// Mirror of DockerService.GetRegistryFromImage for testing
    /// </summary>
    private static string GetRegistryFromImage(string image)
    {
        if (!image.Contains('/'))
        {
            return "https://index.docker.io/v1/";
        }

        var firstPart = image.Split('/')[0];

        if (firstPart.Contains('.') || firstPart.Contains(':'))
        {
            return firstPart;
        }

        return "https://index.docker.io/v1/";
    }
}
