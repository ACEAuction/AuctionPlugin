using System.Data;
using ACE.Database;
using ACE.Mods.Legend.Lib.Database;
using ACE.Mods.Legend.Lib.Database.Models;
using ACE.Server.Managers;
using Microsoft.EntityFrameworkCore;

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
                ProcessExpiredListings(currentUnixTime);
            }
            catch (Exception ex)
            {
                Log($"Tick, Error occurred: {ex}", ModManager.LogLevel.Error);
            }
        }
    }

    private static void ProcessExpiredListings(double currentUnixTime)
    {
        var expiredListings = DatabaseManager.Shard.BaseDatabase.GetExpiredListings(currentUnixTime, AuctionListingStatus.active);

        foreach (var expiredListing in expiredListings)
        {
            ProcessExpiredListing(expiredListing);
        }
    }

    private static AuctionListing? ProcessExpiredListing(uint listingId)
    {
        return DatabaseManager.Shard.BaseDatabase.ExecuteInTransaction(
            executeAction: dbContext =>
            {
                var expiredListing = dbContext.AuctionListing.Find(listingId);
                if (expiredListing == null) return null;  

                var sellerId = expiredListing.SellerId;
                var sellerName = expiredListing.SellerName;
                var highestBidderId = expiredListing.HighestBidderId;
                var highestBidId = expiredListing.HighestBidId;

                if (highestBidderId == 0)
                {
                    DatabaseManager.Shard.BaseDatabase.SendMailItem(dbContext, sellerId, expiredListing.ItemId, "Auction House");
                }
                else
                {
                    // Processing for the highest bidder...
                }

                expiredListing.Status = AuctionListingStatus.completed;
                Log($"Successfully processed expired listing {expiredListing.Id}");

                return expiredListing;
            },
            isolationLevel: IsolationLevel.Serializable);
    }
}
