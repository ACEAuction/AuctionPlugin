namespace ACE.Mods.Legend.Lib.Common.Errors;

public class AuctionFailure : Exception
{
    private const string Prefix = "[AuctionFailure]";

    public AuctionFailure() : base($"{Prefix} Generic Auction Exception.") { }

    public AuctionFailure(string message)
        : base($"{Prefix} {message}") { }

    public AuctionFailure(string message, Exception innerException)
        : base($"{Prefix} {message}", innerException) { }
}

public class ItemTransferFailure : Exception
{
    private const string Prefix = "[ItemTransfer]";

    public ItemTransferFailure()
        : base($"{Prefix} Generic item transfer Exception.") { }

    public ItemTransferFailure(string message)
        : base($"{Prefix} {message}") { }

    public ItemTransferFailure(string message, Exception innerException)
        : base($"{Prefix} {message}", innerException) { }
}
