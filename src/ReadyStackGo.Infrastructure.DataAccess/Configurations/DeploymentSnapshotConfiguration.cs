namespace ReadyStackGo.Infrastructure.DataAccess.Configurations;

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReadyStackGo.Domain.Deployment.Deployments;

/// <summary>
/// EF Core configuration for DeploymentSnapshot entity.
/// </summary>
public class DeploymentSnapshotConfiguration : IEntityTypeConfiguration<DeploymentSnapshot>
{
    public void Configure(EntityTypeBuilder<DeploymentSnapshot> builder)
    {
        builder.ToTable("DeploymentSnapshots");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasConversion(
                id => id.Value,
                value => new DeploymentSnapshotId(value))
            .IsRequired();

        builder.Property(s => s.DeploymentId)
            .HasConversion(
                id => id.Value,
                value => new DeploymentId(value))
            .IsRequired();

        builder.Property(s => s.StackVersion)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(s => s.CreatedAt)
            .IsRequired();

        builder.Property(s => s.Description)
            .HasMaxLength(500);

        // Configure Variables as JSON column
        builder.Property(s => s.Variables)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null) ?? new())
            .HasColumnName("VariablesJson")
            .IsRequired();

        // Configure Services as JSON column
        builder.Property(s => s.Services)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<ServiceSnapshot>>(v, (JsonSerializerOptions?)null) ?? new())
            .HasColumnName("ServicesJson")
            .IsRequired();

        // Index for faster lookups
        builder.HasIndex(s => s.DeploymentId);
        builder.HasIndex(s => s.CreatedAt);
    }
}
