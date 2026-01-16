namespace ReadyStackGo.Infrastructure.DataAccess.Configurations;

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReadyStackGo.Domain.Deployment;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.Health;
using ReadyStackGo.Domain.Deployment.Observers;
using RuntimeConfig = ReadyStackGo.Domain.Deployment.RuntimeConfig;

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

        builder.Property(d => d.StackId)
            .HasMaxLength(200)
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

        builder.Property(d => d.OperationMode)
            .HasConversion(
                mode => mode.Value,
                value => OperationMode.FromValue(value))
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

        // Configure Variables as JSON column
        builder.Property(d => d.Variables)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null) ?? new())
            .HasColumnName("VariablesJson");

        // Configure MaintenanceObserverConfig as JSON column
        // This is a complex value object with polymorphic IObserverSettings
        var observerConfigOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new MaintenanceObserverConfigJsonConverter() }
        };
        builder.Property(d => d.MaintenanceObserverConfig)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, observerConfigOptions),
                v => string.IsNullOrEmpty(v) ? null : JsonSerializer.Deserialize<MaintenanceObserverConfig>(v, observerConfigOptions))
            .HasColumnName("MaintenanceObserverConfigJson");

        // Configure HealthCheckConfigs as JSON column (value objects, not entities)
        builder.Property<List<RuntimeConfig.ServiceHealthCheckConfig>>("_healthCheckConfigs")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<RuntimeConfig.ServiceHealthCheckConfig>>(v, (JsonSerializerOptions?)null) ?? new())
            .HasColumnName("HealthCheckConfigsJson");

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

        // Configure PhaseHistory as owned collection
        builder.OwnsMany(d => d.PhaseHistory, ph =>
        {
            ph.ToTable("DeploymentPhaseHistory");

            ph.WithOwner().HasForeignKey("DeploymentId");

            ph.Property<int>("Id")
                .ValueGeneratedOnAdd();

            ph.HasKey("Id");

            ph.Property(p => p.Phase)
                .IsRequired();

            ph.Property(p => p.Message)
                .HasMaxLength(500)
                .IsRequired();

            ph.Property(p => p.Timestamp)
                .IsRequired();
        });

        // Configure Snapshots relationship
        builder.HasMany(d => d.Snapshots)
            .WithOne()
            .HasForeignKey(s => s.DeploymentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes (no unique constraint on ProjectName - allows re-deploying same stack name)
        builder.HasIndex(d => d.EnvironmentId);

        builder.HasIndex(d => d.Status);
    }
}
