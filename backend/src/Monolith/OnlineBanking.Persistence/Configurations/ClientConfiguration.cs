using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineBanking.Module.Clients.Domain;

namespace OnlineBanking.Persistence.Configurations;

internal sealed class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.ToTable("clients", "clients");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.FullName).HasColumnName("full_name").HasMaxLength(200).IsRequired();
        builder.Property(c => c.Email).HasColumnName("email").HasMaxLength(256).IsRequired();
        builder.Property(c => c.Phone).HasColumnName("phone").HasMaxLength(30).IsRequired();
        builder.Property(c => c.BirthDate).HasColumnName("birth_date").IsRequired();
        builder.Property(c => c.TaxId).HasColumnName("tax_id").HasMaxLength(20).IsRequired();
        builder.Property(c => c.PasswordHash).HasColumnName("password_hash").HasMaxLength(512).IsRequired();
        builder.Property(c => c.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(c => c.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(c => c.StatusReason).HasColumnName("status_reason").HasMaxLength(500);
        builder.Property(c => c.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();

        builder.HasIndex(c => c.Email).IsUnique().HasDatabaseName("ux_clients_email");
        builder.HasIndex(c => c.TaxId).IsUnique().HasDatabaseName("ux_clients_tax_id");
    }
}
