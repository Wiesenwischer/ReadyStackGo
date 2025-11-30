namespace ReadyStackGo.DomainTests.StepDefinitions;

using FluentAssertions;
using Reqnroll;
using ReadyStackGo.DomainTests.Support;
using ReadyStackGo.Domain.Identity.Aggregates;
using ReadyStackGo.Domain.Identity.ValueObjects;
using ReadyStackGo.Domain.Access.ValueObjects;

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
        var tenantId = _context.TenantRepository.NextIdentity();
        var tenant = Tenant.Provision(tenantId, orgName, "Existing organization");
        tenant.Activate();
        _context.TenantRepository.Add(tenant);
        _context.Tenants[orgName] = tenant;
    }

    [When(@"I provision organization ""(.*)"" with description ""(.*)""")]
    public void WhenIProvisionOrganizationWithDescription(string orgName, string description)
    {
        try
        {
            var tenantId = _context.TenantRepository.NextIdentity();
            _context.CurrentTenant = Tenant.Provision(tenantId, orgName, description);
            _context.CurrentTenant.Activate();
            _context.Tenants[orgName] = _context.CurrentTenant;
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
            if (_context.CurrentTenant == null)
            {
                throw new InvalidOperationException("No tenant provisioned");
            }

            var (tenant, user) = _context.TenantProvisioningService.ProvisionTenant(
                _context.CurrentTenant.Name,
                _context.CurrentTenant.Description,
                username,
                email,
                password);

            _context.CurrentTenant = tenant;
            _context.CurrentUser = user;
            _context.Users[username] = user;
            _context.Tenants[tenant.Name] = tenant;
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
            // Check for duplicate name (same as TenantProvisioningService does)
            var existing = _context.TenantRepository.GetByName(orgName);
            if (existing != null)
            {
                throw new InvalidOperationException("Organization name already exists.");
            }

            var tenantId = _context.TenantRepository.NextIdentity();
            _context.CurrentTenant = Tenant.Provision(tenantId, orgName, "Test description");
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
            if (_context.CurrentTenant == null)
            {
                throw new InvalidOperationException("No tenant provisioned");
            }

            var (tenant, user) = _context.TenantProvisioningService.ProvisionTenant(
                _context.CurrentTenant.Name,
                _context.CurrentTenant.Description,
                username,
                $"{username}@test.com",
                password);

            _context.CurrentUser = user;
            _context.Tenants[tenant.Name] = tenant;
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
            if (_context.CurrentTenant == null)
            {
                throw new InvalidOperationException("No tenant provisioned");
            }

            var (tenant, user) = _context.TenantProvisioningService.ProvisionTenant(
                _context.CurrentTenant.Name,
                _context.CurrentTenant.Description,
                username,
                email,
                password);

            _context.CurrentUser = user;
            _context.Tenants[tenant.Name] = tenant;
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
            var tenantId = _context.TenantRepository.NextIdentity();
            _context.CurrentTenant = Tenant.Provision(tenantId, "", "Test description");
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
        var tenant = _context.TenantRepository.GetByName(orgName);
        tenant.Should().NotBeNull();
        tenant!.Active.Should().BeTrue();
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

        var tenant = _context.TenantRepository.GetByName(orgName);
        tenant.Should().NotBeNull();

        var roleId = new RoleId(roleName);
        user!.HasRoleWithScope(roleId, ScopeType.Organization, tenant!.Id.Value.ToString()).Should().BeTrue(
            $"User '{username}' should have role '{roleName}' for organization '{orgName}'");
    }

    [Then(@"the provisioning should fail with error ""(.*)""")]
    public void ThenTheProvisioningShouldFailWithError(string expectedError)
    {
        _context.LastException.Should().NotBeNull("Expected an exception to be thrown");
        _context.LastException!.Message.Should().Contain(expectedError);
    }
}
