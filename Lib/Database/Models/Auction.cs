namespace ACE.Mods.Legend.Lib.Database.Models;

public enum AuctionListingStatus
{
    active,
    completed,
    cancelled,
    failed
}

public enum MailStatus
{
    pending,
    sent,
    failed
}

public partial class AuctionSellOrder
{
    public uint Id { get; set; }
    public uint SellerId { get; set; }
    public ICollection<AuctionListing> Listings { get; set; }
}

public partial class AuctionListing
{
    public uint Id { get; set; }
    public uint SellOrderId { get; set; }
    public uint ItemId { get; set; }
    public uint ItemIconId { get; set; }
    public uint ItemIconOverlay { get; set; }
    public uint ItemIconUnderlay { get; set; }
    public uint ItemIconEffects{ get; set; }
    public string ItemName {  get; set; }
    public string ItemInfo { get; set; }
    public uint SellerId { get; set; }
    public string SellerName { get; set; }
    public uint StartPrice { get; set; }
    public uint BuyoutPrice { get; set; }
    public uint StackSize { get; set; }
    public uint NumberOfStacks { get; set; }
    public uint CurrencyWcid { get; set; }
    public uint CurrencyIconId { get; set; }
    public uint CurrencyIconOverlay { get; set; }
    public uint CurrencyIconUnderlay { get; set; }
    public uint CurrencyIconEffects { get; set; }
    public string CurrencyName { get; set; }
    public uint HighestBidAmount { get; set; } = 0;
    public uint HighestBidId { get; set; } = 0;
    public uint HighestBidderId { get; set; } = 0;
    public string HighestBidderName { get; set; } = "";
    public AuctionListingStatus Status { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }

    [JsonIgnore]
    public AuctionSellOrder SellOrder { get; set; }
    public ICollection<AuctionBid> Bids { get; set; }
}

public partial class AuctionBid
{
    public uint Id { get; set; }
    public uint BidderId { get; set; }
    public string BidderName { get; set; }
    public uint AuctionListingId { get; set; }
    public uint BidAmount { get; set; }
    public bool Resolved { get; set; }
    public DateTime BidTime { get; set; }

    [JsonIgnore]
    public AuctionListing AuctionListing { get; set; }
    [JsonIgnore]
    public ICollection<AuctionBidItem> AuctionBidItems { get; set; }
}

public partial class AuctionBidItem
{
    public uint Id { get; set; }
    public uint BidId { get; set; }
    public uint ItemId { get; set; }
    [JsonIgnore]
    public AuctionBid AuctionBid { get; set; }
}

public partial class MailItem
{
    public uint Id { get; set; }
    public string From { get; set; }
    public uint ItemId { get; set; }
    public uint ReceiverId { get; set; }
    public string Subject { get; set; } = "";
    public uint IconId { get; set; }
    public DateTime CreatedTime { get; set; }


    public MailStatus Status { get; set; }
}

