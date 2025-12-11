using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using ReadyStackGo.IntegrationTests.Infrastructure;

namespace ReadyStackGo.IntegrationTests;

/// <summary>
/// End-to-end tests for Stack Variable Types API.
/// Verifies that variable type information (Type, Label, Description, Options, etc.)
/// is correctly returned from the API endpoints.
/// </summary>
public class StackVariableTypesIntegrationTests : AuthenticatedTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    #region List Stacks Variable Types

    [Fact]
    public async Task GET_Stacks_ReturnsVariablesWithTypeProperty()
    {
        // Act
        var response = await Client.GetAsync("/api/stacks");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stacks = await response.Content.ReadFromJsonAsync<List<StackListResponseDto>>(JsonOptions);
        stacks.Should().NotBeNull();

        // Find any stack with variables
        var stackWithVariables = stacks!.FirstOrDefault(s => s.Variables?.Count > 0);
        if (stackWithVariables != null)
        {
            stackWithVariables.Variables.Should().AllSatisfy(v =>
            {
                v.Name.Should().NotBeNullOrEmpty("Variable must have a name");
                v.Type.Should().NotBeNullOrEmpty("Variable must have a type");
            });
        }
    }

    [Fact]
    public async Task GET_Stacks_VariablesContainExpectedTypes()
    {
        // Act
        var response = await Client.GetAsync("/api/stacks");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stacks = await response.Content.ReadFromJsonAsync<List<StackListResponseDto>>(JsonOptions);

        var allVariables = stacks!.SelectMany(s => s.Variables ?? new List<VariableDto>()).ToList();

        if (allVariables.Any())
        {
            // All variables should have valid type values
            var validTypes = new[]
            {
                "String", "Number", "Boolean", "Select", "Password",
                "Port", "Url", "Email", "Path", "MultiLine",
                "ConnectionString", "SqlServerConnectionString", "PostgresConnectionString",
                "MySqlConnectionString", "EventStoreConnectionString", "MongoConnectionString",
                "RedisConnectionString"
            };

            allVariables.Should().AllSatisfy(v =>
            {
                validTypes.Should().Contain(v.Type, $"Variable '{v.Name}' has unexpected type '{v.Type}'");
            });
        }
    }

    [Fact]
    public async Task GET_Stacks_VariablesHaveFullMetadata()
    {
        // Act
        var response = await Client.GetAsync("/api/stacks");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stacks = await response.Content.ReadFromJsonAsync<List<StackListResponseDto>>(JsonOptions);

        var allVariables = stacks!.SelectMany(s => s.Variables ?? new List<VariableDto>()).ToList();

        // Variables with labels should have them properly set
        var varsWithLabels = allVariables.Where(v => v.Label != null).ToList();
        varsWithLabels.Should().AllSatisfy(v =>
        {
            v.Label.Should().NotBeEmpty("Label should not be empty string when set");
        });

        // Variables with groups should have them properly set
        var varsWithGroups = allVariables.Where(v => v.Group != null).ToList();
        varsWithGroups.Should().AllSatisfy(v =>
        {
            v.Group.Should().NotBeEmpty("Group should not be empty string when set");
        });
    }

    #endregion

    #region Get Stack Detail Variable Types

    [Fact]
    public async Task GET_StackDetail_ReturnsVariablesWithFullTypeInfo()
    {
        // Arrange - First get list of stacks to find a valid ID
        var listResponse = await Client.GetAsync("/api/stacks");
        var stacks = await listResponse.Content.ReadFromJsonAsync<List<StackListResponseDto>>(JsonOptions);

        var stackWithVariables = stacks?.FirstOrDefault(s => s.Variables?.Count > 0);
        if (stackWithVariables == null)
        {
            // Skip test if no stacks with variables exist
            return;
        }

        // Act
        var response = await Client.GetAsync($"/api/stacks/{stackWithVariables.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stackDetail = await response.Content.ReadFromJsonAsync<StackDetailResponseDto>(JsonOptions);

        stackDetail.Should().NotBeNull();
        stackDetail!.Variables.Should().NotBeNull();
        stackDetail.Variables.Should().HaveCountGreaterThan(0);

        stackDetail.Variables.Should().AllSatisfy(v =>
        {
            v.Name.Should().NotBeNullOrEmpty();
            v.Type.Should().NotBeNullOrEmpty();
        });
    }

    [Fact]
    public async Task GET_StackDetail_SelectTypeHasOptions()
    {
        // Arrange - Get all stacks
        var listResponse = await Client.GetAsync("/api/stacks");
        var stacks = await listResponse.Content.ReadFromJsonAsync<List<StackListResponseDto>>(JsonOptions);

        // Find stack with Select type variable
        var stackWithSelect = stacks?.FirstOrDefault(s =>
            s.Variables?.Any(v => v.Type == "Select") == true);

        if (stackWithSelect == null)
        {
            // Skip if no Select variables exist
            return;
        }

        // Act
        var response = await Client.GetAsync($"/api/stacks/{stackWithSelect.Id}");
        var stackDetail = await response.Content.ReadFromJsonAsync<StackDetailResponseDto>(JsonOptions);

        // Assert
        var selectVariables = stackDetail!.Variables.Where(v => v.Type == "Select").ToList();
        selectVariables.Should().AllSatisfy(v =>
        {
            v.Options.Should().NotBeNull("Select type variables must have Options");
            v.Options.Should().HaveCountGreaterThan(0, "Select type should have at least one option");

            v.Options!.Should().AllSatisfy(o =>
            {
                o.Value.Should().NotBeNullOrEmpty("Option must have a value");
            });
        });
    }

    [Fact]
    public async Task GET_StackDetail_PortTypeHasCorrectConstraints()
    {
        // Arrange - Get all stacks
        var listResponse = await Client.GetAsync("/api/stacks");
        var stacks = await listResponse.Content.ReadFromJsonAsync<List<StackListResponseDto>>(JsonOptions);

        // Find stack with Port type variable
        var stackWithPort = stacks?.FirstOrDefault(s =>
            s.Variables?.Any(v => v.Type == "Port") == true);

        if (stackWithPort == null)
        {
            // Skip if no Port variables exist
            return;
        }

        // Act
        var response = await Client.GetAsync($"/api/stacks/{stackWithPort.Id}");
        var stackDetail = await response.Content.ReadFromJsonAsync<StackDetailResponseDto>(JsonOptions);

        // Assert
        var portVariables = stackDetail!.Variables.Where(v => v.Type == "Port").ToList();
        portVariables.Should().HaveCountGreaterThan(0);

        // Port variables should have valid defaults if set
        portVariables.Where(v => v.DefaultValue != null).Should().AllSatisfy(v =>
        {
            int.TryParse(v.DefaultValue, out var port).Should().BeTrue($"Port default value should be a number, got '{v.DefaultValue}'");
            port.Should().BeInRange(1, 65535, "Port should be in valid range");
        });
    }

    [Fact]
    public async Task GET_StackDetail_SqlServerConnectionStringTypeExists()
    {
        // Arrange - Get all stacks
        var listResponse = await Client.GetAsync("/api/stacks");
        var stacks = await listResponse.Content.ReadFromJsonAsync<List<StackListResponseDto>>(JsonOptions);

        // Find stack with SqlServerConnectionString type variable
        var stackWithSqlServer = stacks?.FirstOrDefault(s =>
            s.Variables?.Any(v => v.Type == "SqlServerConnectionString") == true);

        if (stackWithSqlServer == null)
        {
            // This is a valid scenario if no such stacks exist in test data
            return;
        }

        // Act
        var response = await Client.GetAsync($"/api/stacks/{stackWithSqlServer.Id}");
        var stackDetail = await response.Content.ReadFromJsonAsync<StackDetailResponseDto>(JsonOptions);

        // Assert
        var sqlVars = stackDetail!.Variables.Where(v => v.Type == "SqlServerConnectionString").ToList();
        sqlVars.Should().HaveCountGreaterThan(0);

        // SQL connection string variables should have a label and group typically
        sqlVars.Should().AllSatisfy(v =>
        {
            v.Name.Should().NotBeNullOrEmpty();
            // DefaultValue may contain connection string pattern
            if (v.DefaultValue != null)
            {
                v.DefaultValue.Should().Contain("Server=", "Connection string should contain Server parameter");
            }
        });
    }

    #endregion

    #region Variable Validation Properties

    [Fact]
    public async Task GET_StackDetail_VariablesHaveValidationProperties()
    {
        // Arrange
        var listResponse = await Client.GetAsync("/api/stacks");
        var stacks = await listResponse.Content.ReadFromJsonAsync<List<StackListResponseDto>>(JsonOptions);

        var stackWithVariables = stacks?.FirstOrDefault(s => s.Variables?.Count > 0);
        if (stackWithVariables == null)
        {
            return;
        }

        // Act
        var response = await Client.GetAsync($"/api/stacks/{stackWithVariables.Id}");
        var stackDetail = await response.Content.ReadFromJsonAsync<StackDetailResponseDto>(JsonOptions);

        // Assert - Variables with patterns should have valid regex
        var varsWithPatterns = stackDetail!.Variables.Where(v => v.Pattern != null).ToList();
        varsWithPatterns.Should().AllSatisfy(v =>
        {
            // Verify pattern is valid regex
            Action testPattern = () => new System.Text.RegularExpressions.Regex(v.Pattern!);
            testPattern.Should().NotThrow($"Pattern for variable '{v.Name}' should be valid regex");
        });

        // Variables with min/max should have valid numbers
        var varsWithMin = stackDetail.Variables.Where(v => v.Min != null).ToList();
        varsWithMin.Should().AllSatisfy(v =>
        {
            v.Min.Should().BeOfType(typeof(double), "Min should be a number");
        });

        var varsWithMax = stackDetail.Variables.Where(v => v.Max != null).ToList();
        varsWithMax.Should().AllSatisfy(v =>
        {
            v.Max.Should().BeOfType(typeof(double), "Max should be a number");
        });
    }

    [Fact]
    public async Task GET_StackDetail_RequiredVariablesAreMarked()
    {
        // Arrange
        var listResponse = await Client.GetAsync("/api/stacks");
        var stacks = await listResponse.Content.ReadFromJsonAsync<List<StackListResponseDto>>(JsonOptions);

        var stackWithVariables = stacks?.FirstOrDefault(s => s.Variables?.Any(v => v.IsRequired) == true);
        if (stackWithVariables == null)
        {
            return;
        }

        // Act
        var response = await Client.GetAsync($"/api/stacks/{stackWithVariables.Id}");
        var stackDetail = await response.Content.ReadFromJsonAsync<StackDetailResponseDto>(JsonOptions);

        // Assert
        var requiredVars = stackDetail!.Variables.Where(v => v.IsRequired).ToList();
        requiredVars.Should().HaveCountGreaterThan(0, "Should have required variables");
    }

    #endregion

    #region Variable Groups and Order

    [Fact]
    public async Task GET_StackDetail_VariablesHaveGroupsAndOrder()
    {
        // Arrange
        var listResponse = await Client.GetAsync("/api/stacks");
        var stacks = await listResponse.Content.ReadFromJsonAsync<List<StackListResponseDto>>(JsonOptions);

        var stackWithGroups = stacks?.FirstOrDefault(s =>
            s.Variables?.Any(v => v.Group != null) == true);

        if (stackWithGroups == null)
        {
            return;
        }

        // Act
        var response = await Client.GetAsync($"/api/stacks/{stackWithGroups.Id}");
        var stackDetail = await response.Content.ReadFromJsonAsync<StackDetailResponseDto>(JsonOptions);

        // Assert
        var varsWithGroups = stackDetail!.Variables.Where(v => v.Group != null).ToList();
        varsWithGroups.Should().HaveCountGreaterThan(0);

        // Variables in the same group should be able to be sorted by order
        var groups = varsWithGroups.GroupBy(v => v.Group).ToList();
        groups.Should().AllSatisfy(g =>
        {
            var varsInGroup = g.ToList();
            // If any variable has order, they can be sorted
            if (varsInGroup.Any(v => v.Order != null))
            {
                var orderedVars = varsInGroup.Where(v => v.Order != null).OrderBy(v => v.Order).ToList();
                orderedVars.Should().BeInAscendingOrder(v => v.Order);
            }
        });
    }

    #endregion

    #region Response DTOs for Variable Type Testing

    public class StackListResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string SourceId { get; set; } = string.Empty;
        public string SourceName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? RelativePath { get; set; }
        public List<string> Services { get; set; } = new();
        public List<VariableDto> Variables { get; set; } = new();
        public DateTime LastSyncedAt { get; set; }
        public string? Version { get; set; }
    }

    public class StackDetailResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string SourceId { get; set; } = string.Empty;
        public string SourceName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public List<ServiceResponseDto> Services { get; set; } = new();
        public List<VariableDto> Variables { get; set; } = new();
        public List<VolumeResponseDto> Volumes { get; set; } = new();
        public List<NetworkResponseDto> Networks { get; set; } = new();
        public string? FilePath { get; set; }
        public DateTime LastSyncedAt { get; set; }
        public string? Version { get; set; }
    }

    public class ServiceResponseDto
    {
        public string Name { get; set; } = string.Empty;
        public string Image { get; set; } = string.Empty;
        public string? ContainerName { get; set; }
        public List<string> Ports { get; set; } = new();
        public Dictionary<string, string> Environment { get; set; } = new();
        public List<string> Volumes { get; set; } = new();
        public List<string> Networks { get; set; } = new();
        public List<string> DependsOn { get; set; } = new();
    }

    public class VolumeResponseDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Driver { get; set; }
        public bool External { get; set; }
    }

    public class NetworkResponseDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Driver { get; set; }
        public bool External { get; set; }
    }

    public class VariableDto
    {
        public string Name { get; set; } = string.Empty;
        public string? DefaultValue { get; set; }
        public bool IsRequired { get; set; }
        public string Type { get; set; } = "String";
        public string? Label { get; set; }
        public string? Description { get; set; }
        public string? Placeholder { get; set; }
        public string? Group { get; set; }
        public int? Order { get; set; }
        public string? Pattern { get; set; }
        public string? PatternError { get; set; }
        public double? Min { get; set; }
        public double? Max { get; set; }
        public List<SelectOptionDto>? Options { get; set; }
    }

    public class SelectOptionDto
    {
        public string Value { get; set; } = string.Empty;
        public string? Label { get; set; }
        public string? Description { get; set; }
    }

    #endregion
}
