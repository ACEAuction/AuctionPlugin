using ACE.Database;
using ACE.Database.Models.Shard;
using ACE.Mods.Legend.Lib.Auction.Models;
using ACE.Mods.Legend.Lib.Database.Models;
using Microsoft.EntityFrameworkCore;

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
                        ModManager.Log($"[DATABASE] Transaction rollback failed: {rollbackEx.Message}", ModManager.LogLevel.Error);
                    }

                    failureAction?.Invoke(ex);
                    throw;
                }
            });
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

    public static AuctionListing PlaceAuctionListing(this ShardDatabase database, AuctionDbContext context, CreateAuctionListing createAuctionListing)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));

        var entry = new AuctionListing
        {
            Status = AuctionListingStatus.active,
            SellerId = createAuctionListing.SellerId,
            ItemId = createAuctionListing.ItemId,
            StackSize = createAuctionListing.StackSize,
            NumberOfStacks = createAuctionListing.NumberOfStacks,
            CurrencyType = createAuctionListing.CurrencyType,
            HighestBidAmount = 0,
            StartPrice = createAuctionListing.StartPrice,
            BuyoutPrice = createAuctionListing.BuyoutPrice,
            StartTime = createAuctionListing.StartTime,
            EndTime = createAuctionListing.EndTime
        };

        context.AuctionListing.Add(entry);
        context.SaveChanges();

        return entry;
    }
}
