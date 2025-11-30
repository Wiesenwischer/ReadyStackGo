namespace ReadyStackGo.Domain.Identity.Events;

using ReadyStackGo.Domain.Common;
using ReadyStackGo.Domain.Identity.ValueObjects;

public sealed class TenantDeactivated : DomainEvent
{
    public TenantId TenantId { get; }

    public TenantDeactivated(TenantId tenantId)
    {
        TenantId = tenantId;
    }
}
