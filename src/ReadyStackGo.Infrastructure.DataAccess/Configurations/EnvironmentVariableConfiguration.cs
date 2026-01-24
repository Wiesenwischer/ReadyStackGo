namespace ReadyStackGo.Infrastructure.DataAccess.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReadyStackGo.Domain.Deployment.Environments;

/// <summary>
/// EF Core configuration for EnvironmentVariable entity.
/// </summary>
public class EnvironmentVariableConfiguration : IEntityTypeConfiguration<EnvironmentVariable>
{
    public void Configure(EntityTypeBuilder<EnvironmentVariable> builder)
    {
        builder.ToTable("EnvironmentVariables");

        builder.HasKey(ev => ev.Id);

        builder.Property(ev => ev.Id)
            .HasConversion(
                id => id.Value,
                value => new EnvironmentVariableId(value))
            .IsRequired();

        builder.Property(ev => ev.EnvironmentId)
            .HasConversion(
                id => id.Value,
                value => new EnvironmentId(value))
            .IsRequired();

        builder.Property(ev => ev.Key)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(ev => ev.Value)
            .IsRequired();

        builder.Property(ev => ev.IsEncrypted)
            .IsRequired();

        builder.Property(ev => ev.CreatedAt)
            .IsRequired();

        builder.Property(ev => ev.UpdatedAt)
            .IsRequired();

        // Indexes
        builder.HasIndex(ev => new { ev.EnvironmentId, ev.Key })
            .IsUnique();

        builder.HasIndex(ev => ev.EnvironmentId);
    }
}
