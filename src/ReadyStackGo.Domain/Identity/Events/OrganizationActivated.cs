namespace ReadyStackGo.Domain.Identity.Events;

using ReadyStackGo.Domain.Common;
using ReadyStackGo.Domain.Identity.ValueObjects;

public sealed class OrganizationActivated : DomainEvent
{
    public OrganizationId OrganizationId { get; }

    public OrganizationActivated(OrganizationId organizationId)
    {
        OrganizationId = organizationId;
    }
}
