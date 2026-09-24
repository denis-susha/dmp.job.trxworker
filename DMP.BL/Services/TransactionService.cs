using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using DMP.BL.Models;
using DMP.BL.Models.Bitcart;
using DMP.BL.Models.Configurations;
using DMP.BL.Models.Mail;
using DMP.Crosscutting;
using DMP.DataAccess;
using DMP.DataAccess.BillingModels;
using DMP.DataAccess.BillingModels.Enumerations;
using DMP.DataAccess.Models;
using DMP.DataAccess.Models.Enumerations;
using DMP.DataAccess.Models.Order;
using DMP.DataAccess.Models.Sale;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Const = DMP.Crosscutting.Constants;

namespace DMP.BL.Services;

/// <summary>
/// Processes transaction worker tasks as resumable state machines: every step persists the task status,
/// so a failed or interrupted task continues from the last completed step on the next run.
/// </summary>
public class TransactionService(
    ILogger<TransactionService> logger,
    IOptions<DmpHostsSettings> dmpHostsOptions,
    IOptions<MinioSettings> minioOptions,
    IDbContextFactory<BillingDbContext> billingContextFactory,
    IDbContextFactory<DmpDbContext> dmpContextFactory,
    IHttpClientFactory httpClientFactory,
    ISettingsService settingsService) : ITransactionService
{
    // Marketplace fee charged on every sale.
    private const decimal SaleFeeRate = 0.05m;

    private readonly DmpHostsSettings _dmpHostsSettings = dmpHostsOptions.Value;
    private readonly MinioSettings _minioSettings = minioOptions.Value;

    public async Task ProcessPayment(TransactionWorkerTaskDAL transactionWorkerTask)
    {
        var invoice = await GetInvoice(transactionWorkerTask.InvoiceId);
        var invoiceStatus = Enum.Parse<InvoiceStatusBC>(invoice.Status, ignoreCase: true);

        if (invoiceStatus != InvoiceStatusBC.Complete)
        {
            if (invoiceStatus != InvoiceStatusBC.Expired)
            {
                throw new InvalidOperationException(
                    $"Invoice '{transactionWorkerTask.InvoiceId}' hasn't been completed yet. Current status: '{invoiceStatus}'.");
            }

            // Marks the task as Complete, so all steps below are skipped and the task is removed.
            await RevertExpiredInvoice(invoice, transactionWorkerTask);
        }

        await using var dmpContext = await dmpContextFactory.CreateDbContextAsync();
        var orderHdr = await dmpContext.OrderHeaders
            .AsNoTracking()
            .FirstAsync(or => or.OrderId == transactionWorkerTask.OrderId);

        // 1. Create income transaction
        if (transactionWorkerTask.Status < TransactionWorkerTaskStatus.IncomeTrxCreated)
        {
            var transaction = GenerateIncomePaymentTransaction(invoice, orderHdr);
            await using var billingContext = await billingContextFactory.CreateDbContextAsync();
            await using var dbTransaction = await billingContext.Database.BeginTransactionAsync();
            var stateBefore = TaskState.Of(transactionWorkerTask);

            try
            {
                var transactionEntry = billingContext.Add(transaction);
                await billingContext.SaveChangesAsync();

                var newTransaction = transactionEntry.Entity;

                var glCreditRecord = GenerateGeneralLedgerRecord(newTransaction, false);
                billingContext.GeneralLedgers.Add(glCreditRecord);

                var targetAccount = await billingContext.Accounts.FirstAsync(a => a.AccountId == glCreditRecord.AccountId);
                targetAccount.AccountBalance += glCreditRecord.CreditAmount;
                billingContext.Accounts.Update(targetAccount);

                await billingContext.SaveChangesAsync();

                transactionWorkerTask.Status = TransactionWorkerTaskStatus.IncomeTrxCreated;
                transactionWorkerTask.SourceTransactionId = newTransaction.TransactionId;
                transactionWorkerTask.Attempts = 0;
                dmpContext.TransactionWorkerTasks.Update(transactionWorkerTask);
                await dmpContext.SaveChangesAsync();

                await dbTransaction.CommitAsync();

                logger.LogInformation("New income transaction {TransactionId} added", newTransaction.TransactionId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to create income transaction for task {InvoiceId}", transactionWorkerTask.InvoiceId);
                await dbTransaction.RollbackAsync();
                await RevertFailedStep(dmpContext, transactionWorkerTask, stateBefore);
            }
        }

        // 2. Move funds to sellers
        if (transactionWorkerTask.Status == TransactionWorkerTaskStatus.IncomeTrxCreated)
        {
            var orderLines = await dmpContext.OrderLines
                .Where(ol => ol.OrderId == orderHdr.OrderId)
                .AsNoTracking()
                .ToListAsync();

            await using var billingContext = await billingContextFactory.CreateDbContextAsync();
            var sourceTrx = await billingContext.Transactions
                .AsNoTracking()
                .FirstAsync(t => t.TransactionId == transactionWorkerTask.SourceTransactionId);

            await using var dbTransaction = await billingContext.Database.BeginTransactionAsync();
            var stateBefore = TaskState.Of(transactionWorkerTask);

            try
            {
                foreach (var line in orderLines)
                {
                    var sellerAccount = await billingContext.Accounts.FirstAsync(a =>
                        a.UserId == line.SellerId && a.Cryptocurrency == sourceTrx.Cryptocurrency &&
                        a.Status == AccountStatus.Active);

                    var movePaymentTrx = GenerateMovePaymentTransaction(sourceTrx, line, sellerAccount);
                    var movePaymentTrxEntity = billingContext.Add(movePaymentTrx);
                    var feeTrx = GenerateMovePaymentFeeTransaction(movePaymentTrx);
                    var feeTrxEntity = billingContext.Add(feeTrx);
                    await billingContext.SaveChangesAsync();

                    var glDebitRecord = GenerateGeneralLedgerRecord(movePaymentTrxEntity.Entity, true);
                    billingContext.GeneralLedgers.Add(glDebitRecord);

                    var glCreditRecord = GenerateGeneralLedgerRecord(movePaymentTrxEntity.Entity, false);
                    billingContext.GeneralLedgers.Add(glCreditRecord);

                    var glFeeDebitRecord = GenerateGeneralLedgerRecord(feeTrxEntity.Entity, true);
                    billingContext.GeneralLedgers.Add(glFeeDebitRecord);

                    var glFeeCreditRecord = GenerateGeneralLedgerRecord(feeTrxEntity.Entity, false);
                    billingContext.GeneralLedgers.Add(glFeeCreditRecord);

                    // Source account pays the seller's share plus the fee
                    var sourceAccount = await billingContext.Accounts.FirstAsync(a => a.AccountId == glDebitRecord.AccountId);
                    sourceAccount.AccountBalance -= glDebitRecord.DebitAmount + glFeeCreditRecord.CreditAmount;
                    billingContext.Accounts.Update(sourceAccount);

                    // Seller receives the amount minus the fee
                    var targetAccount = await billingContext.Accounts.FirstAsync(a => a.AccountId == glCreditRecord.AccountId);
                    targetAccount.AccountBalance += glCreditRecord.CreditAmount;
                    billingContext.Accounts.Update(targetAccount);

                    await billingContext.SaveChangesAsync();

                    // Fee account receives the fee
                    var targetFeeAccount = await billingContext.Accounts.FirstAsync(a => a.AccountId == glFeeDebitRecord.AccountId);
                    targetFeeAccount.AccountBalance += glFeeDebitRecord.DebitAmount;
                    billingContext.Accounts.Update(targetFeeAccount);

                    await billingContext.SaveChangesAsync();
                }

                transactionWorkerTask.Status = TransactionWorkerTaskStatus.MoveFundsTrxCreated;
                transactionWorkerTask.Attempts = 0;
                dmpContext.TransactionWorkerTasks.Update(transactionWorkerTask);
                await dmpContext.SaveChangesAsync();

                await dbTransaction.CommitAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to move funds to sellers for task {InvoiceId}", transactionWorkerTask.InvoiceId);
                await dbTransaction.RollbackAsync();
                await RevertFailedStep(dmpContext, transactionWorkerTask, stateBefore);
            }
        }

        // 3. Create sales
        if (transactionWorkerTask.Status == TransactionWorkerTaskStatus.MoveFundsTrxCreated)
        {
            await using var billingContext = await billingContextFactory.CreateDbContextAsync();
            var sourceTransaction = await billingContext.Transactions
                .FirstAsync(t => t.TransactionId == transactionWorkerTask.SourceTransactionId);
            var transactions = await billingContext.Transactions
                .Where(t => t.SourceTransactionId == transactionWorkerTask.SourceTransactionId && t.TransactionType == TransactionType.MovePayment)
                .AsNoTracking()
                .ToListAsync();

            var groupsTransactions = transactions
                .Where(t => t.SellerId != null)
                .GroupBy(t => t.SellerId)
                .Select(g => new
                {
                    SellerId = g.Key ?? Guid.Empty,
                    Transactions = g,
                    TotalAmount = g.Sum(t => t.NetAmount),
                })
                .ToList();

            await using var dbDmpTransaction = await dmpContext.Database.BeginTransactionAsync();
            var stateBefore = TaskState.Of(transactionWorkerTask);

            try
            {
                foreach (var group in groupsTransactions)
                {
                    var salesHdr = new SalesHeaderDAL
                    {
                        SellerId = group.SellerId,
                        Amount = group.TotalAmount,
                        Cryptocurrency = sourceTransaction.Cryptocurrency,
                        OrderId = sourceTransaction.OrderId!.Value,
                    };

                    var hdrEntity = dmpContext.SalesHeaders.Add(salesHdr);
                    await dmpContext.SaveChangesAsync();

                    foreach (var transaction in group.Transactions)
                    {
                        var orderLine = await dmpContext.OrderLines.FirstAsync(ol => ol.OrderLineId == transaction.OrderLineId);

                        dmpContext.SalesLines.Add(new SalesLineDAL
                        {
                            SalesHeaderId = hdrEntity.Entity.SalesHeaderId,
                            ProductId = orderLine.ProductId,
                            Price = orderLine.Price,
                            Currency = orderLine.Currency,
                            Quantity = orderLine.Quantity,
                            TransactionId = transaction.TransactionId,
                        });
                    }

                    await dmpContext.SaveChangesAsync();
                }

                transactionWorkerTask.Status = TransactionWorkerTaskStatus.SalesCreated;
                transactionWorkerTask.Attempts = 0;
                dmpContext.TransactionWorkerTasks.Update(transactionWorkerTask);

                await dmpContext.SaveChangesAsync();

                await dbDmpTransaction.CommitAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to create sales for task {InvoiceId}", transactionWorkerTask.InvoiceId);
                await dbDmpTransaction.RollbackAsync();
                await RevertFailedStep(dmpContext, transactionWorkerTask, stateBefore);
            }
        }

        // 4. Queue "new sale" emails to sellers
        if (transactionWorkerTask.Status == TransactionWorkerTaskStatus.SalesCreated)
        {
            var salesList = await dmpContext.SalesHeaders
                .AsNoTracking()
                .Where(sh => sh.OrderId == transactionWorkerTask.OrderId)
                .ToListAsync();

            var templates = await dmpContext.EmailTemplates
                .Where(et => et.Name == "SellerNewSale")
                .Select(t => new { t.EmailTemplateId, t.Language })
                .ToListAsync();

            foreach (var sale in salesList)
            {
                var user = await dmpContext.Users.AsNoTracking().FirstAsync(u => u.UserId == sale.SellerId);
                var templateId = templates.First(t => t.Language == user.Language).EmailTemplateId;
                var mail = CreateSellerNewSaleEmail(templateId, user, sale);
                dmpContext.Mails.Add(mail);
            }

            transactionWorkerTask.Status = TransactionWorkerTaskStatus.Complete;
            transactionWorkerTask.Attempts = 0;

            dmpContext.TransactionWorkerTasks.Update(transactionWorkerTask);
            await dmpContext.SaveChangesAsync();
        }

        // 5. Remove completed task
        if (transactionWorkerTask.Status == TransactionWorkerTaskStatus.Complete)
        {
            dmpContext.TransactionWorkerTasks.Remove(transactionWorkerTask);
            await dmpContext.SaveChangesAsync();
        }
    }

    public async Task ProcessPayout(TransactionWorkerTaskDAL transactionTask)
    {
        await using var dmpContext = await dmpContextFactory.CreateDbContextAsync();
        var payout = await dmpContext.Payouts.FirstAsync(p => p.PayoutId == transactionTask.InvoiceId);

        // 1. Create payout transaction
        if (transactionTask.Status == TransactionWorkerTaskStatus.New)
        {
            await using var billingContext = await billingContextFactory.CreateDbContextAsync();
            var sellerAccount = await billingContext.Accounts.FirstAsync(a =>
                a.UserId == payout.UserId && a.Cryptocurrency == payout.Cryptocurrency &&
                a.Status == AccountStatus.Active);

            if (sellerAccount.AccountBalance < payout.Amount + payout.NetworkFee)
            {
                logger.LogError(
                    "The payout {PayoutId} can't be processed: account {AccountId} has insufficient balance",
                    payout.PayoutId, sellerAccount.AccountId);

                payout.Status = PayoutStatus.Error;
                dmpContext.Payouts.Update(payout);

                dmpContext.TransactionWorkerTasks.Remove(transactionTask);
                await dmpContext.SaveChangesAsync();

                return;
            }

            var transaction = GeneratePayoutTransaction(payout, sellerAccount);

            await using var dbTransaction = await billingContext.Database.BeginTransactionAsync();
            var stateBefore = TaskState.Of(transactionTask);

            try
            {
                billingContext.Add(transaction);

                // Saved first so the database-generated CreatedAt is available for the GL records' Date.
                await billingContext.SaveChangesAsync();

                // GL records cover the payout amount plus the network fee
                var glDebitRecord = new GeneralLedgerDAL
                {
                    TransactionId = transaction.TransactionId,
                    Date = transaction.CreatedAt,
                    AccountId = GetGlSourceAccount(transaction),
                    Description = GetGlDescription(transaction.TransactionType, true),
                    DebitAmount = transaction.Amount + transaction.FeeAmount,
                    CreditAmount = 0m,
                    TransactionType = transaction.TransactionType,
                    Cryptocurrency = transaction.Cryptocurrency,
                    TransactionSource = transaction.TransactionSource,
                    ReferenceNumber = transaction.ReferenceNumber,
                };

                billingContext.GeneralLedgers.Add(glDebitRecord);

                var glCreditRecord = new GeneralLedgerDAL
                {
                    TransactionId = transaction.TransactionId,
                    Date = transaction.CreatedAt,
                    AccountId = GetGlTargetAccount(transaction),
                    Description = GetGlDescription(transaction.TransactionType),
                    DebitAmount = 0m,
                    CreditAmount = transaction.Amount + transaction.FeeAmount,
                    TransactionType = transaction.TransactionType,
                    Cryptocurrency = transaction.Cryptocurrency,
                    TransactionSource = transaction.TransactionSource,
                    ReferenceNumber = transaction.ReferenceNumber,
                };

                billingContext.GeneralLedgers.Add(glCreditRecord);

                var sourceAccount = await billingContext.Accounts.FirstAsync(a => a.AccountId == glDebitRecord.AccountId);
                sourceAccount.AccountBalance -= glDebitRecord.DebitAmount;
                billingContext.Accounts.Update(sourceAccount);

                var targetAccount = await billingContext.Accounts.FirstAsync(a => a.AccountId == glCreditRecord.AccountId);
                targetAccount.AccountBalance += glCreditRecord.CreditAmount;
                billingContext.Accounts.Update(targetAccount);

                await billingContext.SaveChangesAsync();

                transactionTask.Status = TransactionWorkerTaskStatus.PayoutCreated;
                transactionTask.SourceTransactionId = transaction.TransactionId;
                transactionTask.Attempts = 0;
                dmpContext.TransactionWorkerTasks.Update(transactionTask);
                await dmpContext.SaveChangesAsync();

                await dbTransaction.CommitAsync();

                logger.LogInformation("New payout transaction {TransactionId} added", transaction.TransactionId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to create payout transaction for payout {PayoutId}", payout.PayoutId);
                await dbTransaction.RollbackAsync();
                await RevertFailedStep(dmpContext, transactionTask, stateBefore);
            }
        }

        // 2. Queue payout email
        if (transactionTask.Status == TransactionWorkerTaskStatus.PayoutCreated)
        {
            var user = await dmpContext.Users.AsNoTracking().FirstAsync(u => u.UserId == payout.UserId);
            var templateId = await dmpContext.EmailTemplates
                .Where(et => et.Name == "SellerPayout" && et.Language == user.Language)
                .Select(t => t.EmailTemplateId)
                .FirstAsync();

            var mail = CreateSellerPayoutEmail(templateId, user, payout);
            dmpContext.Mails.Add(mail);

            transactionTask.Status = TransactionWorkerTaskStatus.Complete;
            transactionTask.Attempts = 0;

            dmpContext.TransactionWorkerTasks.Update(transactionTask);
            await dmpContext.SaveChangesAsync();
        }

        // 3. Complete payout and remove task
        if (transactionTask.Status == TransactionWorkerTaskStatus.Complete)
        {
            payout.Status = PayoutStatus.Complete;
            dmpContext.Payouts.Update(payout);

            dmpContext.TransactionWorkerTasks.Remove(transactionTask);
            await dmpContext.SaveChangesAsync();
        }
    }

    public async Task ProcessIncomingTransfer(TransactionWorkerTaskDAL transactionWorkerTask)
    {
        var invoice = await GetInvoice(transactionWorkerTask.InvoiceId);
        var invoiceStatus = Enum.Parse<InvoiceStatusBC>(invoice.Status, ignoreCase: true);

        await using var dmpContext = await dmpContextFactory.CreateDbContextAsync();

        if (invoiceStatus != InvoiceStatusBC.Complete)
        {
            if (invoiceStatus != InvoiceStatusBC.Expired)
            {
                throw new InvalidOperationException(
                    $"Invoice '{transactionWorkerTask.InvoiceId}' hasn't been completed yet. Current status: '{invoiceStatus}'.");
            }

            transactionWorkerTask.Status = TransactionWorkerTaskStatus.Complete;
            dmpContext.TransactionWorkerTasks.Update(transactionWorkerTask);
            await dmpContext.SaveChangesAsync();
        }

        // 1. Create income transaction
        if (transactionWorkerTask.Status < TransactionWorkerTaskStatus.IncomeTrxCreated)
        {
            var transaction = GenerateIncomingTransferTransaction(invoice, transactionWorkerTask.UserId);
            await using var billingContext = await billingContextFactory.CreateDbContextAsync();
            await using var dbTransaction = await billingContext.Database.BeginTransactionAsync();
            var stateBefore = TaskState.Of(transactionWorkerTask);

            try
            {
                var transactionEntry = billingContext.Add(transaction);
                await billingContext.SaveChangesAsync();

                var newTransaction = transactionEntry.Entity;

                var glCreditRecord = GenerateGeneralLedgerRecord(newTransaction, false);
                billingContext.GeneralLedgers.Add(glCreditRecord);

                var targetAccount = await billingContext.Accounts.FirstAsync(a => a.AccountId == glCreditRecord.AccountId);
                targetAccount.AccountBalance += glCreditRecord.CreditAmount;
                billingContext.Accounts.Update(targetAccount);

                await billingContext.SaveChangesAsync();

                transactionWorkerTask.Status = TransactionWorkerTaskStatus.Complete;
                transactionWorkerTask.SourceTransactionId = newTransaction.TransactionId;
                transactionWorkerTask.Attempts = 0;
                dmpContext.TransactionWorkerTasks.Update(transactionWorkerTask);
                await dmpContext.SaveChangesAsync();

                await dbTransaction.CommitAsync();

                logger.LogInformation("New incoming transfer transaction {TransactionId} added", newTransaction.TransactionId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to create incoming transfer transaction for task {InvoiceId}", transactionWorkerTask.InvoiceId);
                await dbTransaction.RollbackAsync();
                await RevertFailedStep(dmpContext, transactionWorkerTask, stateBefore);
            }
        }

        // 2. Remove completed task
        if (transactionWorkerTask.Status == TransactionWorkerTaskStatus.Complete)
        {
            dmpContext.TransactionWorkerTasks.Remove(transactionWorkerTask);
            await dmpContext.SaveChangesAsync();
        }
    }

    public async Task ProcessBonus(TransactionWorkerTaskDAL transactionWorkerTask)
    {
        await using var dmpContext = await dmpContextFactory.CreateDbContextAsync();

        // For bonus tasks OrderId holds the FundsTransferId.
        var fundsTransfer = await dmpContext.FundsTransfers
            .FirstAsync(ft => ft.FundsTransferId == transactionWorkerTask.OrderId);

        // 1. Move funds to user
        if (transactionWorkerTask.Status == TransactionWorkerTaskStatus.New)
        {
            await using var billingContext = await billingContextFactory.CreateDbContextAsync();
            await using var dbTransaction = await billingContext.Database.BeginTransactionAsync();
            var stateBefore = TaskState.Of(transactionWorkerTask);

            try
            {
                var userAccount = await billingContext.Accounts.FirstAsync(a =>
                    a.UserId == fundsTransfer.TargetUserId && a.Cryptocurrency == fundsTransfer.Cryptocurrency &&
                    a.Status == AccountStatus.Active);

                var transferTrx = GenerateTransferTransaction(fundsTransfer, userAccount);
                var transferTrxEntity = billingContext.Add(transferTrx);
                await billingContext.SaveChangesAsync();

                var glDebitRecord = GenerateGeneralLedgerRecord(transferTrxEntity.Entity, true);
                billingContext.GeneralLedgers.Add(glDebitRecord);

                var glCreditRecord = GenerateGeneralLedgerRecord(transferTrxEntity.Entity, false);
                billingContext.GeneralLedgers.Add(glCreditRecord);

                var sourceAccount = await billingContext.Accounts.FirstAsync(a => a.AccountId == glDebitRecord.AccountId);
                sourceAccount.AccountBalance -= glDebitRecord.DebitAmount;
                billingContext.Accounts.Update(sourceAccount);

                var targetAccount = await billingContext.Accounts.FirstAsync(a => a.AccountId == glCreditRecord.AccountId);
                targetAccount.AccountBalance += glCreditRecord.CreditAmount;
                billingContext.Accounts.Update(targetAccount);

                await billingContext.SaveChangesAsync();

                transactionWorkerTask.Status = TransactionWorkerTaskStatus.MoveFundsTrxCreated;
                transactionWorkerTask.Attempts = 0;
                dmpContext.TransactionWorkerTasks.Update(transactionWorkerTask);
                await dmpContext.SaveChangesAsync();

                await dbTransaction.CommitAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to move bonus funds for funds transfer {FundsTransferId}", fundsTransfer.FundsTransferId);
                await dbTransaction.RollbackAsync();
                await RevertFailedStep(dmpContext, transactionWorkerTask, stateBefore);
            }
        }

        // 2. No email is sent for bonuses yet
        if (transactionWorkerTask.Status == TransactionWorkerTaskStatus.MoveFundsTrxCreated)
        {
            transactionWorkerTask.Status = TransactionWorkerTaskStatus.Complete;
            transactionWorkerTask.Attempts = 0;

            dmpContext.TransactionWorkerTasks.Update(transactionWorkerTask);
            await dmpContext.SaveChangesAsync();
        }

        // 3. Complete funds transfer and remove task
        if (transactionWorkerTask.Status == TransactionWorkerTaskStatus.Complete)
        {
            fundsTransfer.Status = FundsTransferStatus.Completed;

            dmpContext.TransactionWorkerTasks.Remove(transactionWorkerTask);
            await dmpContext.SaveChangesAsync();
        }
    }

    private readonly record struct TaskState(TransactionWorkerTaskStatus Status, string? SourceTransactionId, int Attempts)
    {
        public static TaskState Of(TransactionWorkerTaskDAL task) => new(task.Status, task.SourceTransactionId, task.Attempts);
    }

    /// <summary>
    /// Undoes a failed step after its database transaction has been rolled back and counts the attempt.
    /// The step may have advanced the task in memory (and left other entities in the change tracker) before a save
    /// failed; persisting that state would mark the step as done without its ledger records. So the tracker is cleared,
    /// the task is restored to its pre-step state, and only the attempt counter is written.
    /// </summary>
    private static async Task RevertFailedStep(DmpDbContext dmpContext, TransactionWorkerTaskDAL task, TaskState before)
    {
        dmpContext.ChangeTracker.Clear();

        task.Status = before.Status;
        task.SourceTransactionId = before.SourceTransactionId;
        task.Attempts = before.Attempts + 1;

        await dmpContext.TransactionWorkerTasks
            .Where(t => t.InvoiceId == task.InvoiceId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.Attempts, t => t.Attempts + 1));
    }

    private async Task<InvoiceResponseBC> GetInvoice(string invoiceId)
    {
        var client = httpClientFactory.CreateClient(BitcartOptions.Position);

        // Relative to BitcartOptions.ApiUrl, which ends with "/invoices" without a trailing slash,
        // so the last segment is replaced: ".../invoices" + "invoices/{id}" => ".../invoices/{id}".
        var response = await client.GetFromJsonAsync<InvoiceResponseBC>($"invoices/{invoiceId}", JsonSerializerOptions.Default);

        return response ?? throw new InvalidOperationException($"Bitcart returned an empty response for invoice '{invoiceId}'.");
    }

    private static TransactionDAL GenerateIncomePaymentTransaction(InvoiceResponseBC invoice, OrderHeaderDAL orderHeader)
    {
        var invoicePayment = invoice.Payments.First(p =>
            string.Equals(p.Name, invoice.PaidCurrency, StringComparison.InvariantCultureIgnoreCase));
        var paidCurrency = Enum.Parse<Cryptocurrency>(invoice.PaidCurrency!, ignoreCase: true);
        var amount = invoice.SentAmount ?? 0;

        return new TransactionDAL
        {
            TransactionId = Ulid.NewUlid().ToString(),
            OrderId = orderHeader.OrderId,
            TransactionDate = ParseBitcartDate(invoice.PaidDate!),
            TransactionType = TransactionType.Payment,
            Price = ParseBitcartDecimal(invoice.Price),
            PriceCurrency = Enum.Parse<Currency>(invoice.Currency, ignoreCase: true),
            Cryptocurrency = paidCurrency,
            Amount = amount,
            UserId = orderHeader.UserId,
            AccountId = SystemAccount.Credit[paidCurrency],
            Status = TransactionStatus.Completed,
            PaymentMethodId = invoicePayment.Id,
            ReferenceNumber = invoice.Id,
            Description = $"Income payment by order #{orderHeader.OrderId}",
            NetAmount = amount,
            TransactionSource = TransactionSourceType.WebApp,
            Rate = ParseBitcartDecimal(invoicePayment.Rate),
        };
    }

    private TransactionDAL GenerateMovePaymentTransaction(TransactionDAL sourceTrx, OrderLineDAL orderLine, AccountDAL sellerAccount)
    {
        var divisibility = settingsService.GetCurrencySetting(sourceTrx.Cryptocurrency).Divisibility;

        var sourceAmount = orderLine.Quantity * orderLine.Price / (sourceTrx.Rate ?? 0m);
        var netAmount = MathHelper.Truncate(sourceAmount, divisibility);
        var feeAmount = Math.Round(netAmount * SaleFeeRate, divisibility);
        var amount = netAmount - feeAmount;

        return new TransactionDAL
        {
            TransactionId = Ulid.NewUlid().ToString(),
            OrderId = sourceTrx.OrderId,
            TransactionDate = sourceTrx.TransactionDate,
            TransactionType = TransactionType.MovePayment,
            Amount = amount,
            Cryptocurrency = sourceTrx.Cryptocurrency,
            Price = orderLine.Price,
            PriceCurrency = orderLine.Currency,
            UserId = sourceTrx.UserId,
            AccountId = sellerAccount.AccountId,
            Status = TransactionStatus.Completed,
            PaymentMethodId = sourceTrx.PaymentMethodId,
            ReferenceNumber = sourceTrx.ReferenceNumber,
            Description = "Sale",
            SellerId = orderLine.SellerId,
            FeeAmount = feeAmount,
            NetAmount = netAmount,
            TransactionSource = sourceTrx.TransactionSource,
            SourceTransactionId = sourceTrx.TransactionId,
            Rate = sourceTrx.Rate,
            OrderLineId = orderLine.OrderLineId,
        };
    }

    private static TransactionDAL GenerateMovePaymentFeeTransaction(TransactionDAL movePaymentTrx) => new()
    {
        TransactionId = Ulid.NewUlid().ToString(),
        OrderId = movePaymentTrx.OrderId,
        TransactionDate = movePaymentTrx.TransactionDate,
        TransactionType = TransactionType.Fee,
        Amount = movePaymentTrx.FeeAmount,
        Cryptocurrency = movePaymentTrx.Cryptocurrency,
        Price = movePaymentTrx.Price,
        PriceCurrency = movePaymentTrx.PriceCurrency,
        UserId = movePaymentTrx.UserId,
        AccountId = movePaymentTrx.AccountId,
        Status = TransactionStatus.Completed,
        PaymentMethodId = movePaymentTrx.PaymentMethodId,
        ReferenceNumber = movePaymentTrx.ReferenceNumber,
        Description = "Fee",
        SellerId = movePaymentTrx.SellerId,
        NetAmount = movePaymentTrx.FeeAmount,
        TransactionSource = movePaymentTrx.TransactionSource,
        SourceTransactionId = movePaymentTrx.SourceTransactionId,
        RelatedTransactionId = movePaymentTrx.TransactionId,
        Rate = movePaymentTrx.Rate,
        OrderLineId = movePaymentTrx.OrderLineId,
    };

    private static TransactionDAL GeneratePayoutTransaction(PayoutDAL payout, AccountDAL sellerAccount) => new()
    {
        TransactionId = Ulid.NewUlid().ToString(),
        OrderId = -1, // payouts are not tied to an order
        TransactionDate = payout.CreatedAt,
        TransactionType = TransactionType.Payout,
        PriceCurrency = Currency.USD,
        Cryptocurrency = payout.Cryptocurrency,
        Amount = payout.Amount,
        UserId = payout.UserId,
        AccountId = sellerAccount.AccountId,
        Status = TransactionStatus.Completed,
        ReferenceNumber = payout.PayoutId,
        Description = "Payout to seller",
        SellerId = payout.UserId,
        FeeAmount = payout.NetworkFee,
        NetAmount = payout.Amount,
        TransactionSource = TransactionSourceType.WebApp,
        Rate = 1,
    };

    private static TransactionDAL GenerateIncomingTransferTransaction(InvoiceResponseBC invoice, Guid userId)
    {
        var invoicePayment = invoice.Payments.First(p =>
            string.Equals(p.Name, invoice.PaidCurrency, StringComparison.InvariantCultureIgnoreCase));
        var paidCurrency = Enum.Parse<Cryptocurrency>(invoice.PaidCurrency!, ignoreCase: true);
        var amount = invoice.SentAmount ?? 0;

        return new TransactionDAL
        {
            TransactionId = Ulid.NewUlid().ToString(),
            TransactionDate = ParseBitcartDate(invoice.PaidDate!),
            TransactionType = TransactionType.IncomingTransfer,
            Price = ParseBitcartDecimal(invoice.Price),
            PriceCurrency = Enum.Parse<Currency>(invoice.Currency, ignoreCase: true),
            Cryptocurrency = paidCurrency,
            Amount = amount,
            UserId = userId,
            AccountId = SystemAccount.Credit[paidCurrency],
            Status = TransactionStatus.Completed,
            PaymentMethodId = invoicePayment.Id,
            ReferenceNumber = invoice.Id,
            Description = "Incoming transfer",
            NetAmount = amount,
            TransactionSource = TransactionSourceType.Admin,
            Rate = ParseBitcartDecimal(invoicePayment.Rate),
        };
    }

    private TransactionDAL GenerateTransferTransaction(FundsTransferDAL fundsTransfer, AccountDAL userAccount)
    {
        var divisibility = settingsService.GetCurrencySetting(fundsTransfer.Cryptocurrency).Divisibility;
        var netAmount = MathHelper.Truncate(fundsTransfer.Amount, divisibility);

        return new TransactionDAL
        {
            TransactionId = Ulid.NewUlid().ToString(),
            TransactionDate = DateTimeOffset.UtcNow,
            TransactionType = TransactionType.Transfer,
            Amount = netAmount,
            Cryptocurrency = fundsTransfer.Cryptocurrency,
            Price = 0,
            PriceCurrency = Currency.USD,
            UserId = fundsTransfer.UserId,
            AccountId = userAccount.AccountId,
            Status = TransactionStatus.Completed,
            ReferenceNumber = fundsTransfer.FundsTransferId.ToString(CultureInfo.InvariantCulture),
            Description = $"Move funds. {fundsTransfer.Comment}",
            FeeAmount = 0,
            NetAmount = netAmount,
            TransactionSource = TransactionSourceType.Admin,
            Rate = 1,
        };
    }

    private static GeneralLedgerDAL GenerateGeneralLedgerRecord(TransactionDAL transaction, bool isDebit) => new()
    {
        TransactionId = transaction.TransactionId,
        Date = transaction.CreatedAt,
        AccountId = isDebit ? GetGlSourceAccount(transaction) : GetGlTargetAccount(transaction),
        Description = GetGlDescription(transaction.TransactionType),
        DebitAmount = isDebit ? transaction.Amount : 0m,
        CreditAmount = !isDebit ? transaction.Amount : 0m,
        TransactionType = transaction.TransactionType,
        Cryptocurrency = transaction.Cryptocurrency,
        TransactionSource = transaction.TransactionSource,
        ReferenceNumber = transaction.ReferenceNumber,
    };

    private static string GetGlDescription(TransactionType transactionType, bool isDebit = false) => transactionType switch
    {
        TransactionType.Payment => "Payment received from client",
        TransactionType.Refund => "",
        TransactionType.Transfer => "Internal transfer",
        TransactionType.Adjustment => "",
        TransactionType.Payout => isDebit ? "Payout to seller" : "Funds sent to seller",
        TransactionType.RecurringPayment => "",
        TransactionType.Fee => "Fee",
        TransactionType.MovePayment => "Move funds to seller",
        TransactionType.IncomingTransfer => "Income funds to system",
        _ => throw new ArgumentOutOfRangeException(nameof(transactionType), transactionType, null),
    };

    private static int GetGlSourceAccount(TransactionDAL transaction) => transaction.TransactionType switch
    {
        TransactionType.Payment or TransactionType.Transfer or TransactionType.MovePayment =>
            SystemAccount.Debit[transaction.Cryptocurrency],
        TransactionType.Fee => SystemAccount.Fees[transaction.Cryptocurrency],
        TransactionType.Payout => transaction.AccountId,
        _ => throw new ArgumentOutOfRangeException(nameof(transaction), transaction.TransactionType, "Unsupported transaction type."),
    };

    private static int GetGlTargetAccount(TransactionDAL transaction) => transaction.TransactionType switch
    {
        TransactionType.Payment or TransactionType.MovePayment or TransactionType.Fee
            or TransactionType.Transfer or TransactionType.IncomingTransfer => transaction.AccountId,
        TransactionType.Payout => SystemAccount.Payout[transaction.Cryptocurrency],
        _ => throw new ArgumentOutOfRangeException(nameof(transaction), transaction.TransactionType, "Unsupported transaction type."),
    };

    private async Task RevertExpiredInvoice(InvoiceResponseBC invoice, TransactionWorkerTaskDAL task)
    {
        var invoiceStatus = Enum.Parse<InvoiceStatusBC>(invoice.Status, ignoreCase: true);

        if (invoiceStatus != InvoiceStatusBC.Expired)
        {
            return;
        }

        logger.LogInformation("Revert expired invoice {InvoiceId}, order {OrderId}", invoice.Id, task.OrderId);

        await using var dmpContext = await dmpContextFactory.CreateDbContextAsync();
        var orderHdr = await dmpContext.OrderHeaders
            .Include(o => o.OrderLines)
            .FirstAsync(or => or.OrderId == task.OrderId);
        orderHdr.Status = OrderStatus.Expired;

        var affectedProductIds = new List<int>();

        foreach (var orderLine in orderHdr.OrderLines!)
        {
            var product = await dmpContext.Products.FirstAsync(p => p.ProductId == orderLine.ProductId);

            if (product.Unlimited)
            {
                continue;
            }

            affectedProductIds.Add(product.ProductId);

            // Return the reserved quantity
            product.Quantity += orderLine.Quantity;
            dmpContext.Products.Update(product);

            if (product.IsLines)
            {
                // Return the reserved line
                var productLine = await dmpContext.ProductLines.FirstAsync(pl => pl.ProductLineId == orderLine.ProductLineId);
                productLine.IsSold = false;
                dmpContext.ProductLines.Update(productLine);
            }
        }

        task.Status = TransactionWorkerTaskStatus.Complete;
        dmpContext.TransactionWorkerTasks.Update(task);

        await dmpContext.SaveChangesAsync();

        if (affectedProductIds.Count > 0)
        {
            // Ask the job server to refresh the product cache
            dmpContext.JobProductCacheTasks.AddRange(affectedProductIds.Select(id => new JobProductCacheTaskDAL { ProductId = id }));

            await dmpContext.SaveChangesAsync();
        }
    }

    private MailDAL CreateSellerNewSaleEmail(int templateId, UserDAL user, SalesHeaderDAL sale)
    {
        var model = new SellerNewSale
        {
            ButtonLink = $"{_dmpHostsSettings.Seller}/finances/view?id={sale.SalesHeaderId}",
            Year = DateTime.UtcNow.Year.ToString(CultureInfo.InvariantCulture),
            LogoUrl = $"{_minioSettings.S3PublicEndpoint}/{Const.LogoPath}",
            UserName = user.Name,
            SaleId = sale.SalesHeaderId.ToString(CultureInfo.InvariantCulture),
            Amount = $"{sale.Amount} {CryptocurrencyToString(sale.Cryptocurrency)}",
        };

        return new MailDAL
        {
            To = user.Email,
            From = Const.DmpEmails["store"],
            Status = MailStatus.New,
            EmailTemplateId = templateId,
            Model = JsonSerializer.Serialize(model),
        };
    }

    private MailDAL CreateSellerPayoutEmail(int templateId, UserDAL user, PayoutDAL payout)
    {
        var model = new SellerPayout
        {
            ButtonLink = $"{_dmpHostsSettings.Seller}/finances/accounts",
            Year = DateTime.UtcNow.Year.ToString(CultureInfo.InvariantCulture),
            LogoUrl = $"{_minioSettings.S3PublicEndpoint}/{Const.LogoPath}",
            UserName = user.Name,
            Amount = $"{payout.Amount} {CryptocurrencyToString(payout.Cryptocurrency)}",
            NetworkFee = $"{payout.NetworkFee} {CryptocurrencyToString(payout.Cryptocurrency)}",
            Address = WalletHelper.TruncateAddress(payout.Address),
            NetworkTransaction = payout.NetworkTransactionId,
        };

        return new MailDAL
        {
            To = user.Email,
            From = Const.DmpEmails["store"],
            Status = MailStatus.New,
            EmailTemplateId = templateId,
            Model = JsonSerializer.Serialize(model),
        };
    }

    private static string CryptocurrencyToString(Cryptocurrency cryptocurrency) => cryptocurrency switch
    {
        Cryptocurrency.USDT_TRC20 => "USDT (TRC20)",
        Cryptocurrency.USDT_BEP20 => "USDT (BEP20)",
        Cryptocurrency.USDT_ERC20 => "USDT (ERC20)",
        Cryptocurrency.USDT_MATIC => "USDT (MATIC)",
        Cryptocurrency.USDC_ERC20 => "USDC (ERC20)",
        _ => cryptocurrency.ToString(),
    };

    // Bitcart serializes decimals as strings with "." as the separator, independent of the host culture.
    private static decimal ParseBitcartDecimal(string value) => decimal.Parse(value, CultureInfo.InvariantCulture);

    private static DateTime ParseBitcartDate(string value) =>
        DateTime.Parse(value, CultureInfo.InvariantCulture).ToUniversalTime();
}
