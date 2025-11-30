namespace ReadyStackGo.Domain.Identity.Events;

using ReadyStackGo.Domain.Common;
using ReadyStackGo.Domain.Identity.ValueObjects;

public sealed class TenantActivated : DomainEvent
{
    public TenantId TenantId { get; }

    public TenantActivated(TenantId tenantId)
    {
        TenantId = tenantId;
    }
}
