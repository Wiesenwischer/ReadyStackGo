namespace ReadyStackGo.Domain.IdentityAccess.Events;

using ReadyStackGo.Domain.Common;
using ReadyStackGo.Domain.IdentityAccess.ValueObjects;

public sealed class OrganizationDeactivated : DomainEvent
{
    public OrganizationId OrganizationId { get; }

    public OrganizationDeactivated(OrganizationId organizationId)
    {
        OrganizationId = organizationId;
    }
}
