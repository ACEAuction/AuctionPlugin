using ACE.Database;
using ACE.Entity;
using ACE.Entity.Models;
using ACE.Mods.Legend.Lib.Common;
using ACE.Mods.Legend.Lib.Common.Errors;
using ACE.Mods.Legend.Lib.Database;
using ACE.Mods.Legend.Lib.Database.Models;
using ACE.Server.Factories;
using ACE.Server.Managers;
using ACE.Shared;

namespace ACE.Mods.Legend.Lib.Auction;

public static class AuctionManager
{
    private readonly static object AuctionTickLock = new object();

    private static double NextTickTime = 0;

    private static readonly double TickTime = 5;

    private static void Log(string message, ModManager.LogLevel level = ModManager.LogLevel.Info)
    {
        ModManager.Log($"[AuctionHouse] {message}", level);
    }


    public static void Tick(double currentUnixTime)
    {
        if (ServerManager.ShutdownInProgress)
            return;


        if (NextTickTime > currentUnixTime)
            return;

        NextTickTime = currentUnixTime + TickTime;

        lock (AuctionTickLock)
        {
            try
            {
                var activeListing = GetExpiredListing(currentUnixTime);
                if (activeListing != null)
                {
                    //Log($"Active listing Id = {activeListing.Id}");

                    //ProcessExpiredListing(activeListing);
                }
            }
            catch (Exception ex)
            {
                Log($"Tick, Error occurred: {ex}", ModManager.LogLevel.Error);
            }
        }
    }

    private static AuctionListing? GetExpiredListing(double currentUnixTime)
    {
        return DatabaseManager.Shard.BaseDatabase.GetExpiredListings(currentUnixTime, 0)?.FirstOrDefault();
    }

    private static void ProcessExpiredListing(AuctionListing activeListing)
    {
        var sellerId = activeListing.SellerId;
        var sellerName = activeListing.SellerName;
        var highestBidderId = activeListing.HighestBidId;
        var highestBidId = activeListing.HighestBidId;

        DatabaseManager.Shard.BaseDatabase.ExecuteInTransaction(
            executeAction: dbContext =>
            {
                if (highestBidderId == 0)
                {
                    DatabaseManager.Shard.BaseDatabase.SendMailItem(dbContext, sellerId, activeListing.ItemId, "Auction House");
                }
                else
                {
                    var highestBid = DatabaseManager.Shard.BaseDatabase.GetAuctionBid(highestBidId);
                    var highestBidderName = highestBid?.BidderName ?? string.Empty;
                    List<AuctionBidItem> bidItems = highestBid?.AuctionBidItems.ToList() ?? new List<AuctionBidItem>();

                    foreach (var item in bidItems)
                    {
                        DatabaseManager.Shard.BaseDatabase.SendMailItem(dbContext, sellerId, item.ItemId, highestBidderName);
                    }

                    DatabaseManager.Shard.BaseDatabase.SendMailItem(dbContext, highestBidderId, activeListing.ItemId, sellerName);
                }

                activeListing.Status = AuctionListingStatus.completed;

                return activeListing;
            },
            failureAction: (ex) =>
            {
                DatabaseManager.Shard.BaseDatabase.UpdateListingStatus(activeListing.Id, AuctionListingStatus.failed);
            },
            System.Data.IsolationLevel.Serializable);
    }
}
