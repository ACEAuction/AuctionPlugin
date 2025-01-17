using System.Runtime.CompilerServices;
using ACE.Database;
using ACE.Database.Models.Shard;
using ACE.Mods.Legend.Lib.Auction.Models;
using ACE.Mods.Legend.Lib.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ACE.Mods.Legend.Lib.Database;

public static class AuctionDatabaseExtensions
{
    public static T ExecuteInTransaction<T>(
       this ShardDatabase database,
       Func<AuctionDbContext, T> executeAction,
       Action<Exception>? failureAction = null,
       System.Data.IsolationLevel isolationLevel = System.Data.IsolationLevel.ReadCommitted)
    {
        using (var context = new AuctionDbContext())
        {
            var executionStrategy = context.Database.CreateExecutionStrategy();

            return executionStrategy.Execute(() =>
            {
                using var transaction = context.Database.BeginTransaction(isolationLevel);
                var stopwatch = Stopwatch.StartNew();

                try
                {
                    var result = executeAction(context);
                    context.SaveChanges();
                    transaction.Commit();
                    stopwatch.Stop();
                    ModManager.Log($"[DATABASE] Transaction executed and committed successfully in {stopwatch.Elapsed.TotalSeconds:F4} seconds using isolation level {isolationLevel}.", ModManager.LogLevel.Debug);
                    return result;
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    ModManager.Log($"[DATABASE] Transaction failed after {stopwatch.Elapsed.TotalSeconds:F4} seconds using isolation level {isolationLevel}, rolling back.", ModManager.LogLevel.Error);
                    ModManager.Log(ex.ToString(), ModManager.LogLevel.Error);

                    try
                    {
                        transaction.Rollback();
                    }
                    catch (Exception rollbackEx)
                    {
                        ModManager.Log($"[DATABASE] Transaction rollback failed: {rollbackEx}", ModManager.LogLevel.Error);
                    }

                    failureAction?.Invoke(ex);
                    throw;
                }
            });
        }
    }

    public static void SendMailItem(this ShardDatabase database, AuctionDbContext context, uint receiverId, uint itemId, string from)
    {
        var mailItem = new MailItem
        {
            Status = MailStatus.pending,
            ReceiverId = receiverId,
            ItemId = itemId,
            From = from
        };

        context.MailItem.Add(mailItem);
        context.SaveChanges();
    }

    public static void SendMailItem(this ShardDatabase database, uint receiverId, uint itemId, string from)
    {
        using (var context = new AuctionDbContext())
        {
            SendMailItem(database, context, receiverId, itemId, from);
        }
    }

    public static AuctionListing? GetActiveAuctionListing(this ShardDatabase database, uint sellerId, uint itemId)
    {
        using (var context = new AuctionDbContext())
        {
            var result = context.AuctionListing
                .AsNoTracking()
                .Where(a => a.SellerId == sellerId && a.ItemId == itemId && a.Status == AuctionListingStatus.active)
                .FirstOrDefault();

            return result;
        }
    }

    public static bool UpdateListingStatus(this ShardDatabase database, uint listingId, AuctionListingStatus status)
    {
        using (var context = new AuctionDbContext())
        {
            var listing = context.AuctionListing.SingleOrDefault(listing => listing.Id == listingId);

            if (listing != null)
            {
                listing.Status = status;
                context.SaveChanges();
                return true;
                
            } else
            {
                return false;
            }
        }
    }

    public static AuctionSellOrder PlaceAuctionSellOrder(this ShardDatabase database, AuctionDbContext context, CreateAuctionSell createAuctionSell)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));


        var sellOrder = new AuctionSellOrder()
        {
            SellerId = createAuctionSell.SellerId,
        };

        context.AuctionSellOrder.Add(sellOrder);
        context.SaveChanges();

        return sellOrder;
    }
    public static AuctionListing PlaceAuctionListing(this ShardDatabase database, AuctionDbContext context, uint itemId, uint sellOrderId, CreateAuctionSell createAuctionSell)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));

        var listing = new AuctionListing
        {
            Status = AuctionListingStatus.active,
            SellerId = createAuctionSell.SellerId,
            SellerName = createAuctionSell.SellerName,
            SellOrderId = sellOrderId,
            ItemId = itemId,
            CurrencyType = createAuctionSell.CurrencyType,
            StartPrice = createAuctionSell.StartPrice,
            BuyoutPrice = createAuctionSell.BuyoutPrice,
            StackSize = createAuctionSell.StackSize,
            NumberOfStacks = createAuctionSell.NumberOfStacks,
            StartTime = createAuctionSell.StartTime,
            EndTime = createAuctionSell.EndTime
        };

        context.AuctionListing.Add(listing);
        context.SaveChanges();

        return listing;
    }

    public static List<AuctionListing> GetActiveAuctionListings(this ShardDatabase database)
    {
        using (var context = new AuctionDbContext())
        {
            return context.AuctionListing
                .AsNoTracking()
                .Where(auction => auction.Status == AuctionListingStatus.active)
                .OrderByDescending(item => item.EndTime)
                .ToList();
        }
    }

    public static AuctionBid? GetAuctionBid(this ShardDatabase database, uint bidId)
    {
        using (var context = new AuctionDbContext())
        {
            return context.AuctionBid
                .AsNoTracking()
                .Where(auction => auction.Id == bidId)
                .FirstOrDefault();
        }
    }
}
