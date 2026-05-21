namespace ReadyStackGo.Infrastructure.DataAccess.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReadyStackGo.Domain.Deployment;
using ReadyStackGo.Domain.Deployment.PrtgConnections;

public class PrtgConnectionConfiguration : IEntityTypeConfiguration<PrtgConnection>
{
    public void Configure(EntityTypeBuilder<PrtgConnection> builder)
    {
        builder.ToTable("PrtgConnections");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasConversion(id => id.Value, value => new PrtgConnectionId(value))
            .IsRequired();

        builder.Property(c => c.OrganizationId)
            .HasConversion(id => id.Value, value => new OrganizationId(value))
            .IsRequired();

        builder.Property(c => c.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.Url)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(c => c.EncryptedApiToken)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(c => c.TemplateDeviceId);
        builder.Property(c => c.VerifyTls).IsRequired();
        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.UpdatedAt);
        builder.Property(c => c.LastUsedAt);

        builder.HasIndex(c => c.OrganizationId);
        builder.HasIndex(c => new { c.OrganizationId, c.Name }).IsUnique();
    }
}
