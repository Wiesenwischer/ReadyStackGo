namespace ReadyStackGo.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReadyStackGo.Domain.Identity.Aggregates;
using ReadyStackGo.Domain.Identity.ValueObjects;
using ReadyStackGo.Domain.Access.ValueObjects;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id)
            .HasConversion(
                id => id.Value.ToString(),
                value => new UserId(Guid.Parse(value)))
            .HasColumnName("Id")
            .HasMaxLength(36);

        builder.Property(u => u.TenantId)
            .HasConversion(
                id => id.Value.ToString(),
                value => new TenantId(Guid.Parse(value)))
            .IsRequired()
            .HasMaxLength(36);

        builder.HasIndex(u => u.TenantId);

        builder.Property(u => u.Username)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(u => new { u.TenantId, u.Username })
            .IsUnique();

        // Email as owned value object
        builder.OwnsOne(u => u.Email, email =>
        {
            email.Property(e => e.Value)
                .HasColumnName("Email")
                .IsRequired()
                .HasMaxLength(254);

            email.HasIndex(e => e.Value)
                .IsUnique();
        });

        // Password as owned value object
        builder.OwnsOne(u => u.Password, password =>
        {
            password.Property(p => p.Hash)
                .HasColumnName("PasswordHash")
                .IsRequired()
                .HasMaxLength(256);
        });

        // Enablement as owned value object
        builder.OwnsOne(u => u.Enablement, enablement =>
        {
            enablement.Property(e => e.Enabled)
                .HasColumnName("Enabled")
                .IsRequired();

            enablement.Property(e => e.StartDate)
                .HasColumnName("EnablementStartDate");

            enablement.Property(e => e.EndDate)
                .HasColumnName("EnablementEndDate");
        });

        builder.Property(u => u.CreatedAt)
            .IsRequired();

        builder.Property(u => u.Version)
            .IsConcurrencyToken();

        // Role assignments stored in separate table
        builder.OwnsMany(u => u.RoleAssignments, ra =>
        {
            ra.ToTable("UserRoles");

            ra.WithOwner().HasForeignKey("UserId");

            ra.Property<Guid>("Id")
                .ValueGeneratedOnAdd();
            ra.HasKey("Id");

            ra.Property(r => r.RoleId)
                .HasConversion(
                    id => id.Value,
                    value => new RoleId(value))
                .HasColumnName("RoleId")
                .IsRequired()
                .HasMaxLength(50);

            ra.Property(r => r.ScopeType)
                .HasColumnName("ScopeType")
                .IsRequired();

            ra.Property(r => r.ScopeId)
                .HasColumnName("ScopeId")
                .HasMaxLength(36);

            ra.Property(r => r.AssignedAt)
                .HasColumnName("AssignedAt")
                .IsRequired();

            ra.HasIndex("UserId", "RoleId", "ScopeType", "ScopeId")
                .IsUnique();
        });

        // Ignore domain events - they're not persisted
        builder.Ignore(u => u.DomainEvents);
    }
}
