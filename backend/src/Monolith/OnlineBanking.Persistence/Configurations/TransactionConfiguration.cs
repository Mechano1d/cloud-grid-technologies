using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineBanking.Module.Transactions.Domain;

namespace OnlineBanking.Persistence.Configurations;

internal sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("transactions", "transactions", t =>
        {
            t.HasCheckConstraint("ck_transactions_amount_positive", "amount > 0");
        });

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.InitiatedByClientId).HasColumnName("initiated_by_client_id").IsRequired();
        builder.Property(t => t.FromAccountId).HasColumnName("from_account_id").IsRequired();
        builder.Property(t => t.ToAccountId).HasColumnName("to_account_id").IsRequired();
        builder.Property(t => t.ToIban).HasColumnName("to_iban").HasMaxLength(34).IsRequired();
        builder.Property(t => t.Amount).HasColumnName("amount").HasPrecision(18, 2).IsRequired();
        builder.Property(t => t.Currency).HasColumnName("currency").HasMaxLength(16).IsRequired();
        builder.Property(t => t.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(t => t.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(t => t.Description).HasColumnName("description").HasMaxLength(250);
        builder.Property(t => t.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(100);
        builder.Property(t => t.FailureReason).HasColumnName("failure_reason").HasMaxLength(500);
        builder.Property(t => t.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(t => t.CompletedAtUtc).HasColumnName("completed_at_utc");

        builder.HasIndex(t => new { t.InitiatedByClientId, t.IdempotencyKey })
            .IsUnique()
            .HasFilter("idempotency_key IS NOT NULL")
            .HasDatabaseName("ux_transactions_client_idempotency_key");

        builder.HasIndex(t => t.FromAccountId).HasDatabaseName("ix_transactions_from_account_id");
        builder.HasIndex(t => t.ToAccountId).HasDatabaseName("ix_transactions_to_account_id");
        builder.HasIndex(t => t.CreatedAtUtc).HasDatabaseName("ix_transactions_created_at_utc");
    }
}
