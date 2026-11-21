using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineBanking.Module.Accounts.Domain;

namespace OnlineBanking.Persistence.Configurations;

internal sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("accounts", "accounts", t =>
        {
            t.HasCheckConstraint("ck_accounts_balance_non_negative", "balance >= 0");
            t.HasCheckConstraint("ck_accounts_blocked_non_negative", "blocked_amount >= 0");
        });

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.ClientId).HasColumnName("client_id").IsRequired();
        builder.Property(a => a.Iban).HasColumnName("iban").HasMaxLength(34).IsRequired();
        builder.Property(a => a.Currency).HasColumnName("currency").HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(a => a.Balance).HasColumnName("balance").HasPrecision(18, 2).IsRequired();
        builder.Property(a => a.BlockedAmount).HasColumnName("blocked_amount").HasPrecision(18, 2).IsRequired();
        builder.Property(a => a.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(a => a.OpenedAtUtc).HasColumnName("opened_at_utc").IsRequired();

        builder.Ignore(a => a.AvailableBalance);

        builder.HasIndex(a => a.Iban).IsUnique().HasDatabaseName("ux_accounts_iban");
        builder.HasIndex(a => a.ClientId).HasDatabaseName("ix_accounts_client_id");
    }
}
