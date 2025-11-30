namespace ReadyStackGo.Domain.Identity.Events;

using ReadyStackGo.Domain.Common;
using ReadyStackGo.Domain.Identity.ValueObjects;

public sealed class TenantProvisioned : DomainEvent
{
    public TenantId TenantId { get; }
    public string TenantName { get; }

    public TenantProvisioned(TenantId tenantId, string tenantName)
    {
        TenantId = tenantId;
        TenantName = tenantName;
    }
}
