namespace ReadyStackGo.Domain.IdentityAccess.Organizations;

using ReadyStackGo.Domain.SharedKernel;



/// <summary>
/// Aggregate root representing a organization (organization) in the system.
/// </summary>
public class Organization : AggregateRoot<OrganizationId>
{
    public string Name { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public bool Active { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // For EF Core
    protected Organization() { }

    private Organization(OrganizationId id, string name, string description)
    {
        SelfAssertArgumentNotNull(id, "OrganizationId is required.");
        SelfAssertArgumentNotEmpty(name, "Organization name is required.");
        SelfAssertArgumentLength(name, 1, 100, "Organization name must be 100 characters or less.");
        SelfAssertArgumentNotEmpty(description, "Organization description is required.");
        SelfAssertArgumentLength(description, 1, 500, "Organization description must be 500 characters or less.");

        Id = id;
        Name = name;
        Description = description;
        Active = false;
        CreatedAt = DateTime.UtcNow;

        AddDomainEvent(new OrganizationProvisioned(Id, Name));
    }

    public static Organization Provision(OrganizationId id, string name, string description)
    {
        return new Organization(id, name, description);
    }

    public void Activate()
    {
        if (!Active)
        {
            Active = true;
            AddDomainEvent(new OrganizationActivated(Id));
        }
    }

    public void Deactivate()
    {
        if (Active)
        {
            Active = false;
            AddDomainEvent(new OrganizationDeactivated(Id));
        }
    }

    public void UpdateDescription(string description)
    {
        SelfAssertArgumentNotEmpty(description, "Organization description is required.");
        SelfAssertArgumentLength(description, 1, 500, "Organization description must be 500 characters or less.");

        Description = description;
    }

    public override string ToString() =>
        $"Organization [id={Id}, name={Name}, active={Active}]";
}
