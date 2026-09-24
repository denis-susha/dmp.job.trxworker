namespace DMP.DataAccess.Models.Enumerations;

public enum ProductStatus : byte
{
    New = 0, // creation
    Built = 1, // user edit is finished
    Moderated = 2, // on moderation
    Ready = 3, // on sale
    NeedWork = 10, // need correction by seller
    PausedByUser = 11, // by seller
    Paused = 12, // by DMP
    Inactive = 15, // out of work
}
