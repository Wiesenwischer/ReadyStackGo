namespace ReadyStackGo.DomainTests.StepDefinitions;

using FluentAssertions;
using Reqnroll;
using ReadyStackGo.DomainTests.Support;
using ReadyStackGo.Domain.IdentityAccess.Organizations;
using ReadyStackGo.Domain.IdentityAccess.Users;

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
        var orgId = _context.OrganizationRepository.NextIdentity();
        var org = Organization.Provision(orgId, orgName, "Test organization");
        org.Activate();
        _context.OrganizationRepository.Add(org);
        _context.Organizations[orgName] = org;
    }

    [Given(@"user ""(.*)"" exists with password ""(.*)""")]
    public void GivenUserExistsWithPassword(string username, string password)
    {
        var userId = _context.UserRepository.NextIdentity();
        var email = new EmailAddress($"{username}@test.com");
        var hashedPassword = HashedPassword.Create(password, _context.PasswordHasher);
        var user = User.Register(userId, username, email, hashedPassword);
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
        var org = _context.Organizations[orgName];
        org.Deactivate();
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
