using System.Text.Json;
using System.Text.Json.Serialization;
using DMP.DataAccess.Models;
using DMP.DataAccess.Models.Order;
using DMP.DataAccess.Models.Sale;
using Microsoft.EntityFrameworkCore;

namespace DMP.DataAccess;

/// <summary>
/// Subset of the main DMP database model that the transaction worker reads and writes.
/// </summary>
public class DmpDbContext(DbContextOptions<DmpDbContext> options) : DbContext(options)
{
    private static readonly JsonSerializerOptions UserFeatureWriteOptions =
        new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    private static readonly JsonSerializerOptions UserFeatureReadOptions =
        new() { PropertyNameCaseInsensitive = true };

    public DbSet<ProductDAL> Products => Set<ProductDAL>();
    public DbSet<ProductLineDAL> ProductLines => Set<ProductLineDAL>();
    public DbSet<MailDAL> Mails => Set<MailDAL>();
    public DbSet<UserDAL> Users => Set<UserDAL>();
    public DbSet<OrderHeaderDAL> OrderHeaders => Set<OrderHeaderDAL>();
    public DbSet<OrderLineDAL> OrderLines => Set<OrderLineDAL>();
    public DbSet<TransactionWorkerTaskDAL> TransactionWorkerTasks => Set<TransactionWorkerTaskDAL>();
    public DbSet<SalesHeaderDAL> SalesHeaders => Set<SalesHeaderDAL>();
    public DbSet<SalesLineDAL> SalesLines => Set<SalesLineDAL>();
    public DbSet<CryptocurrencySettingDAL> CryptocurrencySettings => Set<CryptocurrencySettingDAL>();
    public DbSet<EmailTemplateDAL> EmailTemplates => Set<EmailTemplateDAL>();
    public DbSet<JobProductCacheTaskDAL> JobProductCacheTasks => Set<JobProductCacheTaskDAL>();
    public DbSet<PayoutDAL> Payouts => Set<PayoutDAL>();
    public DbSet<FundsTransferDAL> FundsTransfers => Set<FundsTransferDAL>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProductDAL>(entity =>
        {
            entity.ToTable("Product");
            entity.HasKey(p => p.ProductId);

            entity.Property(e => e.MenuCategoryId)
                .IsRequired();

            entity.Property(t => t.UserFeature)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, UserFeatureWriteOptions),
                    v => JsonSerializer.Deserialize<UserFeatureDAL>(v, UserFeatureReadOptions))
                .HasColumnType("jsonb");

            entity.Property(e => e.Slug)
                .IsRequired()
                .HasMaxLength(5000);

            entity.Property(e => e.SellerId).IsRequired();
            entity.Property(e => e.Price).IsRequired();
            entity.Property(e => e.Quantity).IsRequired();
            entity.Property(e => e.Unlimited).IsRequired();
            entity.Property(e => e.ImgLinks);
        });

        modelBuilder.Entity<ProductLineDAL>(entity =>
        {
            entity.ToTable("ProductLine");
            entity.HasKey(p => p.ProductLineId);
            entity.Property(p => p.ProductId).IsRequired();
            entity.Property(p => p.Value).IsRequired();
            entity.Property(p => p.IsSold).IsRequired();

            entity.HasOne(cf => cf.Product)
                .WithMany(f => f.ProductLines)
                .HasForeignKey(cf => cf.ProductId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_ProductLines_Product");
        });

        modelBuilder.Entity<MailDAL>(entity =>
        {
            entity.ToTable("Mail");
            entity.HasKey(p => p.MailId);

            entity.Property(e => e.MailId).HasColumnName("MailId").IsRequired();
            entity.Property(e => e.To).HasColumnName("To").IsRequired().HasMaxLength(320);
            entity.Property(e => e.From).HasColumnName("From").IsRequired().HasMaxLength(320);
            entity.Property(e => e.Subject).HasColumnName("Subject").HasMaxLength(988);
            entity.Property(e => e.Copy).HasColumnName("Copy").HasMaxLength(1000);
            entity.Property(e => e.Body).HasColumnName("Body");
            entity.Property(e => e.Status).HasColumnName("Status").IsRequired();
            entity.Property(e => e.UpdatedAt).HasColumnName("UpdatedAt").HasDefaultValueSql();
            entity.Property(e => e.Attempts).HasColumnName("Attempts").IsRequired();
            entity.Property(e => e.EmailTemplateId);
            entity.Property(t => t.Model);

            entity.HasOne(p => p.EmailTemplate)
                .WithMany(f => f.Mails)
                .HasForeignKey(p => p.EmailTemplateId);
        });

        modelBuilder.Entity<UserDAL>(entity =>
        {
            entity.ToTable("User");
            entity.HasKey(p => p.UserId);

            entity.Property(e => e.UserId).HasColumnName("UserId").IsRequired();
            entity.Property(e => e.Email).HasColumnName("Email").IsRequired().HasMaxLength(320);
            entity.Property(e => e.Password).HasColumnName("Password").IsRequired();
            entity.Property(e => e.Salt).HasColumnName("Salt").IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt").HasDefaultValueSql();
            entity.Property(e => e.Status).HasColumnName("Status").HasDefaultValueSql();
            entity.Property(e => e.Flags).HasColumnName("Flags").IsRequired();
            entity.Property(e => e.Language).HasColumnName("Language").IsRequired();
            entity.Property(e => e.Name).HasColumnName("Name").IsRequired().HasMaxLength(200);
        });

        modelBuilder.Entity<OrderHeaderDAL>(entity =>
        {
            entity.ToTable("OrderHeader");
            entity.HasKey(p => p.OrderId);
            entity.Property(e => e.OrderId).HasColumnName("OrderId").IsRequired();
            entity.Property(e => e.UserId).HasColumnName("UserId").IsRequired();
            entity.Property(e => e.Amount).IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt").HasDefaultValueSql();
            entity.Property(e => e.Status).HasColumnName("Status").IsRequired();
            entity.Property(e => e.ReferenceNumber).HasColumnName("ReferenceNumber").HasMaxLength(200);
            entity.Property(e => e.Currency).HasColumnName("Currency").IsRequired();
        });

        modelBuilder.Entity<OrderLineDAL>(entity =>
        {
            entity.ToTable("OrderLine");
            entity.HasKey(p => p.OrderLineId);
            entity.Property(p => p.OrderLineId).IsRequired();
            entity.Property(e => e.OrderId).HasColumnName("OrderId").IsRequired();
            entity.Property(e => e.ProductId).HasColumnName("ProductId").IsRequired();
            entity.Property(e => e.SellerId).HasColumnName("SellerId").IsRequired();
            entity.Property(e => e.Price).HasColumnName("Price").IsRequired();
            entity.Property(e => e.Quantity).HasColumnName("Quantity").IsRequired();
            entity.Property(e => e.Currency).HasColumnName("Currency").IsRequired();
            entity.Property(e => e.IsLine).HasColumnName("IsLine").IsRequired();
            entity.Property(e => e.ProductFileId).HasColumnName("ProductFileId");
            entity.Property(e => e.ProductLineId).HasColumnName("ProductLineId");

            entity.HasOne(p => p.OrderHeader)
                .WithMany(m => m.OrderLines)
                .HasForeignKey(k => k.OrderId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_OrderLine_OrderId_OrderHeader_OrderId");
        });

        modelBuilder.Entity<TransactionWorkerTaskDAL>(entity =>
        {
            entity.ToTable("TransactionWorkerTask");
            entity.HasKey(p => p.InvoiceId);
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt").HasDefaultValueSql();
            entity.Property(e => e.Status).HasColumnName("Status").IsRequired();
            entity.Property(e => e.OrderId).HasColumnName("OrderId").IsRequired();
            entity.Property(e => e.UpdatedAt).HasColumnName("UpdatedAt").HasDefaultValueSql();
            entity.Property(e => e.Attempts).HasColumnName("Attempts").HasDefaultValueSql();
            entity.Property(e => e.SourceTransactionId).HasColumnName("SourceTransactionId");
            entity.Property(e => e.Type).IsRequired();
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.Comment);
        });

        modelBuilder.Entity<SalesHeaderDAL>(entity =>
        {
            entity.ToTable("SalesHeader");
            entity.HasKey(p => p.SalesHeaderId);
            entity.Property(e => e.SellerId).HasColumnName("SellerId").IsRequired();
            entity.Property(e => e.Amount).HasColumnName("Amount").IsRequired();
            entity.Property(e => e.Cryptocurrency).HasColumnName("Cryptocurrency").IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt").HasDefaultValueSql();
            entity.Property(e => e.OrderId).HasColumnName("OrderId").IsRequired();
        });

        modelBuilder.Entity<SalesLineDAL>(entity =>
        {
            entity.ToTable("SalesLine");
            entity.HasKey(p => p.SalesLineId);
            entity.Property(p => p.SalesHeaderId).HasColumnName("SalesHeaderId").IsRequired();
            entity.Property(e => e.ProductId).HasColumnName("ProductId").IsRequired();
            entity.Property(e => e.Price).HasColumnName("Price").IsRequired();
            entity.Property(e => e.Currency).HasColumnName("Currency").IsRequired();
            entity.Property(e => e.Quantity).HasColumnName("Quantity").IsRequired();
            entity.Property(e => e.TransactionId).HasColumnName("TransactionId").IsRequired().HasMaxLength(26);

            entity.HasOne(p => p.SalesHeader)
                .WithMany(m => m.SalesLines)
                .HasForeignKey(k => k.SalesHeaderId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_SalesLine_SalesHeaderId_SalesHeader_SalesHeaderId");

            entity.HasOne(p => p.Product)
                .WithMany(m => m.SalesLines)
                .HasForeignKey(k => k.ProductId)
                .HasConstraintName("FK_SalesLine_ProductId_Product_ProductId");
        });

        modelBuilder.Entity<CryptocurrencySettingDAL>(entity =>
        {
            entity.ToTable("CryptocurrencySetting");
            entity.HasKey(p => p.CryptocurrencyId);
            entity.Property(p => p.Code).IsRequired().HasMaxLength(15);
            entity.Property(p => p.PlatformCode).IsRequired().HasMaxLength(5);
            entity.Property(p => p.PlatformName).IsRequired();
            entity.Property(p => p.TokenCode).IsRequired().HasMaxLength(5);
            entity.Property(p => p.TokenName).IsRequired();
            entity.Property(p => p.Contract);
            entity.Property(p => p.Standart);
            entity.Property(p => p.Divisibility).IsRequired();
        });

        modelBuilder.Entity<EmailTemplateDAL>(entity =>
        {
            entity.ToTable("EmailTemplate");
            entity.HasKey(p => p.EmailTemplateId);
            entity.Property(p => p.Name).IsRequired();
            entity.Property(p => p.Subject).IsRequired();
            entity.Property(p => p.Body).IsRequired();
            entity.Property(p => p.Language).IsRequired();
        });

        modelBuilder.Entity<JobProductCacheTaskDAL>(entity =>
        {
            entity.ToTable("JobProductCacheTask");
            entity.HasKey(p => p.JobProductCacheTaskId);
            entity.Property(p => p.ProductId).IsRequired();
            entity.Property(p => p.CreatedAt).HasDefaultValueSql();
        });

        modelBuilder.Entity<PayoutDAL>(entity =>
        {
            entity.ToTable("Payout");
            entity.HasKey(p => p.PayoutId);
            entity.Property(p => p.PayoutId).IsRequired().HasMaxLength(29); // "01-" prefix + UUID
            entity.Property(p => p.UserId).IsRequired();
            entity.Property(p => p.Amount).IsRequired();
            entity.Property(p => p.NetworkFee).IsRequired();
            entity.Property(p => p.Cryptocurrency).IsRequired();
            entity.Property(p => p.Status).IsRequired();
            entity.Property(p => p.CreatedAt).HasDefaultValueSql();
            entity.Property(p => p.AdminId);
            entity.Property(p => p.UserAmount).IsRequired();
            entity.Property(p => p.Address).IsRequired().HasMaxLength(104);
            entity.Property(p => p.NetworkTransactionId).HasMaxLength(300);
        });

        modelBuilder.Entity<FundsTransferDAL>(entity =>
        {
            entity.ToTable("FundsTransfer");
            entity.HasKey(p => p.FundsTransferId);
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.TargetUserId).IsRequired();
            entity.Property(e => e.Amount).IsRequired();
            entity.Property(e => e.Cryptocurrency).IsRequired();
            entity.Property(e => e.Comment);
            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql();
        });

        base.OnModelCreating(modelBuilder);
    }
}
