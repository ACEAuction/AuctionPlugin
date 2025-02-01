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

public class GetPostListingsRequest
{
    public string SearchQuery { get; set; } = string.Empty;
    public uint SortBy { get; set; } 
    public uint SortDirection { get; set; }
    public uint PageSize { get; set; } = 15;
    public uint PageNumber { get; set; }

    public void Validate()
    {
        if (SortBy == 0 || !Enum.IsDefined(typeof(ListingColumn), (int)SortBy))
            throw new AuctionFailure("Invalid SortBy value provided.", FailureCode.Auction.GetPostListingsRequest);

        if (SortDirection == 0 || !Enum.IsDefined(typeof(ListingSortDirection), (int)SortDirection))
            throw new AuctionFailure("Invalid SortDirection value provided.", FailureCode.Auction.GetPostListingsRequest);

        if (PageSize == 0)  
            throw new AuctionFailure("PageSize must be greter than 0.", FailureCode.Auction.GetPostListingsRequest);

        if (PageNumber == 0)
            throw new AuctionFailure("PageNumber must be greater than 0.", FailureCode.Auction.GetPostListingsRequest);
    }
}
