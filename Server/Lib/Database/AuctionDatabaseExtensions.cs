using System.Data;
using System.Linq.Expressions;
using ACE.Database;
using ACE.Database.Models.Shard;
using ACE.Entity.Models;
using ACE.Mods.Auction.Lib.Auction;
using ACE.Mods.Auction.Lib.Auction.Models;
using ACE.Mods.Auction.Lib.Auction.Network.Models;
using ACE.Mods.Auction.Lib.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace ACE.Mods.Auction.Lib.Database;

public static class AuctionDatabaseExtensions
{
    public static T ExecuteInTransaction<T>(
       this ShardDatabase database,
       Func<AuctionDbContext, T> executeAction,
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

                    throw;
                }
            });
        }
    }

    public static MailItem SendMailItem(this ShardDatabase database, AuctionDbContext context, uint receiverId, uint itemId, uint iconId, string from, string subject)
    {
        var mailItem = new MailItem
        {
            Status = MailStatus.pending,
            ReceiverId = receiverId,
            Subject = subject,
            ItemId = itemId,
            IconId = iconId,
            CreatedTime = DateTime.UtcNow,
            From = from
        };

        context.MailItem.Add(mailItem);
        context.SaveChanges();
        return mailItem;
    }

    public static MailItem SendMailItem(this ShardDatabase database, uint receiverId, uint itemId, uint iconId, string from, string subject)
    {
        using (var context = new AuctionDbContext())
        {
            return SendMailItem(database, context, receiverId, itemId, iconId, from, subject);
        }
    }

    public static void RemoveMailItem(this ShardDatabase database, uint mailId)
    {
        using (var context = new AuctionDbContext())
        {
            var item = context.MailItem.Find(mailId);
            if (item != null)
            {
                context.MailItem.Remove(item);
                context.SaveChanges();
            }
        }
    }

    public static MailItem? GetMailItem(this ShardDatabase database, uint mailId)
    {
        using (var context = new AuctionDbContext())
        {
            var item = context.MailItem.Find(mailId);
            return item;
        }
    }

    public static List<MailItem> GetPaginatedMailItems(this ShardDatabase database, uint accountId, MailStatus status, uint pageSize, uint pageNumber)
    {
        using (var context = new AuctionDbContext())
        {
            var pageIndex = Math.Max(0, (int)pageNumber - 1);
            var skipAmount = pageIndex * (int)pageSize;

            var items = context.MailItem
                .AsNoTracking()
                .Where(item => item.ReceiverId == accountId)
                .Skip(skipAmount)
                .Take((int)pageSize)
                .ToList();

            return items;
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

    public static AuctionSellOrder CreateAuctionSellOrder(this ShardDatabase database, AuctionDbContext context, CreateSellOrderContext createSellOrderContextContext)
    {
        var sellOrder = new AuctionSellOrder()
        {
            SellerId = createSellOrderContextContext.Seller.Guid.Full,
        };

        context.AuctionSellOrder.Add(sellOrder);
        context.SaveChanges();

        return sellOrder;
    }

    public static AuctionListing CreateAuctionListing(this ShardDatabase database, AuctionDbContext context, uint itemId, uint sellOrderId, CreateSellOrderContext createSellOrderContext)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));

        var listing = new AuctionListing
        {
            Status = AuctionListingStatus.active,
            SellerId = createSellOrderContext.Seller.Account.AccountId,
            SellerName = createSellOrderContext.Seller.Name,
            SellOrderId = sellOrderId,
            ItemId = itemId,
            ItemName = createSellOrderContext.Item.NameWithMaterial,
            ItemIconId = createSellOrderContext.Item.IconId,
            ItemIconOverlay = createSellOrderContext.Item.IconOverlayId ?? 0,
            ItemIconUnderlay = createSellOrderContext.Item.IconUnderlayId ?? 0,
            ItemIconEffects = (uint)(createSellOrderContext.Item.UiEffects ?? 0),
            ItemInfo = createSellOrderContext.Item.BuildItemInfo(),
            StartPrice = createSellOrderContext.StartPrice,
            BuyoutPrice = createSellOrderContext.BuyoutPrice,
            StackSize = createSellOrderContext.StackSize,
            CurrencyWcid = createSellOrderContext.Currency.WeenieClassId,
            CurrencyIconId = createSellOrderContext.Currency.GetProperty(Entity.Enum.Properties.PropertyDataId.Icon) ?? 0,
            CurrencyIconOverlay = createSellOrderContext.Currency.GetProperty(Entity.Enum.Properties.PropertyDataId.IconOverlay) ?? 0,
            CurrencyIconUnderlay = createSellOrderContext.Currency.GetProperty(Entity.Enum.Properties.PropertyDataId.IconUnderlay) ?? 0,
            CurrencyIconEffects = 0,
            CurrencyName = createSellOrderContext.Currency.GetName(),
            NumberOfStacks = createSellOrderContext.NumberOfStacks,
            StartTime = createSellOrderContext.StartTime,
            EndTime = createSellOrderContext.EndTime
        };

        context.AuctionListing.Add(listing);
        context.SaveChanges();

        return listing;
    }

    public static IQueryable<AuctionListing> GetListingsByPredicate(this ShardDatabase database, AuctionDbContext context, Expression<Func<AuctionListing, bool>> predicate)
    {
        return context.AuctionListing
            .AsNoTracking()
            .Where(predicate);
    }

    public static IQueryable<AuctionListing> GetListingsByAccount(this ShardDatabase database, AuctionDbContext context, uint accountId, AuctionListingStatus status)
    {
        return database.GetListingsByPredicate(context, listing => listing.Status == AuctionListingStatus.active && listing.SellerId == accountId);
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
            (uint)ListingColumn.Duration => sortDirection == (uint)ListingSortDirection.Ascending ? query.OrderBy(a => a.EndTime) : query.OrderByDescending(a => a.EndTime),
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
    public static List<AuctionListing> GetBrowseAuctionListings(
    this ShardDatabase database,
    uint sortColumn,
    uint sortDirection,
    string search,
    uint pageNumber,
    uint pageSize)
    {
        using (var context = new AuctionDbContext())
        {
            var query = database.GetListingsByPredicate(context, listings => listings.Status == AuctionListingStatus.active);

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

    public static List<uint> GetExpiredListings(this ShardDatabase database, double timestamp, AuctionListingStatus status)
    {
        using (var context = new AuctionDbContext())
        {
            return context.AuctionListing
                .Where(auction => Time.GetDateTimeFromTimestamp(timestamp) > auction.EndTime && auction.Status == status)
                .Select(l => l.Id)
                .ToList();
        }
    }

    public static AuctionListing? ProcessExpiredListing(this ShardDatabase database, uint listingId)
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
                    var subject = $"Sell order expired: {expiredListing.ItemName}";
                    database.SendMailItem(dbContext, sellerId, expiredListing.ItemId, expiredListing.ItemIconId, "Auction House", subject);
                }
                else
                {
                    // TODO: Processing for the highest bidder...
                }

                expiredListing.Status = AuctionListingStatus.completed;
                return expiredListing;
            },
            isolationLevel: IsolationLevel.Serializable);
    }
}
