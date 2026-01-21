namespace ReadyStackGo.Infrastructure.DataAccess.Configurations;

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReadyStackGo.Domain.Deployment;
using ReadyStackGo.Domain.Deployment.Registries;

/// <summary>
/// EF Core configuration for Registry aggregate.
/// </summary>
public class RegistryConfiguration : IEntityTypeConfiguration<Registry>
{
    public void Configure(EntityTypeBuilder<Registry> builder)
    {
        builder.ToTable("Registries");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasConversion(
                id => id.Value,
                value => new RegistryId(value))
            .IsRequired();

        builder.Property(r => r.OrganizationId)
            .HasConversion(
                id => id.Value,
                value => new OrganizationId(value))
            .IsRequired();

        builder.Property(r => r.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(r => r.Url)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(r => r.Username)
            .HasMaxLength(255);

        builder.Property(r => r.Password)
            .HasMaxLength(1000);

        builder.Property(r => r.IsDefault)
            .IsRequired();

        builder.Property(r => r.CreatedAt)
            .IsRequired();

        builder.Property(r => r.UpdatedAt);

        // Store ImagePatterns as JSON array in a single column
        // Use backing field directly since EF Core needs to map to a mutable list
        builder.Property<List<string>>("_imagePatterns")
            .HasField("_imagePatterns")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasConversion(
                patterns => JsonSerializer.Serialize(patterns ?? new List<string>(), (JsonSerializerOptions?)null),
                json => string.IsNullOrEmpty(json)
                    ? new List<string>()
                    : JsonSerializer.Deserialize<List<string>>(json, (JsonSerializerOptions?)null) ?? new List<string>())
            .HasColumnName("ImagePatterns")
            .HasMaxLength(4000);

        // Ignore the read-only property
        builder.Ignore(r => r.ImagePatterns);

        // Indexes
        builder.HasIndex(r => r.OrganizationId);
        builder.HasIndex(r => new { r.OrganizationId, r.Name }).IsUnique();
        builder.HasIndex(r => new { r.OrganizationId, r.IsDefault })
            .HasFilter("[IsDefault] = 1");
    }
}
