namespace ReadyStackGo.Domain.Identity.Events;

using ReadyStackGo.Domain.Common;
using ReadyStackGo.Domain.Identity.ValueObjects;

public sealed class OrganizationDeactivated : DomainEvent
{
    public OrganizationId OrganizationId { get; }

    public OrganizationDeactivated(OrganizationId organizationId)
    {
        OrganizationId = organizationId;
    }
}
