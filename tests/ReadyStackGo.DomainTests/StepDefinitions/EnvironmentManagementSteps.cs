namespace ReadyStackGo.DomainTests.StepDefinitions;

using FluentAssertions;
using Reqnroll;
using ReadyStackGo.DomainTests.Support;
using ReadyStackGo.Domain.IdentityAccess.Organizations;
using ReadyStackGo.Domain.Deployment.Environments;
using Environment = ReadyStackGo.Domain.Deployment.Environments.Environment;

[Binding]
public class EnvironmentManagementSteps
{
    private readonly TestContext _context;

    public EnvironmentManagementSteps(TestContext context)
    {
        _context = context;
    }

    #region Given Steps

    [Given(@"environment ""(.*)"" exists for ""(.*)"" and is default")]
    public void GivenEnvironmentExistsForAndIsDefault(string envName, string orgName)
    {
        var organization = _context.Organizations[orgName];
        var envId = _context.EnvironmentRepository.NextIdentity();
        var environment = Environment.CreateDefault(envId, organization.Id, envName);
        environment.SetAsDefault();
        _context.EnvironmentRepository.Add(environment);
        _context.EnvironmentEntities[envName] = environment;
        _context.CurrentEnvironment = environment;
    }

    #endregion

    #region When Steps

    [When(@"I create environment ""(.*)"" for ""(.*)"" with Docker socket ""(.*)""")]
    public void WhenICreateEnvironmentForWithDockerSocket(string envName, string orgName, string socketPath)
    {
        try
        {
            var organization = _context.Organizations[orgName];
            var envId = _context.EnvironmentRepository.NextIdentity();
            var environment = Environment.CreateDockerSocket(
                envId,
                organization.Id,
                envName,
                null,
                socketPath);

            _context.EnvironmentRepository.Add(environment);
            _context.EnvironmentEntities[envName] = environment;
            _context.CurrentEnvironment = environment;
        }
        catch (Exception ex)
        {
            _context.LastException = ex;
        }
    }

    [When(@"I create default environment ""(.*)"" for ""(.*)""")]
    public void WhenICreateDefaultEnvironmentFor(string envName, string orgName)
    {
        try
        {
            var organization = _context.Organizations[orgName];
            var envId = _context.EnvironmentRepository.NextIdentity();
            var environment = Environment.CreateDefault(envId, organization.Id, envName);

            _context.EnvironmentRepository.Add(environment);
            _context.EnvironmentEntities[envName] = environment;
            _context.CurrentEnvironment = environment;
        }
        catch (Exception ex)
        {
            _context.LastException = ex;
        }
    }

    [When(@"I set environment ""(.*)"" as default")]
    public void WhenISetEnvironmentAsDefault(string envName)
    {
        try
        {
            // Unset current default
            var organization = _context.CurrentEnvironment?.OrganizationId
                ?? _context.EnvironmentEntities[envName].OrganizationId;

            foreach (var env in _context.EnvironmentEntities.Values
                .Where(e => e.OrganizationId == organization && e.IsDefault))
            {
                env.UnsetAsDefault();
            }

            // Set new default
            var environment = _context.EnvironmentEntities[envName];
            environment.SetAsDefault();
            _context.CurrentEnvironment = environment;
        }
        catch (Exception ex)
        {
            _context.LastException = ex;
        }
    }

    [When(@"I update environment name to ""(.*)""")]
    public void WhenIUpdateEnvironmentNameTo(string newName)
    {
        try
        {
            _context.CurrentEnvironment!.UpdateName(newName);
            // Update dictionary key
            var oldKey = _context.EnvironmentEntities.FirstOrDefault(x => x.Value == _context.CurrentEnvironment).Key;
            if (oldKey != null)
            {
                _context.EnvironmentEntities.Remove(oldKey);
                _context.EnvironmentEntities[newName] = _context.CurrentEnvironment;
            }
        }
        catch (Exception ex)
        {
            _context.LastException = ex;
        }
    }

    [When(@"I try to create environment with empty name for ""(.*)""")]
    public void WhenITryToCreateEnvironmentWithEmptyNameFor(string orgName)
    {
        try
        {
            var organization = _context.Organizations[orgName];
            var envId = _context.EnvironmentRepository.NextIdentity();
            var environment = Environment.CreateDefault(envId, organization.Id, "");
        }
        catch (Exception ex)
        {
            _context.LastException = ex;
        }
    }

    [When(@"I try to create environment with name longer than 100 characters for ""(.*)""")]
    public void WhenITryToCreateEnvironmentWithNameLongerThan100CharactersFor(string orgName)
    {
        try
        {
            var organization = _context.Organizations[orgName];
            var envId = _context.EnvironmentRepository.NextIdentity();
            var longName = new string('x', 101);
            var environment = Environment.CreateDefault(envId, organization.Id, longName);
        }
        catch (Exception ex)
        {
            _context.LastException = ex;
        }
    }

    #endregion

    #region Then Steps

    [Then(@"the environment should exist with name ""(.*)""")]
    public void ThenTheEnvironmentShouldExistWithName(string expectedName)
    {
        _context.CurrentEnvironment.Should().NotBeNull();
        _context.CurrentEnvironment!.Name.Should().Be(expectedName);
    }

    [Then(@"the environment should have type ""(.*)""")]
    public void ThenTheEnvironmentShouldHaveType(string expectedType)
    {
        var type = Enum.Parse<EnvironmentType>(expectedType);
        _context.CurrentEnvironment!.Type.Should().Be(type);
    }

    [Then(@"the environment should not be default")]
    public void ThenTheEnvironmentShouldNotBeDefault()
    {
        _context.CurrentEnvironment!.IsDefault.Should().BeFalse();
    }

    [Then(@"the connection config should use default Docker socket")]
    public void ThenTheConnectionConfigShouldUseDefaultDockerSocket()
    {
        _context.CurrentEnvironment!.ConnectionConfig.Should().NotBeNull();
        // Default Docker socket path
        var expectedPath = OperatingSystem.IsWindows()
            ? "npipe://./pipe/docker_engine"
            : "unix:///var/run/docker.sock";
        _context.CurrentEnvironment!.ConnectionConfig.SocketPath.Should().Be(expectedPath);
    }

    [Then(@"environment ""(.*)"" should be default")]
    public void ThenEnvironmentShouldBeDefault(string envName)
    {
        var environment = _context.EnvironmentEntities[envName];
        environment.IsDefault.Should().BeTrue();
    }

    [Then(@"environment ""(.*)"" should not be default")]
    public void ThenEnvironmentShouldNotBeDefault(string envName)
    {
        var environment = _context.EnvironmentEntities[envName];
        environment.IsDefault.Should().BeFalse();
    }

    #endregion
}
