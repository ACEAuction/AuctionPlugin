namespace ACE.Mods.Legend.Lib.Common.Errors;

public enum FailureStatusCode
{

    UnknownError,
    UnknownCurrency
}


public class AuctionFailure: Exception
{
    public readonly FailureStatusCode Code;

    public AuctionFailure(string message, FailureStatusCode code = FailureStatusCode.UnknownError) : base($"{message}")
    {
        Code = code;
    }
}


public class AuctionProcessFailure : AuctionFailure
{
    private const string Prefix = "[AuctionProcessFailure]";

    public AuctionProcessFailure(string message, FailureStatusCode code = FailureStatusCode.UnknownError) : base($"{Prefix} {message}", code)
    {
    }
}

public class ItemTransferFailure : AuctionFailure
{
    private const string Prefix = "[ItemTransferFailure]";

    public ItemTransferFailure(string message, FailureStatusCode code = FailureStatusCode.UnknownError) : base($"{Prefix} {message}", code)
    {
    }
}
