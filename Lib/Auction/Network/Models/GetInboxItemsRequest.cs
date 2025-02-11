using ACE.Mods.Legend.Lib.Common.Errors;

namespace ACE.Mods.Legend.Lib.Auction.Network.Models;

public class GetInboxItemsRequest
{
    public uint PageSize { get; set; }
    public uint PageNumber { get; set; }
    public void Validate()
    {
        if (PageSize == 0)
            throw new AuctionFailure("PageSize must be greter than 0.", FailureCode.Auction.GetInboxItemsRequest);

        if (PageNumber == 0)
            throw new AuctionFailure("PageNumber must be greater than 0.", FailureCode.Auction.GetInboxItemsRequest);
    }
}
