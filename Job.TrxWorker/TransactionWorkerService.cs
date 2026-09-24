using DMP.BL.Services;
using DMP.DataAccess;
using DMP.DataAccess.Models;
using DMP.DataAccess.Models.Enumerations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Job.TrxWorker;

/// <summary>
/// Polls the TransactionWorkerTask table and hands each pending task to <see cref="ITransactionService"/>.
/// </summary>
public class TransactionWorkerService(
    ILogger<TransactionWorkerService> logger,
    IDbContextFactory<DmpDbContext> dmpContextFactory,
    ITransactionService transactionService) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);

    // A task that keeps failing is parked in the Error status after this many attempts.
    private const int MaxAttempts = 10;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Transaction worker is starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingTasks();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while processing transaction tasks");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("Transaction worker is stopping");
    }

    private async Task ProcessPendingTasks()
    {
        var transactionTasks = await GetTransactionTasks();
        if (transactionTasks is null)
        {
            return;
        }

        foreach (var transactionTask in transactionTasks)
        {
            logger.LogInformation(
                "Processing {Type} task {InvoiceId} with status {Status}",
                transactionTask.Type, transactionTask.InvoiceId, transactionTask.Status);

            try
            {
                var processing = transactionTask.Type switch
                {
                    TransactionWorkerTaskType.Payment => transactionService.ProcessPayment(transactionTask),
                    TransactionWorkerTaskType.Payout => transactionService.ProcessPayout(transactionTask),
                    TransactionWorkerTaskType.IncomingTransfer => transactionService.ProcessIncomingTransfer(transactionTask),
                    TransactionWorkerTaskType.Bonus => transactionService.ProcessBonus(transactionTask),
                    _ => throw new ArgumentOutOfRangeException(nameof(transactionTask), transactionTask.Type, "Unknown task type."),
                };

                await processing;
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "Failed to process {Type} task {InvoiceId}", transactionTask.Type, transactionTask.InvoiceId);

                // Only the attempt counter is persisted. The in-memory task may carry a status set by a step
                // whose save failed; writing it would mark that step as done although its data was never stored.
                await using var context = await dmpContextFactory.CreateDbContextAsync();
                await context.TransactionWorkerTasks
                    .Where(t => t.InvoiceId == transactionTask.InvoiceId)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(t => t.Attempts, t => t.Attempts + 1)
                        .SetProperty(t => t.Status, t => t.Attempts + 1 > MaxAttempts ? TransactionWorkerTaskStatus.Error : t.Status));
            }
        }
    }

    private async Task<List<TransactionWorkerTaskDAL>?> GetTransactionTasks()
    {
        try
        {
            await using var context = await dmpContextFactory.CreateDbContextAsync();
            return await context.TransactionWorkerTasks
                .Where(t => t.Status <= TransactionWorkerTaskStatus.Complete)
                .OrderBy(t => t.CreatedAt)
                .AsNoTracking()
                .ToListAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while querying transaction tasks");
            return null;
        }
    }
}
