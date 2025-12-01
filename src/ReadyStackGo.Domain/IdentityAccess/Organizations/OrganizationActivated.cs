namespace ReadyStackGo.Domain.IdentityAccess.Organizations;

using ReadyStackGo.Domain.SharedKernel;


public sealed class OrganizationActivated : DomainEvent
{
    public OrganizationId OrganizationId { get; }

    public OrganizationActivated(OrganizationId organizationId)
    {
        OrganizationId = organizationId;
    }
}
