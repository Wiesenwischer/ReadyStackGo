namespace ReadyStackGo.Domain.Identity.Aggregates;

using ReadyStackGo.Domain.Common;
using ReadyStackGo.Domain.Identity.ValueObjects;
using ReadyStackGo.Domain.Identity.Events;

/// <summary>
/// Aggregate root representing a tenant (organization) in the system.
/// </summary>
public class Tenant : AggregateRoot<TenantId>
{
    public string Name { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public bool Active { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // For EF Core
    protected Tenant() { }

    private Tenant(TenantId id, string name, string description)
    {
        SelfAssertArgumentNotNull(id, "TenantId is required.");
        SelfAssertArgumentNotEmpty(name, "Organization name is required.");
        SelfAssertArgumentLength(name, 1, 100, "Organization name must be 100 characters or less.");
        SelfAssertArgumentNotEmpty(description, "Organization description is required.");
        SelfAssertArgumentLength(description, 1, 500, "Organization description must be 500 characters or less.");

        Id = id;
        Name = name;
        Description = description;
        Active = false;
        CreatedAt = DateTime.UtcNow;

        AddDomainEvent(new TenantProvisioned(Id, Name));
    }

    public static Tenant Provision(TenantId id, string name, string description)
    {
        return new Tenant(id, name, description);
    }

    public void Activate()
    {
        if (!Active)
        {
            Active = true;
            AddDomainEvent(new TenantActivated(Id));
        }
    }

    public void Deactivate()
    {
        if (Active)
        {
            Active = false;
            AddDomainEvent(new TenantDeactivated(Id));
        }
    }

    public void UpdateDescription(string description)
    {
        SelfAssertArgumentNotEmpty(description, "Organization description is required.");
        SelfAssertArgumentLength(description, 1, 500, "Organization description must be 500 characters or less.");

        Description = description;
    }

    public override string ToString() =>
        $"Tenant [id={Id}, name={Name}, active={Active}]";
}
