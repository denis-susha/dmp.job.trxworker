using DMP.DataAccess.BillingModels;
using Microsoft.EntityFrameworkCore;

namespace DMP.DataAccess;

public class BillingDbContext(DbContextOptions<BillingDbContext> options) : DbContext(options)
{
    public DbSet<TransactionDAL> Transactions => Set<TransactionDAL>();
    public DbSet<GeneralLedgerDAL> GeneralLedgers => Set<GeneralLedgerDAL>();
    public DbSet<AccountDAL> Accounts => Set<AccountDAL>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TransactionDAL>(entity =>
        {
            entity.ToTable("Transaction");
            entity.HasKey(p => p.TransactionId);

            entity.Property(e => e.TransactionId).HasColumnName("TransactionId").IsRequired().IsFixedLength().HasMaxLength(26);
            entity.Property(e => e.OrderId).HasColumnName("OrderId");
            entity.Property(e => e.TransactionDate).HasColumnName("TransactionDate");
            entity.Property(e => e.TransactionType).HasColumnName("TransactionType").IsRequired();
            entity.Property(e => e.Amount).HasColumnName("Amount").IsRequired();
            entity.Property(e => e.Cryptocurrency).HasColumnName("Cryptocurrency").IsRequired();
            entity.Property(e => e.UserId).HasColumnName("UserId").IsRequired();
            entity.Property(e => e.AccountId).HasColumnName("AccountId").IsRequired();
            entity.Property(e => e.Status).HasColumnName("Status").IsRequired();
            entity.Property(e => e.PaymentMethodId).HasColumnName("PaymentMethodId");
            entity.Property(e => e.ReferenceNumber).HasColumnName("ReferenceNumber");
            entity.Property(e => e.Description).HasColumnName("Description");
            entity.Property(e => e.SellerId).HasColumnName("SellerId");
            entity.Property(e => e.FeeAmount).HasColumnName("FeeAmount").IsRequired();
            entity.Property(e => e.NetAmount).HasColumnName("NetAmount").IsRequired();
            entity.Property(e => e.TransactionSource).HasColumnName("TransactionSource").IsRequired();
            entity.Property(e => e.SourceTransactionId).HasColumnName("SourceTransactionId");
            entity.Property(e => e.RelatedTransactionId).HasColumnName("RelatedTransactionId");
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt").HasDefaultValueSql();
            entity.Property(e => e.UpdatedAt).HasColumnName("UpdatedAt").HasDefaultValueSql();
            entity.Property(e => e.IPAddress).HasColumnName("IPAddress");
            entity.Property(e => e.Rate).HasColumnName("Rate");
            entity.Property(e => e.OrderLineId).HasColumnName("OrderLineId");
            entity.Property(e => e.Price).HasColumnName("Price").IsRequired();
            entity.Property(e => e.PriceCurrency).HasColumnName("PriceCurrency").IsRequired();
        });

        modelBuilder.Entity<GeneralLedgerDAL>(entity =>
        {
            entity.ToTable("GeneralLedger");
            entity.HasKey(p => p.GeneralLedgerId);

            entity.Property(e => e.TransactionId).HasColumnName("TransactionId").IsRequired().IsFixedLength().HasMaxLength(26);
            entity.Property(e => e.Date).HasColumnName("Date").IsRequired();
            entity.Property(e => e.AccountId).HasColumnName("AccountId").IsRequired();
            entity.Property(e => e.Description).HasColumnName("Description");
            entity.Property(e => e.DebitAmount).HasColumnName("DebitAmount").IsRequired();
            entity.Property(e => e.CreditAmount).HasColumnName("CreditAmount").IsRequired();
            entity.Property(e => e.TransactionType).HasColumnName("TransactionType").IsRequired();
            entity.Property(e => e.Cryptocurrency).HasColumnName("Cryptocurrency").IsRequired();
            entity.Property(e => e.TransactionSource).HasColumnName("TransactionSource").IsRequired();
            entity.Property(e => e.CreatedBy).HasColumnName("CreatedBy");
            entity.Property(e => e.ReferenceNumber).HasColumnName("ReferenceNumber");
            entity.Property(e => e.Rate).HasColumnName("Rate");
        });

        modelBuilder.Entity<AccountDAL>(entity =>
        {
            entity.ToTable("Account");
            entity.HasKey(p => p.AccountId);

            entity.Property(e => e.UserId).HasColumnName("UserId").IsRequired();
            entity.Property(e => e.AccountType).HasColumnName("AccountType").IsRequired();
            entity.Property(e => e.Cryptocurrency).HasColumnName("Cryptocurrency").IsRequired();
            entity.Property(e => e.AccountBalance).HasColumnName("AccountBalance").IsRequired();
            entity.Property(e => e.ReservedBalance).HasColumnName("ReservedBalance").IsRequired();
            entity.Property(e => e.Status).HasColumnName("Status").IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt").IsRequired().HasDefaultValueSql();
            entity.Property(e => e.UpdatedAt).HasColumnName("UpdatedAt").IsRequired().HasDefaultValueSql();
        });

        base.OnModelCreating(modelBuilder);
    }
}
