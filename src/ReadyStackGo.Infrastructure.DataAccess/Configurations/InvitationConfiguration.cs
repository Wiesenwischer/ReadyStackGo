namespace ReadyStackGo.Infrastructure.DataAccess.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ReadyStackGo.Domain.IdentityAccess.Invitations;
using ReadyStackGo.Domain.IdentityAccess.Roles;
using ReadyStackGo.Domain.IdentityAccess.Users;

public class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        builder.ToTable("Invitations");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id)
            .HasConversion(
                id => id.Value.ToString(),
                value => new InvitationId(Guid.Parse(value)))
            .HasColumnName("Id")
            .HasMaxLength(36);

        // Email as owned value object
        builder.OwnsOne(i => i.Email, email =>
        {
            email.Property(e => e.Value)
                .HasColumnName("Email")
                .IsRequired()
                .HasMaxLength(254);

            email.HasIndex(e => e.Value);
        });

        builder.Property(i => i.TokenHash)
            .IsRequired()
            .HasMaxLength(64);

        builder.HasIndex(i => i.TokenHash);

        builder.Property(i => i.Status)
            .IsRequired();

        builder.Property(i => i.RoleId)
            .HasConversion(
                id => id.Value,
                value => new RoleId(value))
            .HasColumnName("RoleId")
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(i => i.ScopeType)
            .IsRequired();

        builder.Property(i => i.ScopeId)
            .HasMaxLength(36);

        builder.Property(i => i.InvitedBy)
            .HasConversion(
                id => id.Value.ToString(),
                value => new UserId(Guid.Parse(value)))
            .HasColumnName("InvitedBy")
            .IsRequired()
            .HasMaxLength(36);

        builder.Property(i => i.CreatedAt).IsRequired();
        builder.Property(i => i.ExpiresAt).IsRequired();
        builder.Property(i => i.AcceptedAt);

        builder.Property(i => i.Version)
            .IsConcurrencyToken();

        builder.Ignore(i => i.DomainEvents);
    }
}
