namespace ReadyStackGo.DomainTests.StepDefinitions;

using FluentAssertions;
using Reqnroll;
using ReadyStackGo.DomainTests.Support;
using ReadyStackGo.Domain.IdentityAccess.Aggregates;
using ReadyStackGo.Domain.IdentityAccess.ValueObjects;
using ReadyStackGo.Domain.IdentityAccess.ValueObjects;

[Binding]
public class UserAuthenticationSteps
{
    private readonly TestContext _context;

    public UserAuthenticationSteps(TestContext context)
    {
        _context = context;
    }

    [Given(@"organization ""(.*)"" exists and is active")]
    public void GivenOrganizationExistsAndIsActive(string orgName)
    {
        var tenantId = _context.TenantRepository.NextIdentity();
        var tenant = Tenant.Provision(tenantId, orgName, "Test organization");
        tenant.Activate();
        _context.TenantRepository.Add(tenant);
        _context.Tenants[orgName] = tenant;
    }

    [Given(@"user ""(.*)"" exists with password ""(.*)""")]
    public void GivenUserExistsWithPassword(string username, string password)
    {
        var tenant = _context.Tenants.Values.FirstOrDefault()
            ?? throw new InvalidOperationException("No tenant exists");

        var userId = _context.UserRepository.NextIdentity();
        var email = new EmailAddress($"{username}@test.com");
        var hashedPassword = HashedPassword.Create(password, _context.PasswordHasher);
        var user = User.Register(userId, tenant.Id, username, email, hashedPassword);
        _context.UserRepository.Add(user);
        _context.Users[username] = user;
    }

    [Given(@"user ""(.*)"" is disabled")]
    public void GivenUserIsDisabled(string username)
    {
        var user = _context.Users[username];
        user.Disable();
    }

    [Given(@"organization ""(.*)"" is deactivated")]
    public void GivenOrganizationIsDeactivated(string orgName)
    {
        var tenant = _context.Tenants[orgName];
        tenant.Deactivate();
    }

    [When(@"I authenticate with username ""(.*)"" and password ""(.*)""")]
    public void WhenIAuthenticateWithUsernameAndPassword(string username, string password)
    {
        try
        {
            _context.AuthenticatedUser = _context.AuthenticationService.Authenticate(username, password);
            _context.AuthenticationSucceeded = _context.AuthenticatedUser != null;
        }
        catch (Exception ex)
        {
            _context.LastException = ex;
            _context.AuthenticationSucceeded = false;
        }
    }

    [Then(@"authentication should succeed")]
    public void ThenAuthenticationShouldSucceed()
    {
        _context.LastException.Should().BeNull();
        _context.AuthenticationSucceeded.Should().BeTrue();
        _context.AuthenticatedUser.Should().NotBeNull();
    }

    [Then(@"authentication should fail")]
    public void ThenAuthenticationShouldFail()
    {
        _context.AuthenticationSucceeded.Should().BeFalse();
    }
}
