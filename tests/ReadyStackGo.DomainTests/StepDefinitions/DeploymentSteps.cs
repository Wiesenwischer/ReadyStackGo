namespace ReadyStackGo.DomainTests.StepDefinitions;

using FluentAssertions;
using Reqnroll;
using ReadyStackGo.DomainTests.Support;
using ReadyStackGo.Domain.IdentityAccess.Organizations;
using ReadyStackGo.Domain.IdentityAccess.Roles;
using ReadyStackGo.Domain.IdentityAccess.Users;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.Deployments;
using Environment = ReadyStackGo.Domain.Deployment.Environments.Environment;

[Binding]
public class DeploymentSteps
{
    private readonly TestContext _context;

    public DeploymentSteps(TestContext context)
    {
        _context = context;
    }

    #region Given Steps

    [Given(@"environment ""(.*)"" exists for ""(.*)""")]
    public void GivenEnvironmentExistsFor(string envName, string orgName)
    {
        var organization = _context.Organizations[orgName];
        var envId = _context.EnvironmentRepository.NextIdentity();
        var environment = Environment.CreateDefault(envId, organization.Id, envName);
        _context.EnvironmentRepository.Add(environment);
        _context.EnvironmentEntities[envName] = environment;
        _context.Environments[envName] = envId.Value;
        _context.CurrentEnvironment = environment;
    }

    [Given(@"user ""(.*)"" exists with ""(.*)"" role for ""(.*)""")]
    public void GivenUserExistsWithRoleFor(string username, string roleName, string orgName)
    {
        var organization = _context.Organizations[orgName];

        var userId = _context.UserRepository.NextIdentity();
        var email = new EmailAddress($"{username}@test.com");
        var hashedPassword = HashedPassword.Create("TestPass123!", _context.PasswordHasher);
        var user = User.Register(userId, username, email, hashedPassword);

        var roleId = new RoleId(roleName);
        user.AssignRole(RoleAssignment.ForOrganization(roleId, organization.Id.Value.ToString()));

        _context.UserRepository.Add(user);
        _context.Users[username] = user;
    }

    [Given(@"deployment ""(.*)"" is started to environment ""(.*)""")]
    public void GivenDeploymentIsStartedToEnvironment(string stackName, string envName)
    {
        var environment = _context.EnvironmentEntities[envName];
        var user = _context.Users.Values.First();

        var deploymentId = DeploymentId.Create();
        var deployment = Deployment.Start(
            deploymentId,
            environment.Id,
            stackName,
            $"{stackName}-project",
            user.Id);

        _context.CurrentDeployment = deployment;
        _context.Deployments[stackName] = deployment;
    }

    [Given(@"deployment ""(.*)"" is running with services")]
    public void GivenDeploymentIsRunningWithServices(string stackName)
    {
        GivenDeploymentIsStartedToEnvironment(stackName, _context.EnvironmentEntities.Keys.First());

        var services = new[]
        {
            new DeployedService("api", "container-abc", "api-container", "nginx:latest", "running"),
            new DeployedService("database", "container-def", "db-container", "postgres:15", "running")
        };

        _context.CurrentDeployment!.MarkAsRunning(services);
    }

    [Given(@"deployment ""(.*)"" is stopped")]
    public void GivenDeploymentIsStopped(string stackName)
    {
        GivenDeploymentIsRunningWithServices(stackName);
        _context.CurrentDeployment!.MarkAsStopped();
    }

    [Given(@"deployment ""(.*)"" has failed")]
    public void GivenDeploymentHasFailed(string stackName)
    {
        GivenDeploymentIsStartedToEnvironment(stackName, _context.EnvironmentEntities.Keys.First());
        _context.CurrentDeployment!.MarkAsFailed("Previous failure");
    }

    [Given(@"deployment ""(.*)"" is pending")]
    public void GivenDeploymentIsPending(string stackName)
    {
        GivenDeploymentIsStartedToEnvironment(stackName, _context.EnvironmentEntities.Keys.First());
        // Deployment starts as Pending by default
    }

    [Given(@"deployment ""(.*)"" has status ""(.*)""")]
    public void GivenDeploymentHasStatus(string stackName, string status)
    {
        GivenDeploymentIsStartedToEnvironment(stackName, _context.EnvironmentEntities.Keys.First());

        if (status == "Running")
        {
            var services = new[] { new DeployedService("api", "container-abc", "api-container", "nginx:latest", "running") };
            _context.CurrentDeployment!.MarkAsRunning(services);
        }
    }

    #endregion

    #region When Steps

    [When(@"I start deployment ""(.*)"" to environment ""(.*)"" as ""(.*)""")]
    public void WhenIStartDeploymentToEnvironmentAs(string stackName, string envName, string username)
    {
        try
        {
            var environment = _context.EnvironmentEntities[envName];
            var user = _context.Users[username];

            var deploymentId = DeploymentId.Create();
            var deployment = Deployment.Start(
                deploymentId,
                environment.Id,
                stackName,
                $"{stackName}-project",
                user.Id);

            _context.CurrentDeployment = deployment;
            _context.Deployments[stackName] = deployment;
        }
        catch (Exception ex)
        {
            _context.LastException = ex;
        }
    }

    [When(@"I update progress to phase ""(.*)"" at (.*)% with message ""(.*)""")]
    public void WhenIUpdateProgressToPhaseAtWithMessage(string phaseName, int percentage, string message)
    {
        try
        {
            var phase = Enum.Parse<DeploymentPhase>(phaseName);
            _context.CurrentDeployment!.UpdateProgress(phase, percentage, message);
        }
        catch (Exception ex)
        {
            _context.LastException = ex;
        }
    }

    [When(@"I mark the deployment as running with services:")]
    public void WhenIMarkTheDeploymentAsRunningWithServices(Table table)
    {
        try
        {
            var services = table.Rows.Select(row => new DeployedService(
                row["ServiceName"],
                row["ContainerId"],
                row["ServiceName"] + "-container",
                "test:latest",
                row["Status"]
            )).ToList();

            _context.CurrentDeployment!.MarkAsRunning(services);
        }
        catch (Exception ex)
        {
            _context.LastException = ex;
        }
    }

    [When(@"I mark the deployment as failed with error ""(.*)""")]
    public void WhenIMarkTheDeploymentAsFailedWithError(string errorMessage)
    {
        try
        {
            _context.CurrentDeployment!.MarkAsFailed(errorMessage);
        }
        catch (Exception ex)
        {
            _context.LastException = ex;
        }
    }

    [When(@"I stop the deployment")]
    public void WhenIStopTheDeployment()
    {
        try
        {
            _context.CurrentDeployment!.MarkAsStopped();
        }
        catch (Exception ex)
        {
            _context.LastException = ex;
        }
    }

    [When(@"I restart the deployment")]
    public void WhenIRestartTheDeployment()
    {
        try
        {
            _context.CurrentDeployment!.Restart();
        }
        catch (Exception ex)
        {
            _context.LastException = ex;
        }
    }

    [When(@"I try to restart the deployment")]
    public void WhenITryToRestartTheDeployment()
    {
        WhenIRestartTheDeployment();
    }

    [When(@"I request cancellation with reason ""(.*)""")]
    public void WhenIRequestCancellationWithReason(string reason)
    {
        try
        {
            _context.CurrentDeployment!.RequestCancellation(reason);
        }
        catch (Exception ex)
        {
            _context.LastException = ex;
        }
    }

    [When(@"the deployment status changes to ""(.*)""")]
    public void WhenTheDeploymentStatusChangesTo(string status)
    {
        try
        {
            if (status == "Running")
            {
                var services = new[] { new DeployedService("api", "container-abc", "api-container", "nginx:latest", "running") };
                _context.CurrentDeployment!.MarkAsRunning(services);
            }
            else if (status == "Stopped")
            {
                _context.CurrentDeployment!.MarkAsStopped();
            }
            else if (status == "Failed")
            {
                _context.CurrentDeployment!.MarkAsFailed("Test failure");
            }
        }
        catch (Exception ex)
        {
            _context.LastException = ex;
        }
    }

    [When(@"I remove the deployment")]
    public void WhenIRemoveTheDeployment()
    {
        try
        {
            _context.CurrentDeployment!.MarkAsRemoved();
        }
        catch (Exception ex)
        {
            _context.LastException = ex;
        }
    }

    #endregion

    #region Then Steps

    [Then(@"the deployment should have status ""(.*)""")]
    public void ThenTheDeploymentShouldHaveStatus(string expectedStatus)
    {
        var status = Enum.Parse<DeploymentStatus>(expectedStatus);
        _context.CurrentDeployment!.Status.Should().Be(status);
    }

    [Then(@"the deployment should have phase ""(.*)""")]
    public void ThenTheDeploymentShouldHavePhase(string expectedPhase)
    {
        var phase = Enum.Parse<DeploymentPhase>(expectedPhase);
        _context.CurrentDeployment!.CurrentPhase.Should().Be(phase);
    }

    [Then(@"the deployment should have progress (.*)%")]
    public void ThenTheDeploymentShouldHaveProgress(int expectedPercentage)
    {
        _context.CurrentDeployment!.ProgressPercentage.Should().Be(expectedPercentage);
    }

    [Then(@"the deployment should have (.*) services")]
    public void ThenTheDeploymentShouldHaveServices(int expectedCount)
    {
        _context.CurrentDeployment!.Services.Count.Should().Be(expectedCount);
    }

    [Then(@"all services should be healthy")]
    public void ThenAllServicesShouldBeHealthy()
    {
        _context.CurrentDeployment!.AreAllServicesHealthy().Should().BeTrue();
    }

    [Then(@"the error message should contain ""(.*)""")]
    public void ThenTheErrorMessageShouldContain(string expectedText)
    {
        _context.CurrentDeployment!.ErrorMessage.Should().Contain(expectedText);
    }

    [Then(@"cancellation should be requested")]
    public void ThenCancellationShouldBeRequested()
    {
        _context.CurrentDeployment!.IsCancellationRequested.Should().BeTrue();
    }

    [Then(@"the cancellation reason should be ""(.*)""")]
    public void ThenTheCancellationReasonShouldBe(string expectedReason)
    {
        _context.CurrentDeployment!.CancellationReason.Should().Be(expectedReason);
    }

    [Then(@"the valid next states should be ""(.*)""")]
    public void ThenTheValidNextStatesShouldBe(string expectedStates)
    {
        var expected = expectedStates.Split(", ")
            .Select(s => Enum.Parse<DeploymentStatus>(s.Trim()))
            .OrderBy(s => s)
            .ToList();

        var actual = _context.CurrentDeployment!.GetValidNextStates()
            .OrderBy(s => s)
            .ToList();

        actual.Should().BeEquivalentTo(expected);
    }

    [Then(@"the deployment should be terminal")]
    public void ThenTheDeploymentShouldBeTerminal()
    {
        _context.CurrentDeployment!.IsTerminal.Should().BeTrue();
    }

    #endregion
}
