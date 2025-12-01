namespace ReadyStackGo.Domain.IdentityAccess.Organizations;

using ReadyStackGo.Domain.SharedKernel;


public sealed class OrganizationProvisioned : DomainEvent
{
    public OrganizationId OrganizationId { get; }
    public string OrganizationName { get; }

    public OrganizationProvisioned(OrganizationId organizationId, string organizationName)
    {
        OrganizationId = organizationId;
        OrganizationName = organizationName;
    }
}
