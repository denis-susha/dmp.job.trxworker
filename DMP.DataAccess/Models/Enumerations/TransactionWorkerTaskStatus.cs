namespace DMP.DataAccess.Models.Enumerations;

public enum TransactionWorkerTaskStatus : byte
{
    New = 0,
    IncomeTrxCreated = 1,
    MoveFundsTrxCreated = 2,
    SalesCreated = 3,
    PayoutCreated = 10,
    Complete = 20,
    Error = 21
}
