namespace ReadyStackGo.Infrastructure.DataAccess.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReadyStackGo.Domain.IdentityAccess.Organizations;
using ReadyStackGo.Domain.IdentityAccess.Roles;
using ReadyStackGo.Domain.IdentityAccess.Users;
using ReadyStackGo.Domain.IdentityAccess.Organizations;
using ReadyStackGo.Domain.IdentityAccess.Users;

public class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("Organizations");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasConversion(
                id => id.Value.ToString(),
                value => new OrganizationId(Guid.Parse(value)))
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

        // Owner ID - optional foreign key to Users
        builder.Property(t => t.OwnerId)
            .HasConversion(
                id => id != null ? id.Value.ToString() : null,
                value => value != null ? new UserId(Guid.Parse(value)) : null)
            .HasColumnName("OwnerId")
            .HasMaxLength(36);

        // Ignore domain events - they're not persisted
        builder.Ignore(t => t.DomainEvents);

        // Ignore memberships for now - they're in-memory only
        // TODO: Add proper owned collection configuration when persistence is needed
        builder.Ignore(t => t.Memberships);
    }
}
