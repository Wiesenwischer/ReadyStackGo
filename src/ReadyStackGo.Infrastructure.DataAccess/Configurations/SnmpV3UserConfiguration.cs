using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReadyStackGo.Domain.Snmp;

namespace ReadyStackGo.Infrastructure.DataAccess.Configurations;

public class SnmpV3UserConfiguration : IEntityTypeConfiguration<SnmpV3User>
{
    public void Configure(EntityTypeBuilder<SnmpV3User> builder)
    {
        builder.ToTable("SnmpV3Users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Name).IsRequired().HasMaxLength(64);
        builder.HasIndex(u => u.Name).IsUnique();
        builder.Property(u => u.AuthProtocol).HasConversion<int>().IsRequired();
        builder.Property(u => u.PrivProtocol).HasConversion<int>().IsRequired();
        builder.Property(u => u.AuthPassphraseEncrypted).IsRequired().HasMaxLength(1024);
        builder.Property(u => u.PrivPassphraseEncrypted).IsRequired().HasMaxLength(1024);
        builder.Property(u => u.CreatedAt).IsRequired();
        builder.Property(u => u.UpdatedAt);
        builder.Property(u => u.Version).IsConcurrencyToken();
    }
}
