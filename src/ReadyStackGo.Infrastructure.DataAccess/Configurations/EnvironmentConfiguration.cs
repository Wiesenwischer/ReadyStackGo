namespace ReadyStackGo.Infrastructure.DataAccess.Configurations;

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReadyStackGo.Domain.Deployment;
using ReadyStackGo.Domain.Deployment.Environments;

/// <summary>
/// EF Core configuration for Environment aggregate.
/// </summary>
public class EnvironmentConfiguration : IEntityTypeConfiguration<Environment>
{
    public void Configure(EntityTypeBuilder<Environment> builder)
    {
        builder.ToTable("Environments");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasConversion(
                id => id.Value,
                value => new EnvironmentId(value))
            .IsRequired();

        builder.Property(e => e.OrganizationId)
            .HasConversion(
                id => id.Value,
                value => new OrganizationId(value))
            .IsRequired();

        builder.Property(e => e.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.Description)
            .HasMaxLength(500);

        builder.Property(e => e.Type)
            .IsRequired();

        // ConnectionConfig as JSON column (polymorphic)
        var connectionConfigOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new ConnectionConfigJsonConverter() }
        };
        builder.Property(e => e.ConnectionConfig)
            .HasConversion(
                v => JsonSerializer.Serialize(v, connectionConfigOptions),
                v => JsonSerializer.Deserialize<ConnectionConfig>(v, connectionConfigOptions)!)
            .HasColumnName("ConnectionConfigJson")
            .IsRequired();

        builder.Property(e => e.IsDefault)
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .IsRequired();

        builder.Property(e => e.UpdatedAt);

        builder.Property(e => e.Version)
            .IsConcurrencyToken();

        // Indexes
        builder.HasIndex(e => new { e.OrganizationId, e.Name })
            .IsUnique();

        builder.HasIndex(e => e.OrganizationId);
    }
}
