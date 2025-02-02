using System.Runtime.CompilerServices;
using ACE.Database;
using ACE.Database.Models.Shard;
using ACE.Entity.Models;
using ACE.Mods.Legend.Lib.Auction;
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

    public static AuctionSellOrder PlaceAuctionSellOrder(this ShardDatabase database, AuctionDbContext context, CreateSellOrder createAuctionSell)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));


        var sellOrder = new AuctionSellOrder()
        {
            SellerId = createAuctionSell.Seller.Guid.Full,
        };

        context.AuctionSellOrder.Add(sellOrder);
        context.SaveChanges();

        return sellOrder;
    }
    public static AuctionListing PlaceAuctionListing(this ShardDatabase database, AuctionDbContext context, uint itemId, uint sellOrderId, CreateSellOrder createAuctionSell)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));

        var listing = new AuctionListing
        {
            Status = AuctionListingStatus.active,
            SellerId = createAuctionSell.Seller.Account.AccountId,
            SellerName = createAuctionSell.Seller.Name,
            SellOrderId = sellOrderId,
            ItemId = itemId,
            ItemName = createAuctionSell.Item.Name,
            ItemIconId = createAuctionSell.Item.IconId,
            ItemIconOverlay = createAuctionSell.Item.IconOverlayId ?? 0,
            ItemIconUnderlay = createAuctionSell.Item.IconUnderlayId ?? 0,
            ItemIconEffects = (uint)(createAuctionSell.Item.UiEffects ?? 0),
            ItemInfo = createAuctionSell.Item.BuildItemInfo(),
            StartPrice = createAuctionSell.StartPrice,
            BuyoutPrice = createAuctionSell.BuyoutPrice,
            StackSize = createAuctionSell.StackSize,
            CurrencyWcid = createAuctionSell.Currency.WeenieClassId,
            CurrencyIconId = createAuctionSell.Currency.GetProperty(Entity.Enum.Properties.PropertyDataId.Icon) ?? 0,
            CurrencyIconOverlay = createAuctionSell.Currency.GetProperty(Entity.Enum.Properties.PropertyDataId.IconOverlay) ?? 0, 
            CurrencyIconUnderlay = createAuctionSell.Currency.GetProperty(Entity.Enum.Properties.PropertyDataId.IconUnderlay) ?? 0, 
            CurrencyIconEffects = 0, 
            CurrencyName = createAuctionSell.Currency.GetName(),
            NumberOfStacks = createAuctionSell.NumberOfStacks,
            StartTime = createAuctionSell.StartTime,
            EndTime = createAuctionSell.EndTime
        };

        context.AuctionListing.Add(listing);
        context.SaveChanges();

        return listing;
    }

    public static IQueryable<AuctionListing> GetListingsByAccount(this ShardDatabase database, AuctionDbContext context, uint accountId, AuctionListingStatus status)
    {
        var query = context.AuctionListing
            .AsNoTracking()
            .Where(listing => listing.Status == status && listing.SellerId == accountId);

        return query;
    }

    public static IQueryable<AuctionListing> GetListingsByAccount(this ShardDatabase database, uint accountId, AuctionListingStatus status)
    {
        using (var context = new AuctionDbContext())
        {
            return database.GetListingsByAccount(context, accountId, status);
        }
    }

    public static IQueryable<AuctionListing> ApplyListingsSearchFilter(this ShardDatabase database, IQueryable<AuctionListing> query, string searchQuery)
    {
        if (!string.IsNullOrEmpty(searchQuery))
        {
            query = query.Where(a => a.ItemInfo.ToLower().Contains(searchQuery.ToLower()));
        }

        return query;
    }

    public static IQueryable<AuctionListing> ApplyListingsSortFilter(
        this ShardDatabase database,
        IQueryable<AuctionListing> query,
        uint sortColumn,
        uint sortDirection)
    {
        query = sortColumn switch
        {
            (uint)ListingColumn.Name => sortDirection == (uint)ListingSortDirection.Ascending ? query.OrderBy(a => a.ItemName) : query.OrderByDescending(a => a.ItemName),
            (uint)ListingColumn.StackSize => sortDirection == (uint)ListingSortDirection.Ascending ? query.OrderBy(a => a.StackSize) : query.OrderByDescending(a => a.StackSize),
            (uint)ListingColumn.BuyoutPrice => sortDirection == (uint)ListingSortDirection.Ascending ? query.OrderBy(a => a.BuyoutPrice) : query.OrderByDescending(a => a.BuyoutPrice),
            (uint)ListingColumn.StartPrice => sortDirection == (uint)ListingSortDirection.Ascending ? query.OrderBy(a => a.StartPrice) : query.OrderByDescending(a => a.StartPrice),
            (uint)ListingColumn.Seller => sortDirection == (uint)ListingSortDirection.Ascending ? query.OrderBy(a => a.SellerName) : query.OrderByDescending(a => a.SellerName),
            (uint)ListingColumn.Currency => sortDirection == (uint)ListingSortDirection.Ascending ? query.OrderBy(a => a.CurrencyName) : query.OrderByDescending(a => a.CurrencyName),
            (uint)ListingColumn.HighestBidder => sortDirection == (uint)ListingSortDirection.Ascending ? query.OrderBy(a => a.HighestBidderName) : query.OrderByDescending(a => a.HighestBidderName),
            _ => query.OrderBy(a => a.ItemName), 
        };

        return query;
    }

    public static List<AuctionListing> GetPostAuctionListings(
    this ShardDatabase database,
    uint accountId,
    uint sortColumn,
    uint sortDirection,
    string search,
    uint pageNumber,
    uint pageSize)
    {
        using (var context = new AuctionDbContext())
        {
            var query = database.GetListingsByAccount(context, accountId, AuctionListingStatus.active);

            // Apply filtering before sorting
            var filteredQuery = database.ApplyListingsSearchFilter(query, search);
            var sortedQuery = database.ApplyListingsSortFilter(filteredQuery, sortColumn, sortDirection);

            var pageIndex = Math.Max(0, (int)pageNumber - 1);
            var skipAmount = pageIndex * (int)pageSize;

            return sortedQuery
                .Skip(skipAmount)
                .Take((int)pageSize)
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

    public static List<AuctionListing>? GetExpiredListings(this ShardDatabase database, double timestamp, uint status)
    {
        using (var context = new AuctionDbContext())
        {
            return context.AuctionListing
                .AsNoTracking()
                .Where(auction => (uint)auction.Status == status)
                .Where(auction => Time.GetDateTimeFromTimestamp(timestamp) > auction.EndTime)
                .ToList();
        }
    }
}
