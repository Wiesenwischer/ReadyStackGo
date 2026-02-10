namespace ReadyStackGo.Infrastructure.DataAccess.Configurations;

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReadyStackGo.Domain.IdentityAccess.ApiKeys;
using ReadyStackGo.Domain.IdentityAccess.Organizations;

/// <summary>
/// EF Core configuration for ApiKey aggregate.
/// </summary>
public class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
    public void Configure(EntityTypeBuilder<ApiKey> builder)
    {
        builder.ToTable("ApiKeys");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasConversion(
                id => id.Value,
                value => new ApiKeyId(value))
            .IsRequired();

        builder.Property(a => a.OrganizationId)
            .HasConversion(
                id => id.Value,
                value => new OrganizationId(value))
            .IsRequired();

        builder.Property(a => a.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(a => a.KeyHash)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(a => a.KeyPrefix)
            .HasMaxLength(12)
            .IsRequired();

        builder.Property(a => a.EnvironmentId);

        builder.Property(a => a.CreatedAt)
            .IsRequired();

        builder.Property(a => a.LastUsedAt);
        builder.Property(a => a.ExpiresAt);
        builder.Property(a => a.IsRevoked)
            .IsRequired();
        builder.Property(a => a.RevokedAt);

        builder.Property(a => a.RevokedReason)
            .HasMaxLength(500);

        // Store Permissions as JSON array in a single column
        builder.Property<List<string>>("_permissions")
            .HasField("_permissions")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasConversion(
                permissions => JsonSerializer.Serialize(permissions ?? new List<string>(), (JsonSerializerOptions?)null),
                json => string.IsNullOrEmpty(json)
                    ? new List<string>()
                    : JsonSerializer.Deserialize<List<string>>(json, (JsonSerializerOptions?)null) ?? new List<string>())
            .HasColumnName("Permissions")
            .HasMaxLength(4000);

        // Ignore the read-only property
        builder.Ignore(a => a.Permissions);

        // Indexes
        builder.HasIndex(a => a.KeyHash).IsUnique();
        builder.HasIndex(a => a.OrganizationId);
        builder.HasIndex(a => new { a.OrganizationId, a.Name }).IsUnique();
    }
}
