namespace ReadyStackGo.Infrastructure.DataAccess.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReadyStackGo.Domain.StackManagement.Sources;

/// <summary>
/// EF Core configuration for StackSource aggregate.
/// </summary>
public class StackSourceConfiguration : IEntityTypeConfiguration<StackSource>
{
    public void Configure(EntityTypeBuilder<StackSource> builder)
    {
        builder.ToTable("StackSources");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasConversion(
                id => id.Value,
                value => new StackSourceId(value))
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(s => s.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(s => s.Type)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(s => s.Enabled)
            .IsRequired();

        builder.Property(s => s.LastSyncedAt);

        builder.Property(s => s.CreatedAt)
            .IsRequired();

        // Type-specific fields (nullable based on source type)
        builder.Property(s => s.Path)
            .HasMaxLength(1000);

        builder.Property(s => s.FilePattern)
            .HasMaxLength(200);

        builder.Property(s => s.GitUrl)
            .HasMaxLength(1000);

        builder.Property(s => s.GitBranch)
            .HasMaxLength(255);

        // Indexes
        builder.HasIndex(s => s.Name).IsUnique();
        builder.HasIndex(s => s.Type);
        builder.HasIndex(s => s.Enabled);
    }
}
