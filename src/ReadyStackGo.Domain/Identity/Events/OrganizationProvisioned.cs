namespace ReadyStackGo.Domain.Identity.Events;

using ReadyStackGo.Domain.Common;
using ReadyStackGo.Domain.Identity.ValueObjects;

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
