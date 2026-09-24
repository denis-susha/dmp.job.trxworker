using DMP.DataAccess.Models;

namespace DMP.BL.Services;

public interface ITransactionService
{
    Task ProcessPayment(TransactionWorkerTaskDAL transactionWorkerTask);
    Task ProcessPayout(TransactionWorkerTaskDAL transactionWorkerTask);
    Task ProcessIncomingTransfer(TransactionWorkerTaskDAL transactionWorkerTask);
    Task ProcessBonus(TransactionWorkerTaskDAL transactionWorkerTask);
}
