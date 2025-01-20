namespace ACE.Mods.Legend.Lib.Common.Errors;

public static class FailureCode
{
    public enum Auction: uint 
    {
        Unknown = 0,
        SellValidation = 1,
        TransferItemFailure = 2,
        ProcessSell = 3,
        GetListingsRequest = 4,
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
    public AuctionFailure(string message, FailureCode.Auction code) : base(message, (uint)code)
    {
    }
}
