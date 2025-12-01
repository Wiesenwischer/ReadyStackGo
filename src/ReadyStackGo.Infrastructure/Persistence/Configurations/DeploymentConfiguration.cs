namespace ReadyStackGo.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReadyStackGo.Domain.IdentityAccess.ValueObjects;
using ReadyStackGo.Domain.StackManagement.Aggregates;
using ReadyStackGo.Domain.StackManagement.ValueObjects;

/// <summary>
/// EF Core configuration for Deployment aggregate.
/// </summary>
public class DeploymentConfiguration : IEntityTypeConfiguration<Deployment>
{
    public void Configure(EntityTypeBuilder<Deployment> builder)
    {
        builder.ToTable("Deployments");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id)
            .HasConversion(
                id => id.Value,
                value => new DeploymentId(value))
            .IsRequired();

        builder.Property(d => d.EnvironmentId)
            .HasConversion(
                id => id.Value,
                value => new EnvironmentId(value))
            .IsRequired();

        builder.Property(d => d.StackName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(d => d.StackVersion)
            .HasMaxLength(50);

        builder.Property(d => d.ProjectName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(d => d.Status)
            .IsRequired();

        builder.Property(d => d.ErrorMessage)
            .HasMaxLength(2000);

        builder.Property(d => d.CreatedAt)
            .IsRequired();

        builder.Property(d => d.CompletedAt);

        builder.Property(d => d.DeployedBy)
            .HasConversion(
                id => id.Value,
                value => new UserId(value))
            .IsRequired();

        builder.Property(d => d.Version)
            .IsConcurrencyToken();

        // Configure DeployedServices as owned collection
        builder.OwnsMany(d => d.Services, s =>
        {
            s.ToTable("DeployedServices");

            s.WithOwner().HasForeignKey("DeploymentId");

            s.Property(ds => ds.Id)
                .HasColumnName("Id")
                .IsRequired();

            s.HasKey(ds => ds.Id);

            s.Property(ds => ds.ServiceName)
                .HasMaxLength(100)
                .IsRequired();

            s.Property(ds => ds.ContainerId)
                .HasMaxLength(100);

            s.Property(ds => ds.ContainerName)
                .HasMaxLength(200);

            s.Property(ds => ds.Image)
                .HasMaxLength(500);

            s.Property(ds => ds.Status)
                .HasMaxLength(50)
                .IsRequired();
        });

        // Indexes
        builder.HasIndex(d => new { d.EnvironmentId, d.ProjectName })
            .IsUnique();

        builder.HasIndex(d => d.EnvironmentId);

        builder.HasIndex(d => d.Status);
    }
}
