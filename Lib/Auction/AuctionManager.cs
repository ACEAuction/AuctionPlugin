using System.Data;
using ACE.Database;
using ACE.Entity;
using ACE.Entity.Models;
using ACE.Mods.Legend.Lib.Auction.Models;
using ACE.Mods.Legend.Lib.Auction.Network.Models;
using ACE.Mods.Legend.Lib.Common.Errors;
using ACE.Mods.Legend.Lib.Database;
using ACE.Mods.Legend.Lib.Database.Models;
using ACE.Server.Factories;
using ACE.Server.Managers;
using Microsoft.EntityFrameworkCore;

namespace ACE.Mods.Legend.Lib.Auction;

public static class AuctionManager
{
    private readonly static object AuctionTickLock = new object();

    private static double NextTickTime = 0;

    private static readonly double TickTime = 5;

    private static readonly ushort MaxAuctionHours = 168;
    private static Settings Settings => PatchClass.Settings;

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
                Log($"Failed to process expired listings: {ex}", ModManager.LogLevel.Error);
            }
        }
    }

    private static void ProcessExpiredListings(double currentUnixTime)
    {
        var expiredListings = DatabaseManager.Shard.BaseDatabase.GetExpiredListings(currentUnixTime, AuctionListingStatus.active);

        foreach (var expiredListing in expiredListings)
            ProcessListing(expiredListing);
    }

    private static void ProcessListing(uint listingId)
    {
        var listing = DatabaseManager.Shard.BaseDatabase.ProcessExpiredListing(listingId);

        if (listing == null)
            throw new AuctionFailure($"Unable to process expired listing with listinId = {listingId}", FailureCode.Auction.Unknown);

        // TODO: create a queue to handle notifications
        var notifierId = listing.HighestBidderId == 0 ? listing.SellerId : listing.HighestBidderId;
        var onlinePlayer = PlayerManager.GetAllOnline()
            .Where(player => player.Account.AccountId == notifierId)
            .FirstOrDefault();

        onlinePlayer?.SendMailNotification();

        Log($"Successfully processed expired listing {listing.Id}");
    }

    public static List<AuctionListing> GetPostAuctionListings(uint accountId, GetPostListingsRequest request)
    {
        return DatabaseManager.Shard.BaseDatabase.GetPostAuctionListings(
            accountId, 
            request.SortBy, 
            request.SortDirection, 
            request.SearchQuery, 
            request.PageNumber, 
            request.PageSize);
    }

    /// <summary>
    /// Collect mail items and send them to a players inventory
    /// </summary>
    /// <param name="player"></param>
    /// <exception cref="AuctionFailure"></exception>
    public static void CollectAuctionInboxItems(Player player, List<uint> inboxItemIds)
    {
        foreach (var id in inboxItemIds)
        {
            var item = DatabaseManager.Shard.BaseDatabase.GetMailItem(id);

            if (item == null)
                continue;

            var biota = DatabaseManager.Shard.BaseDatabase.GetBiota(item.ItemId);

            lock (player.BiotaDatabaseLock)
            {
                if (biota == null)
                    continue;
                //throw new AuctionFailure($"Inbox collection failure, Could not find item with Id = {item.ItemId}", FailureCode.Auction.Unknown);

                if (player.Inventory.ContainsKey(new ObjectGuid(item.ItemId)))
                    continue;
                    //throw new AuctionFailure($"Inbox collection failure, {player.Name} already has item with Id = {item.ItemId} in their inventory", FailureCode.Auction.Unknown);

                var wo = WorldObjectFactory.CreateWorldObject(biota);

                if (player.TryCreateInInventoryWithNetworking(wo))
                    DatabaseManager.Shard.BaseDatabase.RemoveMailItem(item.Id);
                else
                    throw new AuctionFailure($"Failed to add mail item with Id = {item.Id} to {player.Name}'s inventory", FailureCode.Auction.Unknown);
            }
        }
    }

    public static List<MailItem> GetPendingMailItems(uint accountId, uint pageSize, uint pageNumber)
    {
        return DatabaseManager.Shard.BaseDatabase.GetPaginatedMailItems(accountId, MailStatus.pending, pageSize, pageNumber);
    }

    public static AuctionSellOrder CreateAuctionSellOrder(Player player, CreateSellOrderRequest request)
    {
        var currencyWcid = request.CurrencyWcid;
        Weenie currencyWeenie = DatabaseManager.World.GetCachedWeenie(currencyWcid) 
            ?? throw new AuctionFailure($"Failed to get currency name from weenie with WeenieClassId = {currencyWcid}", FailureCode.Auction.SellValidation);

        var sellItem = player.GetInventoryItem(request.ItemId)
            ?? throw new AuctionFailure("The specified item could not be found in the player's inventory.", FailureCode.Auction.ProcessSell);

        var hoursDuration = request.HoursDuration;
        var startTime = DateTime.UtcNow;
        var endTime = Settings.IsDev ? startTime.AddSeconds(request.HoursDuration) : startTime.AddHours(hoursDuration);

        var createSellOrderContext = new CreateSellOrderContext()
        {
            Item = sellItem,
            Seller = player,
            Currency = currencyWeenie,
            NumberOfStacks = request.NumberOfStacks,
            StackSize = request.StackSize,
            StartPrice = request.StartPrice,
            BuyoutPrice = request.BuyoutPrice,
            StartTime = startTime,
            HoursDuration = hoursDuration,
            EndTime = endTime,
            RemovedItems = new List<WorldObject>(),
            RemainingTime = endTime - startTime,
        };

        return ExecuteCreateSellOrder(player, createSellOrderContext);
    }

    private static AuctionSellOrder ExecuteCreateSellOrder(Player player, CreateSellOrderContext createSellOrderContext)
    {
        try
        {
            return DatabaseManager.Shard.BaseDatabase.ExecuteInTransaction(
                executeAction: dbContext =>
                {
                    var sellOrder = DatabaseManager.Shard.BaseDatabase.CreateAuctionSellOrder(dbContext, createSellOrderContext);
                    ProcessSell(player, sellOrder.Id, createSellOrderContext, dbContext);
                    return sellOrder;
                });
        }
        catch (Exception ex)
        {
            HandleCreateSellOrderFailure(player, createSellOrderContext, ex.Message);
            throw;
        }
    }

    private static void ProcessSell(Player player, uint sellOrderId, CreateSellOrderContext createSellOrderContext, AuctionDbContext dbContext)
    {
        var numOfStacks = createSellOrderContext.NumberOfStacks;
        var stackSize = createSellOrderContext.StackSize;
        var item = createSellOrderContext.Item;

        if (createSellOrderContext.HoursDuration > MaxAuctionHours)
            throw new AuctionFailure($"Failed validation for auction sell, an auction end time can not exceed 168 hours (a week)", FailureCode.Auction.SellValidation);

        if (item.ItemWorkmanship != null && (numOfStacks > 1 || stackSize > 1))
            throw new AuctionFailure("A loot-generated item cannot be traded if the number of stacks is greater than 1.", FailureCode.Auction.ProcessSell);

        var totalStacks = numOfStacks * stackSize;

        if (totalStacks > item.StackSize)
            throw new AuctionFailure("The item does not have enough stacks to complete the auction sale.", FailureCode.Auction.ProcessSell);

        var sellItemMaxStackSize = item.MaxStackSize ?? 1;

        for (var i = 0; i < numOfStacks; i++)
        {
            player.RemoveItemForTransfer(item.Guid.Full, out WorldObject removedItem, (int?)stackSize);
            createSellOrderContext.RemovedItems.Add(removedItem);
            DatabaseManager.Shard.BaseDatabase.CreateAuctionListing(dbContext, removedItem.Guid.Full, sellOrderId, createSellOrderContext);
        }
    }

    private static void HandleCreateSellOrderFailure(Player player, CreateSellOrderContext createSellOrderContext, string errorMessage)
    {
        foreach (var removedItem in createSellOrderContext.RemovedItems)
        {
            var subject = $"Create sell order failed: {removedItem.NameWithMaterial}";
            DatabaseManager.Shard.BaseDatabase.SendMailItem(createSellOrderContext.Seller.Account.AccountId, removedItem.Guid.Full, removedItem.IconId, "Auction House", subject);
            player.SendMailNotification();
        }
    }
}
