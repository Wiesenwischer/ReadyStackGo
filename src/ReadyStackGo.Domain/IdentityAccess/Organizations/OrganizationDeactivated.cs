namespace ReadyStackGo.Domain.IdentityAccess.Organizations;

using ReadyStackGo.Domain.SharedKernel;


public sealed class OrganizationDeactivated : DomainEvent
{
    public OrganizationId OrganizationId { get; }

    public OrganizationDeactivated(OrganizationId organizationId)
    {
        OrganizationId = organizationId;
    }
}
