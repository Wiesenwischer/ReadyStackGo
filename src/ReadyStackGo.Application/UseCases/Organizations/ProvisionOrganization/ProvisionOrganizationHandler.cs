namespace ReadyStackGo.Application.UseCases.Organizations.ProvisionOrganization;

using MediatR;
using ReadyStackGo.Domain.IdentityAccess.Organizations;
using ReadyStackGo.Domain.IdentityAccess.Users;
using ReadyStackGo.Domain.IdentityAccess.Organizations;
using ReadyStackGo.Domain.IdentityAccess.Roles;
using ReadyStackGo.Domain.IdentityAccess.Users;
using ReadyStackGo.Domain.IdentityAccess.Organizations;
using ReadyStackGo.Domain.IdentityAccess.Users;

public class ProvisionOrganizationHandler : IRequestHandler<ProvisionOrganizationCommand, ProvisionOrganizationResult>
{
    private readonly OrganizationProvisioningService _provisioningService;
    private readonly IUserRepository _userRepository;

    public ProvisionOrganizationHandler(
        OrganizationProvisioningService provisioningService,
        IUserRepository userRepository)
    {
        _provisioningService = provisioningService;
        _userRepository = userRepository;
    }

    public Task<ProvisionOrganizationResult> Handle(ProvisionOrganizationCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Find the system admin to be the owner of the initial organization
            var systemAdmin = _userRepository.GetAll()
                .FirstOrDefault(u => u.RoleAssignments.Any(r => r.RoleId == RoleId.SystemAdmin));

            if (systemAdmin == null)
            {
                return Task.FromResult(new ProvisionOrganizationResult(
                    false,
                    ErrorMessage: "System administrator must be created first"));
            }

            var organization = _provisioningService.ProvisionOrganization(
                request.Name,
                request.Description,
                systemAdmin);

            return Task.FromResult(new ProvisionOrganizationResult(true, organization.Id.ToString()));
        }
        catch (InvalidOperationException ex)
        {
            return Task.FromResult(new ProvisionOrganizationResult(false, ErrorMessage: ex.Message));
        }
        catch (ArgumentException ex)
        {
            return Task.FromResult(new ProvisionOrganizationResult(false, ErrorMessage: ex.Message));
        }
    }
}
