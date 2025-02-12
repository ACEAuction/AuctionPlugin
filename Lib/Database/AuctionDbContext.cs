using ACE.Database.Models.Shard;
using ACE.Mods.Auction.Lib.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace ACE.Mods.Auction.Lib.Database;

public partial class AuctionDbContext : ShardDbContext
{
    public virtual DbSet<AuctionSellOrder> AuctionSellOrder { get; set; }
    public virtual DbSet<AuctionListing> AuctionListing { get; set; }
    public virtual DbSet<AuctionBid> AuctionBid { get; set; }
    public virtual DbSet<AuctionBidItem> AuctionBidItem { get; set; }
    public virtual DbSet<MailItem> MailItem { get; set; }
}
