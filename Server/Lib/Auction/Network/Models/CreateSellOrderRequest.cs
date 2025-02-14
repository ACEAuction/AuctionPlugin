using ACE.Mods.Auction.Lib.Common.Errors;

namespace ACE.Mods.Auction.Lib.Auction.Network.Models;

public class CreateSellOrderRequest
{
    public uint ItemId { get; set; }
    public uint StartPrice { get; set; }
    public uint BuyoutPrice { get; set; }
    public uint NumberOfStacks { get; set; }
    public uint StackSize { get; set; }
    public uint CurrencyWcid { get; set; }
    public uint HoursDuration { get; set; }
    public void Validate()
    {
        if (ItemId <= 0)
            throw new AuctionFailure("ItemId must be greater than 0.", FailureCode.Auction.SellValidation);
        if (StackSize <= 0)
            throw new AuctionFailure("StackSize must be greater than 0.", FailureCode.Auction.SellValidation);
        if (NumberOfStacks <= 0)
            throw new AuctionFailure("NumberOfStacks must be greater than 0.", FailureCode.Auction.SellValidation);
        if (StartPrice < 0)
            throw new AuctionFailure("StartingPrice cannot be negative.", FailureCode.Auction.SellValidation);
        if (BuyoutPrice < 0)
            throw new AuctionFailure("BuyoutPrice cannot be negative.", FailureCode.Auction.SellValidation);
        if (HoursDuration <= 0)
            throw new AuctionFailure("HoursDuration must be greater than 0.", FailureCode.Auction.SellValidation);
        if (CurrencyWcid <= 0)
            throw new AuctionFailure("Invalid Weenie Currency Type.", FailureCode.Auction.SellValidation);
    }
}
