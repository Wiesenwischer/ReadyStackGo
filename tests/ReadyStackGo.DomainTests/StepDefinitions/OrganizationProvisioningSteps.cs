namespace ReadyStackGo.DomainTests.StepDefinitions;

using FluentAssertions;
using Reqnroll;
using ReadyStackGo.DomainTests.Support;
using ReadyStackGo.Domain.IdentityAccess.Organizations;
using ReadyStackGo.Domain.IdentityAccess.Roles;
using ReadyStackGo.Domain.IdentityAccess.Users;

[Binding]
public class OrganizationProvisioningSteps
{
    private readonly TestContext _context;

    public OrganizationProvisioningSteps(TestContext context)
    {
        _context = context;
    }

    [Given(@"the system has no tenants configured")]
    public void GivenTheSystemHasNoTenantsConfigured()
    {
        _context.Reset();
    }

    [Given(@"organization ""(.*)"" exists")]
    public void GivenOrganizationExists(string orgName)
    {
        var organizationId = _context.OrganizationRepository.NextIdentity();
        var organization = Organization.Provision(organizationId, orgName, "Existing organization");
        organization.Activate();
        _context.OrganizationRepository.Add(organization);
        _context.Organizations[orgName] = organization;
    }

    [Given(@"user ""(.*)"" exists with ""(.*)"" role globally")]
    public void GivenUserExistsWithRoleGlobally(string username, string roleName)
    {
        var userId = _context.UserRepository.NextIdentity();
        var email = new EmailAddress($"{username}@test.com");
        var hashedPassword = HashedPassword.Create("TestPass123!", _context.PasswordHasher);
        var user = User.Register(userId, username, email, hashedPassword);

        var roleId = new RoleId(roleName);
        user.AssignRole(RoleAssignment.Global(roleId));

        _context.UserRepository.Add(user);
        _context.Users[username] = user;
        _context.CurrentUser = user;
    }

    [When(@"user ""(.*)"" provisions organization ""(.*)"" with description ""(.*)""")]
    public void WhenUserProvisionsOrganizationWithDescription(string username, string orgName, string description)
    {
        try
        {
            var user = _context.Users[username];
            var organization = _context.OrganizationProvisioningService.ProvisionOrganization(
                orgName,
                description,
                user);

            _context.CurrentOrganization = organization;
            _context.Organizations[orgName] = organization;
        }
        catch (Exception ex)
        {
            _context.LastException = ex;
        }
    }

    [When(@"I provision organization ""(.*)"" with description ""(.*)""")]
    public void WhenIProvisionOrganizationWithDescription(string orgName, string description)
    {
        try
        {
            var organizationId = _context.OrganizationRepository.NextIdentity();
            _context.CurrentOrganization = Organization.Provision(organizationId, orgName, description);
            _context.CurrentOrganization.Activate();
            _context.Organizations[orgName] = _context.CurrentOrganization;
        }
        catch (Exception ex)
        {
            _context.LastException = ex;
        }
    }

    [When(@"I create admin user ""(.*)"" with email ""(.*)"" and password ""(.*)""")]
    public void WhenICreateAdminUserWithEmailAndPassword(string username, string email, string password)
    {
        try
        {
            if (_context.CurrentOrganization == null)
            {
                throw new InvalidOperationException("No organization provisioned");
            }

            // Create user first
            var userId = _context.UserRepository.NextIdentity();
            var emailAddress = new EmailAddress(email);
            var hashedPassword = HashedPassword.Create(password, _context.PasswordHasher);
            var user = User.Register(userId, username, emailAddress, hashedPassword);
            _context.UserRepository.Add(user);

            // Provision organization with the user as owner
            var organization = _context.OrganizationProvisioningService.ProvisionOrganization(
                _context.CurrentOrganization.Name,
                _context.CurrentOrganization.Description,
                user);

            _context.CurrentOrganization = organization;
            _context.CurrentUser = user;
            _context.Users[username] = user;
            _context.Organizations[organization.Name] = organization;
        }
        catch (Exception ex)
        {
            _context.LastException = ex;
        }
    }

    [When(@"I try to provision organization ""(.*)""")]
    public void WhenITryToProvisionOrganization(string orgName)
    {
        try
        {
            // Check for duplicate name (same as OrganizationProvisioningService does)
            var existing = _context.OrganizationRepository.GetByName(orgName);
            if (existing != null)
            {
                throw new InvalidOperationException("Organization name already exists.");
            }

            var organizationId = _context.OrganizationRepository.NextIdentity();
            _context.CurrentOrganization = Organization.Provision(organizationId, orgName, "Test description");
        }
        catch (Exception ex)
        {
            _context.LastException = ex;
        }
    }

    [When(@"I try to create admin user ""(.*)"" with password ""(.*)""")]
    public void WhenITryToCreateAdminUserWithPassword(string username, string password)
    {
        try
        {
            if (_context.CurrentOrganization == null)
            {
                throw new InvalidOperationException("No organization provisioned");
            }

            // Create user first
            var userId = _context.UserRepository.NextIdentity();
            var email = new EmailAddress($"{username}@test.com");
            var hashedPassword = HashedPassword.Create(password, _context.PasswordHasher);
            var user = User.Register(userId, username, email, hashedPassword);
            _context.UserRepository.Add(user);

            // Provision organization with the user as owner
            var organization = _context.OrganizationProvisioningService.ProvisionOrganization(
                _context.CurrentOrganization.Name,
                _context.CurrentOrganization.Description,
                user);

            _context.CurrentUser = user;
            _context.Organizations[organization.Name] = organization;
        }
        catch (Exception ex)
        {
            _context.LastException = ex;
        }
    }

    [When(@"I try to create admin user ""(.*)"" with email ""(.*)"" and password ""(.*)""")]
    public void WhenITryToCreateAdminUserWithEmailAndPassword(string username, string email, string password)
    {
        try
        {
            if (_context.CurrentOrganization == null)
            {
                throw new InvalidOperationException("No organization provisioned");
            }

            // Create user first
            var userId = _context.UserRepository.NextIdentity();
            var emailAddress = new EmailAddress(email);
            var hashedPassword = HashedPassword.Create(password, _context.PasswordHasher);
            var user = User.Register(userId, username, emailAddress, hashedPassword);
            _context.UserRepository.Add(user);

            // Provision organization with the user as owner
            var organization = _context.OrganizationProvisioningService.ProvisionOrganization(
                _context.CurrentOrganization.Name,
                _context.CurrentOrganization.Description,
                user);

            _context.CurrentUser = user;
            _context.Organizations[organization.Name] = organization;
        }
        catch (Exception ex)
        {
            _context.LastException = ex;
        }
    }

    [When(@"I try to provision organization with empty name")]
    public void WhenITryToProvisionOrganizationWithEmptyName()
    {
        try
        {
            var organizationId = _context.OrganizationRepository.NextIdentity();
            _context.CurrentOrganization = Organization.Provision(organizationId, "", "Test description");
        }
        catch (Exception ex)
        {
            _context.LastException = ex;
        }
    }

    [Then(@"the organization ""(.*)"" should exist and be active")]
    public void ThenTheOrganizationShouldExistAndBeActive(string orgName)
    {
        _context.LastException.Should().BeNull(_context.LastException?.Message);
        var organization = _context.OrganizationRepository.GetByName(orgName);
        organization.Should().NotBeNull();
        organization!.Active.Should().BeTrue();
    }

    [Then(@"user ""(.*)"" should exist with email ""(.*)""")]
    public void ThenUserShouldExistWithEmail(string username, string email)
    {
        var user = _context.UserRepository.FindByUsername(username);
        user.Should().NotBeNull();
        user!.Email.Value.Should().Be(email.ToLowerInvariant());
    }

    [Then(@"user ""(.*)"" should have role ""(.*)"" with global scope")]
    public void ThenUserShouldHaveRoleWithGlobalScope(string username, string roleName)
    {
        var user = _context.UserRepository.FindByUsername(username);
        user.Should().NotBeNull();

        var roleId = new RoleId(roleName);
        user!.HasRoleWithScope(roleId, ScopeType.Global, null).Should().BeTrue(
            $"User '{username}' should have role '{roleName}' with global scope");
    }

    [Then(@"user ""(.*)"" should have role ""(.*)"" for organization ""(.*)""")]
    public void ThenUserShouldHaveRoleForOrganization(string username, string roleName, string orgName)
    {
        var user = _context.UserRepository.FindByUsername(username);
        user.Should().NotBeNull();

        var organization = _context.OrganizationRepository.GetByName(orgName);
        organization.Should().NotBeNull();

        var roleId = new RoleId(roleName);
        user!.HasRoleWithScope(roleId, ScopeType.Organization, organization!.Id.Value.ToString()).Should().BeTrue(
            $"User '{username}' should have role '{roleName}' for organization '{orgName}'");
    }

    [Then(@"the provisioning should fail with error ""(.*)""")]
    public void ThenTheProvisioningShouldFailWithError(string expectedError)
    {
        _context.LastException.Should().NotBeNull("Expected an exception to be thrown");
        _context.LastException!.Message.Should().Contain(expectedError);
    }
}
