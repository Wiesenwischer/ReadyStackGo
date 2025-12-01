namespace ReadyStackGo.Domain.IdentityAccess.Events;

using ReadyStackGo.Domain.Common;
using ReadyStackGo.Domain.IdentityAccess.ValueObjects;

public sealed class OrganizationActivated : DomainEvent
{
    public OrganizationId OrganizationId { get; }

    public OrganizationActivated(OrganizationId organizationId)
    {
        OrganizationId = organizationId;
    }
}
