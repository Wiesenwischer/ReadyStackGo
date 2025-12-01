namespace ReadyStackGo.Domain.IdentityAccess.Events;

using ReadyStackGo.Domain.Common;
using ReadyStackGo.Domain.IdentityAccess.ValueObjects;

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
