namespace ACE.Mods.Legend.Lib.Common.Errors;

public static class FailureCode
{
    public enum Auction: uint 
    {
        Unknown = 0,
        InvalidCurrencyFailure = 1,
        UniqueItemFailure = 2,
        InsufficientAmountFailure = 3,
        ItemNotFoundFailure = 4,
        DurationLimitReached = 5,

        TransferItemNotFoundFailure = 6,
        TransferItemToFailure = 7,
        TransferItemFromFailure = 8,
        TransferBusyFailure = 9,
        TransferItemAttunedFailure = 10,

        TransferItemsInTradeWindowFailure = 11,
        TransferRemoveItemForGiveFailure = 12,
        TransferItemFromBankFailure = 13,
        TransferItemToBankFailure = 14,
        IncompleteStack = 15,
    }
    public enum Bank: uint 
    {

    }
}

public abstract class ChoriziteFailure: Exception
{
    public readonly uint Code;

    public ChoriziteFailure(string message, uint code) : base($"{message}")
    {
        Code = code;
    }
}


public class AuctionFailure : ChoriziteFailure
{
    private const string Prefix = "[AuctionFailure]";

    public AuctionFailure(string message, FailureCode.Auction code) : base($"{Prefix} {message}", (uint)code)
    {
    }
}

public class BankFailure : ChoriziteFailure
{
    private const string Prefix = "[BankFailure]";

    public BankFailure(string message, FailureCode.Bank code) : base($"{Prefix} {message}", (uint)code)
    {
    }
}
