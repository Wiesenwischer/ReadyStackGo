namespace ReadyStackGo.DomainTests.StepDefinitions;

using FluentAssertions;
using Reqnroll;
using ReadyStackGo.DomainTests.Support;
using ReadyStackGo.Domain.IdentityAccess.Organizations;
using ReadyStackGo.Domain.IdentityAccess.Roles;
using ReadyStackGo.Domain.IdentityAccess.Users;
using ReadyStackGo.Domain.Deployment.Environments;

[Binding]
public class InitialSetupWizardSteps
{
    private readonly TestContext _context;

    public InitialSetupWizardSteps(TestContext context)
    {
        _context = context;
    }

    [Given(@"the system is not initialized")]
    public void GivenTheSystemIsNotInitialized()
    {
        _context.Reset();
        _context.SystemInitialized = false;
    }

    [Given(@"the system is already initialized")]
    public void GivenTheSystemIsAlreadyInitialized()
    {
        _context.SystemInitialized = true;
    }

    [Given(@"user ""(.*)"" completed wizard step 1 and is SystemAdmin")]
    public void GivenUserCompletedWizardStep1AndIsSystemAdmin(string username)
    {
        var userId = _context.UserRepository.NextIdentity();
        var email = new EmailAddress($"{username}@test.com");
        var hashedPassword = HashedPassword.Create("TestPass123!", _context.PasswordHasher);
        var user = User.Register(userId, username, email, hashedPassword);
        user.AssignRole(RoleAssignment.Global(RoleId.SystemAdmin));

        _context.UserRepository.Add(user);
        _context.Users[username] = user;
        _context.CurrentUser = user;
        _context.SystemInitialized = true;
    }

    [When(@"I complete wizard step 1 with user ""(.*)"" email ""(.*)"" and password ""(.*)""")]
    public void WhenICompleteWizardStep1WithUserEmailAndPassword(string username, string email, string password)
    {
        try
        {
            if (_context.SystemInitialized)
            {
                throw new InvalidOperationException("System is already initialized");
            }

            var userId = _context.UserRepository.NextIdentity();
            var emailAddress = new EmailAddress(email);
            var hashedPassword = HashedPassword.Create(password, _context.PasswordHasher);
            var user = User.Register(userId, username, emailAddress, hashedPassword);

            // Wizard step 1: User becomes SystemAdmin globally
            user.AssignRole(RoleAssignment.Global(RoleId.SystemAdmin));

            _context.UserRepository.Add(user);
            _context.Users[username] = user;
            _context.CurrentUser = user;
            _context.SystemInitialized = true;
        }
        catch (Exception ex)
        {
            _context.LastException = ex;
        }
    }

    [When(@"I complete wizard step 2 with organization ""(.*)"" and environment ""(.*)""")]
    public void WhenICompleteWizardStep2WithOrganizationAndEnvironment(string orgName, string envName)
    {
        try
        {
            if (_context.CurrentUser == null)
            {
                throw new InvalidOperationException("No user from step 1");
            }

            // Create organization
            var organization = _context.OrganizationProvisioningService.ProvisionOrganization(
                orgName,
                "Created during wizard",
                _context.CurrentUser);

            _context.CurrentOrganization = organization;
            _context.Organizations[orgName] = organization;

            // Create default environment
            var envId = _context.EnvironmentRepository.NextIdentity();
            var environment = Environment.CreateDefault(envId, organization.Id, envName);
            environment.SetAsDefault();

            _context.EnvironmentRepository.Add(environment);
            _context.EnvironmentEntities[envName] = environment;
            _context.CurrentEnvironment = environment;
        }
        catch (Exception ex)
        {
            _context.LastException = ex;
        }
    }

    [When(@"I try to complete wizard step (.*)")]
    public void WhenITryToCompleteWizardStep(int step)
    {
        try
        {
            if (_context.SystemInitialized)
            {
                throw new InvalidOperationException("System is already initialized");
            }
        }
        catch (Exception ex)
        {
            _context.LastException = ex;
        }
    }

    [Then(@"environment ""(.*)"" should exist for ""(.*)""")]
    public void ThenEnvironmentShouldExistFor(string envName, string orgName)
    {
        _context.CurrentEnvironment.Should().NotBeNull();
        _context.CurrentEnvironment!.Name.Should().Be(envName);

        var organization = _context.Organizations[orgName];
        _context.CurrentEnvironment.OrganizationId.Should().Be(organization.Id);
    }

    // Note: "environment should be default" step is defined in EnvironmentManagementSteps
}
