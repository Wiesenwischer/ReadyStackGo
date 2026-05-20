using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReadyStackGo.Domain.Snmp;

namespace ReadyStackGo.Infrastructure.DataAccess.Configurations;

public class SnmpSettingsConfiguration : IEntityTypeConfiguration<SnmpSettings>
{
    public void Configure(EntityTypeBuilder<SnmpSettings> builder)
    {
        builder.ToTable("SnmpSettings");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Enabled).IsRequired();
        builder.Property(s => s.Port).IsRequired();
        builder.Property(s => s.ListenAddress).IsRequired().HasMaxLength(45);
        builder.Property(s => s.RootOid).IsRequired().HasMaxLength(255);
        builder.Property(s => s.Community).IsRequired().HasMaxLength(255);
        builder.Property(s => s.TrapReceivers).IsRequired().HasMaxLength(2000);
        builder.Property(s => s.EngineIdHex).IsRequired().HasMaxLength(80);
        builder.Property(s => s.EngineBoots).IsRequired();
        builder.Property(s => s.Version).IsConcurrencyToken();
    }
}
