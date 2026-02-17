namespace ReadyStackGo.Infrastructure.DataAccess.Configurations;

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReadyStackGo.Domain.Deployment;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.ProductDeployments;

/// <summary>
/// EF Core configuration for ProductDeployment aggregate.
/// </summary>
public class ProductDeploymentConfiguration : IEntityTypeConfiguration<ProductDeployment>
{
    public void Configure(EntityTypeBuilder<ProductDeployment> builder)
    {
        builder.ToTable("ProductDeployments");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id)
            .HasConversion(
                id => id.Value,
                value => new ProductDeploymentId(value))
            .IsRequired();

        builder.Property(d => d.EnvironmentId)
            .HasConversion(
                id => id.Value,
                value => new EnvironmentId(value))
            .IsRequired();

        builder.Property(d => d.ProductGroupId)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(d => d.ProductId)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(d => d.ProductName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(d => d.ProductDisplayName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(d => d.ProductVersion)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(d => d.DeployedBy)
            .HasConversion(
                id => id.Value,
                value => new UserId(value))
            .IsRequired();

        builder.Property(d => d.Status)
            .IsRequired();

        builder.Property(d => d.CreatedAt)
            .IsRequired();

        builder.Property(d => d.CompletedAt);

        builder.Property(d => d.ErrorMessage)
            .HasMaxLength(2000);

        builder.Property(d => d.ContinueOnError)
            .IsRequired();

        // Upgrade tracking
        builder.Property(d => d.PreviousVersion)
            .HasMaxLength(50);
        builder.Property(d => d.LastUpgradedAt);
        builder.Property(d => d.UpgradeCount)
            .HasDefaultValue(0);

        builder.Property(d => d.Version)
            .IsConcurrencyToken();

        // Configure SharedVariables as JSON column
        builder.Property(d => d.SharedVariables)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null) ?? new())
            .HasColumnName("SharedVariablesJson");

        // Configure PhaseHistory as JSON column
        builder.Property(d => d.PhaseHistory)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<ProductDeploymentPhaseRecord>>(v, (JsonSerializerOptions?)null) ?? new())
            .HasColumnName("PhaseHistoryJson")
            .Metadata.SetPropertyAccessMode(PropertyAccessMode.Field);

        // Configure ProductStackDeployments as owned collection
        builder.OwnsMany(d => d.Stacks, s =>
        {
            s.ToTable("ProductStackDeployments");

            s.WithOwner().HasForeignKey("ProductDeploymentId");

            s.Property<int>("Id")
                .ValueGeneratedOnAdd();

            s.HasKey("Id");

            s.Property(ps => ps.StackName)
                .HasMaxLength(200)
                .IsRequired();

            s.Property(ps => ps.StackDisplayName)
                .HasMaxLength(200)
                .IsRequired();

            s.Property(ps => ps.StackId)
                .HasMaxLength(500)
                .IsRequired();

            s.Property(ps => ps.DeploymentId)
                .HasConversion(
                    id => id == null ? (Guid?)null : id.Value,
                    value => value.HasValue ? new DeploymentId(value.Value) : null);

            s.Property(ps => ps.DeploymentStackName)
                .HasMaxLength(200);

            s.Property(ps => ps.Status)
                .IsRequired();

            s.Property(ps => ps.StartedAt);
            s.Property(ps => ps.CompletedAt);

            s.Property(ps => ps.ErrorMessage)
                .HasMaxLength(2000);

            s.Property(ps => ps.Order)
                .IsRequired();

            s.Property(ps => ps.ServiceCount)
                .IsRequired();

            s.Property(ps => ps.IsNewInUpgrade)
                .HasDefaultValue(false);

            // Configure Variables as JSON column
            s.Property(ps => ps.Variables)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null) ?? new())
                .HasColumnName("VariablesJson");

            s.HasIndex("ProductDeploymentId");
            s.HasIndex(ps => ps.DeploymentId);
        });

        // Indexes
        builder.HasIndex(d => d.EnvironmentId);
        builder.HasIndex(d => d.ProductGroupId);
        builder.HasIndex(d => d.Status);
    }
}
