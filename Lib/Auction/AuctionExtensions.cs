using ACE.Database;
using ACE.Entity.Models;
using ACE.Mods.Legend.Lib.Common;
using ACE.Mods.Legend.Lib.Common.Errors;
using ACE.Server.Command.Handlers;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Shared;
using static ACE.Server.WorldObjects.Player;
using ACE.Mods.Legend.Lib.Database;
using ACE.Mods.Legend.Lib.Auction.Models;
using ACE.Mods.Legend.Lib.Database.Models;


namespace ACE.Mods.Legend.Lib.Auction;

public static class AuctionExtensions
{
    static Settings Settings => PatchClass.Settings;

    private static readonly ushort MaxAuctionHours = 168; 

    private const string AuctionPrefix = "[AuctionHouse]";

    public static void SendAuctionMessage(this Player player, string message, ChatMessageType messageType = ChatMessageType.System)
    {
        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"{AuctionPrefix} {message}", messageType));
    }


    /*private static void ValidateAuctionBid(this Player player, WorldObject listing, uint bidAmount)
    {
        if (listing.GetListingStatus() != "active")
            throw new AuctionFailure($"Failed to place auction bid, the listing for this bid is not currently active", FailureCode.Auction.Unknown);

        var sellerId = listing.GetSellerId();

        if (sellerId > 0 && sellerId == player.Account.AccountId)
            throw new AuctionFailure($"Failed to place auction bid, you cannot bid on items you are selling", FailureCode.Auction.Unknown);

        if (listing.GetHighestBidder() == player.Account.AccountId)
            throw new AuctionFailure($"Failed to place auction bid, you are already the highest bidder", FailureCode.Auction.Unknown);

        var listingHighBid = listing.GetHighestBid();

        if (listingHighBid > 0 && listingHighBid > bidAmount)
            throw new AuctionFailure($"Failed to place auction bid, your bid isn't high enough", FailureCode.Auction.Unknown);

        var currencyType = listing.GetCurrencyType();

        if (currencyType == 0)
            throw new AuctionFailure($"Failed to place auction bid, this listing does not have a currency type", FailureCode.Auction.Unknown);

        var listingStartPrice = listing.GetListingStartPrice();

        var endTime = listing.GetListingEndTimestamp();

        if (endTime > 0 && Time.GetDateTimeFromTimestamp(endTime) < DateTime.UtcNow)
            throw new AuctionFailure($"Failed to place auction bid, this listing has already expired", FailureCode.Auction.Unknown);

        var numOfItems = player.GetNumInventoryItemsOfWCID((uint)currencyType);

        if (bidAmount < listingStartPrice)
            throw new AuctionFailure($"Failed to place auction bid, your bid amount is less than the starting price", FailureCode.Auction.Unknown);

        if (numOfItems < listingHighBid || numOfItems < listingStartPrice)
            throw new AuctionFailure($"Failed to place auction bid, you do not have enough currency items to bid on this listing", FailureCode.Auction.Unknown);
    }

    public static void PlaceAuctionBid(this Player player, uint listingId, uint bidAmount)
    {
        var listing = AuctionManager.GetListingById(listingId) ?? throw new AuctionFailure($"Listing with Id = {listingId} does not exist", FailureCode.Auction.Unknown);

        List<WorldObject> newBidItems = new List<WorldObject>();

        List<WorldObject> oldBidItems = AuctionManager.ItemsContainer.Inventory.Values
                     .Where(item => item.GetBidOwnerId() > 0 && item.GetBidOwnerId() == listing.GetHighestBidder()).ToList();

        var previousState = CapturePreviousBidState(listing);

        try
        {
            ValidateAuctionBid(player, listing, bidAmount);
            RemovePreviousBidItems(listing, previousState.PreviousBidderId, oldBidItems);
            PlaceNewBid(player, listing, bidAmount, newBidItems);
            UpdateListingWithNewBid(listing, player, bidAmount);
            NotifyPlayerOfSuccess(player, listing, bidAmount);
        }
        catch (AuctionFailure ex)
        {
            HandleBidFailure(ex, player, listing, previousState, oldBidItems, newBidItems);
        }
    }

    private static (uint PreviousBidderId, string PreviousBidderName, int PreviousHighBid) CapturePreviousBidState(WorldObject listing)
    {
        return (
            PreviousBidderId: listing.GetHighestBidder(),
            PreviousBidderName: listing.GetHighestBidderName(),
            PreviousHighBid: listing.GetHighestBid()
        );
    }

    private static void RemovePreviousBidItems(WorldObject listing, uint previousBidderId, List<WorldObject> oldBidItems)
    {
        foreach (var item in oldBidItems)
        {
            if (!AuctionManager.TryRemoveFromInventory(item) ||
                !BankManager.TryAddToInventory(item, previousBidderId))
                throw new AuctionFailure($"Failed to process previous bid items for listing {listing.Guid.Full}", FailureCode.Auction.Unknown);

            ResetBidItemProperties(item, previousBidderId);
        }
    }

    private static void ResetBidItemProperties(WorldObject item, uint previousBidderId)
    {
        item.SetProperty(FakeIID.BidOwnerId, 0);
        item.SetProperty(FakeIID.ListingId, 0);
        item.SetProperty(FakeIID.BankId, previousBidderId);
    }

    private static void PlaceNewBid(Player player, WorldObject listing, uint bidAmount, List<WorldObject> newBidItems)
    {
        List<WorldObject> bidItems = player.GetInventoryItemsOfWCID((uint)listing.GetCurrencyType()).ToList();

        var remainingAmount = (int)bidAmount;
        var bidTime = DateTime.UtcNow;

        foreach (var item in bidItems)
        {
            if (remainingAmount <= 0) break;

            var amount = Math.Min(item.StackSize ?? 1, remainingAmount);
            player.RemoveItemForTransfer(item.Guid.Full, out var removedItem, amount);
            ConfigureBidItem(removedItem, player.Account.AccountId, listing.Guid.Full, bidTime);

            if (!AuctionManager.TryAddToInventory(removedItem))
                throw new AuctionFailure($"Failed to add bid item to Auction Items Chest", FailureCode.Auction.Unknown);

            remainingAmount -= amount;
            newBidItems.Add(removedItem);
        }

        if (remainingAmount > 0)
            throw new AuctionFailure($"Insufficient bid items to meet the bid amount for listing {listing.GetListingId()}", FailureCode.Auction.Unknown);
    }

    private static void ConfigureBidItem(WorldObject item, uint bidderId, uint listingId, DateTime bidTime)
    {
        item.SetProperty(FakeFloat.BidTimestamp, Time.GetUnixTime(bidTime));
        item.SetProperty(FakeIID.BidOwnerId, bidderId);
        item.SetProperty(FakeIID.ListingId, listingId);
    }

    private static void UpdateListingWithNewBid(WorldObject listing, Player player, uint bidAmount)
    {
        listing.SetProperty(FakeIID.HighestBidderId, player.Account.AccountId);
        listing.SetProperty(FakeString.HighestBidderName, player.Name);
        listing.SetProperty(FakeInt.ListingHighBid, (int)bidAmount);
    }

    private static void NotifyPlayerOfSuccess(Player player, WorldObject listing, uint bidAmount)
    {
        player.SendAuctionMessage(
            $"Successfully created an auction bid on listing with Id = {listing.Guid.Full}, Seller = {listing.GetSellerName()}, BidAmount = {bidAmount}",
            ChatMessageType.Broadcast
        );
    }

    private static void HandleBidFailure(AuctionFailure ex, Player player, WorldObject listing, (uint PreviousBidderId, string PreviousBidderName, int PreviousHighBid) previousState, List<WorldObject> oldBidItems, List<WorldObject> newBidItems)
    {
        LogErrorAndNotifyPlayer(ex, player);
        RestorePreviousListingState(listing, previousState);
        RevertOldBidItems(oldBidItems, previousState.PreviousBidderId, listing.Guid.Full);
        RevertNewBidItems(player, newBidItems);
    }

    private static void LogErrorAndNotifyPlayer(AuctionFailure ex, Player player)
    {
        ModManager.Log(ex.Message, ModManager.LogLevel.Error);
        player.SendAuctionMessage(ex.Message);
    }

    private static void RestorePreviousListingState(WorldObject listing, (uint PreviousBidderId, string PreviousBidderName, int PreviousHighBid) previousState)
    {
        listing.SetProperty(FakeIID.HighestBidderId, previousState.PreviousBidderId);
        listing.SetProperty(FakeString.HighestBidderName, previousState.PreviousBidderName);
        listing.SetProperty(FakeInt.ListingHighBid, previousState.PreviousHighBid);
    }

    private static void RevertOldBidItems(List<WorldObject> oldBidItems, uint previousBidderId, uint listingId)
    {
        foreach (var item in oldBidItems)
        {
            item.SetProperty(FakeIID.BankId, 0);
            item.SetProperty(FakeIID.BidOwnerId, previousBidderId);
            item.SetProperty(FakeIID.ListingId, listingId);

            BankManager.TryRemoveFromInventory(item, previousBidderId);
            AuctionManager.TryAddToInventory(item);
        }
    }

    private static void RevertNewBidItems(Player player, List<WorldObject> removedNewBidItems)
    {
        foreach (var item in removedNewBidItems)
        {
            item.RemoveProperty(FakeIID.BidOwnerId);
            item.RemoveProperty(FakeIID.ListingId);

            AuctionManager.TryRemoveFromInventory(item);
            player.TryCreateInInventoryWithNetworking(item);
        }
    }*/

    public class AuctionSellContext
    {
        public List<WorldObject> RemovedItems { get; }
        public AuctionListing Listing { get; set; }
        public string CurrencyName { get; }
        public TimeSpan RemainingTime { get; }

        public AuctionSellContext(List<WorldObject> removedItems, string currencyName, TimeSpan remainingTime)
        {
            RemovedItems = removedItems ?? throw new ArgumentNullException(nameof(removedItems));
            CurrencyName = currencyName ?? throw new ArgumentNullException(nameof(currencyName));
            RemainingTime = remainingTime;
        }
    }

    public static AuctionListing PlaceAuctionSell(this Player player, CreateAuctionListing createAuctionListing)
    {
        string currencyName = GetCurrencyName(createAuctionListing.CurrencyType);
        var remainingTime = createAuctionListing.EndTime - createAuctionListing.StartTime;

        var sellContext = new AuctionSellContext(
            new List<WorldObject>(),
            currencyName,
            remainingTime
        );

        return DatabaseManager.Shard.BaseDatabase.ExecuteInTransaction(
            executeAction: dbContext =>
            {
                var auctionListing = DatabaseManager.Shard.BaseDatabase.GetActiveAuctionListing(createAuctionListing.SellerId, createAuctionListing.ItemId);

                if (auctionListing != null)
                    throw new AuctionFailure($"Failed validation for auction sell, an auction already exists for that item", FailureCode.Auction.SellValidation);

                var listing = DatabaseManager.Shard.BaseDatabase.PlaceAuctionListing(dbContext, createAuctionListing);

                if (listing == null)
                    throw new AuctionFailure($"Failed validation for auction sell, failed to create auction listing in the database", FailureCode.Auction.SellValidation);

                sellContext.Listing = listing;

                if (createAuctionListing.HoursDuration > MaxAuctionHours)
                    throw new AuctionFailure($"Failed validation for auction sell, an auction end time can not exceed 168 hours (a week)", FailureCode.Auction.SellValidation);

                ProcessSell(player, sellContext);
                return listing; 
            },
            failureAction: exception =>
            {
                if (exception is AuctionFailure)
                {
                    ModManager.Log(exception.Message, ModManager.LogLevel.Error);
                    HandleAuctionSellFailure(player, sellContext, exception.Message);
                }
            });
    }

    private static string GetCurrencyName(uint currencyType)
    {
        var weenie = DatabaseManager.World.GetCachedWeenie(currencyType);
        if (weenie == null)
            throw new AuctionFailure($"Failed to get currency name from weenie with WeenieClassId = {currencyType}", FailureCode.Auction.SellValidation);
        return weenie.GetName();
    }

    private static void ProcessSell(Player player, AuctionSellContext context)
    {
        var numOfStacks = context.Listing.NumberOfStacks;
        var stackSize = context.Listing.StackSize;
        var listingId = context.Listing.Id;

        var sellItem = player.GetInventoryItem(context.Listing.ItemId)
            ?? throw new AuctionFailure("The specified item could not be found in the player's inventory.", FailureCode.Auction.ProcessSell);

        if (sellItem.ItemWorkmanship != null && numOfStacks > 1)
            throw new AuctionFailure("A loot-generated item cannot be traded if the number of stacks is greater than 1.", FailureCode.Auction.ProcessSell);

        var sellItemMaxStackSize = sellItem.MaxStackSize ?? 1;

        if (stackSize > sellItemMaxStackSize)
            throw new AuctionFailure("Item max stack size is lower than provided stack size.", FailureCode.Auction.ProcessSell);

        var totalItemsRequired = (int)(stackSize * numOfStacks);

        int transferAmount = Math.Min(sellItem.StackSize ?? 1, totalItemsRequired);

        player.RemoveItemForTransfer(sellItem.Guid.Full, out WorldObject removedItem, transferAmount);

        context.RemovedItems.Add(removedItem);
    }

    private static void HandleAuctionSellFailure(Player player, AuctionSellContext sellContext, string errorMessage)
    {
        foreach (var removedItem in sellContext.RemovedItems)
        {
            DatabaseManager.Shard.BaseDatabase.SendMailItem(sellContext.Listing.SellerId, removedItem.Guid.Full, "Auction House");
        }
    }

    public static bool HasItemOnPerson(this Player player, uint itemId, out WorldObject foundItem)
    {
        foundItem = player.FindObject(itemId, SearchLocations.MyInventory | SearchLocations.MyEquippedItems, out var itemFoundInContainer, out var itemRootOwner, out var itemWasEquipped);
        return foundItem != null;
    }

    private static void RemoveItemForTransfer(this Player player, uint itemToTransfer, out WorldObject itemToRemove, int? amount = null)
    {
        if (player.IsBusy || player.Teleporting || player.suicideInProgress)
            throw new AuctionFailure($"The item cannot be transferred, you are too busy", FailureCode.Auction.TransferItemFailure);

        var item = player.FindObject(itemToTransfer, SearchLocations.MyInventory | SearchLocations.MyEquippedItems, out var itemFoundInContainer, out var itemRootOwner, out var itemWasEquipped);

        if (item == null)
            throw new AuctionFailure($"The item cannot be transferred, item with Id = {itemToTransfer} was not found on your person", FailureCode.Auction.TransferItemFailure);

        if (item.IsAttunedOrContainsAttuned)
            throw new AuctionFailure($"The item cannot be transferred {item.NameWithMaterial} is attuned", FailureCode.Auction.TransferItemFailure);

        if (player.IsTrading && item.IsBeingTradedOrContainsItemBeingTraded(player.ItemsInTradeWindow))
            throw new AuctionFailure($"The item cannot be transferred {item.NameWithMaterial}, the item is currently being traded", FailureCode.Auction.TransferItemFailure);

        var removeAmount = amount.HasValue ? amount.Value : item.StackSize ?? 1;

        if (!player.RemoveItemForGive(item, itemFoundInContainer, itemWasEquipped, itemRootOwner, removeAmount, out WorldObject itemToGive))
            throw new AuctionFailure($"The item cannot be transferred {item.NameWithMaterial}, failed to remove item from location", FailureCode.Auction.TransferItemFailure);

        itemToRemove = itemToGive;
    }
}
