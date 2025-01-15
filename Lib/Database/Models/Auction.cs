using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACE.Mods.Legend.Lib.Database.Models;

public enum AuctionListingStatus
{
    active,
    completed,
    cancelled
}

public enum MailStatus
{
    pending,
    sent,
    failed
}

public partial class AuctionListing
{
    public uint Id { get; set; }
    public uint ItemId { get; set; }
    public uint SellerId { get; set; }
    public uint StartPrice { get; set; }
    public uint BuyoutPrice { get; set; }
    public uint StackSize { get; set; }
    public uint NumberOfStacks { get; set; }

    public uint CurrencyType { get; set; }
    public uint? HighestBidAmount { get; set; }
    public uint? HighestBidId { get; set; }
    public uint? HighestBidderId { get; set; }
    public AuctionListingStatus Status { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }

    public ICollection<AuctionBid> Bids { get; set; }
}

public partial class AuctionBid
{
    public uint Id { get; set; }
    public uint BidderId { get; set; }
    public uint AuctionListingId { get; set; }
    public uint BidAmount { get; set; }
    public bool Resolved { get; set; }
    public DateTime BidTime { get; set; }

    public AuctionListing AuctionListing { get; set; }
    public ICollection<AuctionBidItem> AuctionBidItems { get; set; }
}

public partial class AuctionBidItem
{
    public uint Id { get; set; }
    public uint BidId { get; set; }
    public uint ItemId { get; set; }
    public AuctionBid AuctionBid { get; set; }
}

public partial class MailItem
{
    public uint Id { get; set; }
    public string From { get; set; }
    public uint ItemId { get; set; }
    public uint ReceiverId { get; set; }

    public MailStatus Status { get; set; }
}

