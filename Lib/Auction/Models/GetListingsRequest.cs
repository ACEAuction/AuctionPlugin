using ACE.Mods.Legend.Lib.Common.Errors;

namespace ACE.Mods.Legend.Lib.Auction.Models;
public enum ListingColumn
{
    Name = 1,
    StackSize = 2,
    BuyoutPrice = 3,
    StartPrice = 4,
    Seller = 5,
    Currency = 6,
    HighestBidder = 7,
}

public enum ListingSortDirection
{
    Ascending = 1,
    Descending = 2
}

public class GetListingsRequest
{
    public string SearchQuery { get; set; } = string.Empty;
    public uint SortBy { get; set; } 
    public uint SortDirection { get; set; }

    public void Validate()
    {
        if (SortBy == 0 || !Enum.IsDefined(typeof(ListingColumn), (int)SortBy))
            throw new AuctionFailure("Invalid SortBy value provided.", FailureCode.Auction.GetListingsRequest);

        if (SortDirection == 0 || !Enum.IsDefined(typeof(ListingSortDirection), (int)SortDirection))
            throw new AuctionFailure("Invalid SortDirection value provided.", FailureCode.Auction.GetListingsRequest);
    }

}
