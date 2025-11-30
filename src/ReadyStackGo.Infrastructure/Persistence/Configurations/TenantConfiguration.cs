namespace ReadyStackGo.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReadyStackGo.Domain.Identity.Aggregates;
using ReadyStackGo.Domain.Identity.ValueObjects;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenants");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasConversion(
                id => id.Value.ToString(),
                value => new TenantId(Guid.Parse(value)))
            .HasColumnName("Id")
            .HasMaxLength(36);

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(t => t.Name)
            .IsUnique();

        builder.Property(t => t.Description)
            .HasMaxLength(500);

        builder.Property(t => t.Active)
            .IsRequired();

        builder.Property(t => t.CreatedAt)
            .IsRequired();

        builder.Property(t => t.Version)
            .IsConcurrencyToken();

        // Ignore domain events - they're not persisted
        builder.Ignore(t => t.DomainEvents);
    }
}
