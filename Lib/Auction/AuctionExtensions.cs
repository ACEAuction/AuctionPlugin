using ACE.Database;
using ACE.Entity.Models;
using ACE.Mods.Legend.Lib.Common;
using ACE.Mods.Legend.Lib.Common.Errors;
using ACE.Mods.Legend.Lib.Bank;
using ACE.Server.Command.Handlers;
using ACE.Server.Entity.Actions;
using ACE.Server.Factories;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Shared;
using static ACE.Server.WorldObjects.Player;

namespace ACE.Mods.Legend.Lib.Auction;

public static class AuctionExtensions
{
    static Settings Settings => PatchClass.Settings;

    private static readonly ushort MaxAuctionHours = 168; // a week is the longest duration for an auction, this could be a server property

    private const string AuctionPrefix = "[AuctionHouse]";

    public static uint GetListingId(this WorldObject item) =>
        item.GetProperty(FakeIID.ListingId) ?? 0;
    public static uint GetSellerId(this WorldObject item) =>
        item.GetProperty(FakeIID.SellerId) ?? 0;
    public static uint GetListingOwnerId(this WorldObject item) =>
        item.GetProperty(FakeIID.ListingOwnerId) ?? 0;
    public static int GetListingStartPrice(this WorldObject item) =>
        item.GetProperty(FakeInt.ListingStartPrice) ?? 0;
    public static int GetCurrencyType(this WorldObject item) =>
        item.GetProperty(FakeInt.ListingCurrencyType) ?? 0;
    public static int GetHighestBid(this WorldObject item) =>
        item.GetProperty(FakeInt.ListingHighBid) ?? 0;
    public static uint GetHighestBidder(this WorldObject item) =>
        item.GetProperty(FakeIID.HighestBidderId) ?? 0;
    public static string GetHighestBidderName(this WorldObject item) =>
        item.GetProperty(FakeString.HighestBidderName) ?? "";
    public static string GetSellerName(this WorldObject item) =>
        item.GetProperty(FakeString.SellerName) ?? "";
    public static string GetListingStatus(this WorldObject item) =>
        item.GetProperty(FakeString.ListingStatus) ?? "";
    public static double GetListingStartTimestamp(this WorldObject item) =>
        item.GetProperty(FakeFloat.ListingStartTimestamp) ?? 0;
    public static double GetListingEndTimestamp(this WorldObject item) =>
        item.GetProperty(FakeFloat.ListingEndTimestamp) ?? 0;
    public static double GetBidTimestamp(this WorldObject item) =>
        item.GetProperty(FakeFloat.BidTimestamp) ?? 0;
    public static uint GetBidOwnerId(this WorldObject item) =>
        item.GetProperty(FakeIID.BidOwnerId) ?? 0;
    public static bool GetAuctionTagging(this Player player) =>
        player.GetProperty(FakeBool.IsAuctionTagging) ?? false;
    public static uint GetBankId(this WorldObject item) =>
        item.GetProperty(FakeIID.BankId) ?? 0;

    public static void SendAuctionMessage(this Player player, string message, ChatMessageType messageType = ChatMessageType.System)
    {
        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"{AuctionPrefix} {message}", messageType));
    }

    public static void ValidateAuctionSell(this Player player, ushort hoursDuration)
    {
        if (hoursDuration > MaxAuctionHours)
            throw new AuctionFailure($"Failed validation for auction sell, an auction end time can not exceed 168 hours (a week)");
    }

    public static void ValidateAuctionTag(this Player player, uint tagId, out WorldObject taggedItem)
    {
        if (AuctionManager.TaggedItems.TryGetValue(player.Guid.Full, out var items) && items != null && items.Contains(tagId))
            throw new AuctionFailure($"The tagged item with Id = {tagId} is already tagged");

        var item = player.FindObject(tagId, SearchLocations.MyInventory | SearchLocations.MyEquippedItems, out var itemFoundInContainer, out var itemRootOwner, out var itemWasEquipped);

        if (item == null)
            throw new AuctionFailure($"The tagged item with Id = {tagId} was not found on your person");

        if (item.IsAttunedOrContainsAttuned)
            throw new AuctionFailure($"{item.NameWithMaterial} cannot be tagged, it is attuned or bonded");

        if (player.IsTrading && item.IsBeingTradedOrContainsItemBeingTraded(player.ItemsInTradeWindow))
            throw new AuctionFailure($"The item {item.NameWithMaterial} cannot be tagged, the item is currently being traded");

        taggedItem = item;
    }

    private static void ValidateAuctionBid(this Player player, WorldObject listing, uint bidAmount)
    {
        if(listing.GetListingStatus() != "active")
            throw new AuctionFailure($"Failed to place auction bid, the listing for this bid is not currently active");

        var sellerId = listing.GetSellerId();

        if (sellerId > 0 && sellerId == player.Guid.Full) 
            throw new AuctionFailure($"Failed to place auction bid, you cannot bid on items you are selling");

        if (listing.GetHighestBidder() == player.Guid.Full)
            throw new AuctionFailure($"Failed to place auction bid, you are already the highest bidder");

        var listingHighBid = listing.GetHighestBid();

        if (listingHighBid > 0 && listingHighBid > bidAmount)
            throw new AuctionFailure($"Failed to place auction bid, your bid isn't high enough");

        var currencyType = listing.GetCurrencyType();

        if (currencyType == 0)
            throw new AuctionFailure($"Failed to place auction bid, this listing does not have a currency type");

        var listingStartPrice = listing.GetListingStartPrice();

        var endTime = listing.GetListingEndTimestamp();

        if (endTime > 0 && Time.GetDateTimeFromTimestamp(endTime) < DateTime.UtcNow)
            throw new AuctionFailure($"Failed to place auction bid, this listing has already expired");

        var numOfItems = player.GetNumInventoryItemsOfWCID((uint)currencyType);

        if (bidAmount < listingStartPrice)
            throw new AuctionFailure($"Failed to place auction bid, your bid amount is less than the starting price");

        if (numOfItems < listingHighBid || numOfItems < listingStartPrice )
            throw new AuctionFailure($"Failed to place auction bid, you do not have enough currency items to bid on this listing");
    }

    public static void PlaceAuctionBid(this Player player, uint listingId, uint bidAmount)
    {
        List<WorldObject> removedNewBidItems = new List<WorldObject>();
        List<WorldObject> removedOldBidItems = new List<WorldObject>();
        var listing = AuctionManager.GetListingById(listingId) ?? throw new AuctionFailure($"Listing with Id = {listingId} does not exist");

        var previousState = CapturePreviousBidState(listing);

        try
        {
            ValidateAuctionBid(player, listing, bidAmount);
            RemovePreviousBidItems(listing, previousState.PreviousBidderId, removedOldBidItems);

            var bidItems = player.GetInventoryItemsOfWCID((uint)listing.GetCurrencyType());
            PlaceNewBid(player, listing, bidAmount, bidItems, removedNewBidItems);

            UpdateListingWithNewBid(listing, player, bidAmount);
            NotifyPlayerOfSuccess(player, listing, bidAmount);
        }
        catch (AuctionFailure ex)
        {
            HandleBidFailure(ex, player, listing, previousState, removedOldBidItems, removedNewBidItems);
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

    private static void RemovePreviousBidItems(WorldObject listing, uint previousBidderId, List<WorldObject> removedOldBidItems)
    {
        foreach (var item in AuctionManager.ItemsContainer.Inventory.Values
                     .Where(item => item.GetBidOwnerId() > 0 && item.GetBidOwnerId() == listing.GetHighestBidder()))
        {
            if (!AuctionManager.TryRemoveFromItemsContainer(item) ||
                !BankManager.TryAddToBankContainer(item))
                throw new AuctionFailure($"Failed to process previous bid items for listing {listing.Guid.Full}");

            ResetBidItemProperties(item, previousBidderId);
            removedOldBidItems.Add(item);
        }
    }

    private static void ResetBidItemProperties(WorldObject item, uint previousBidderId)
    {
        item.SetProperty(FakeIID.BidOwnerId, 0);
        item.SetProperty(FakeIID.ListingId, 0);
        item.SetProperty(FakeIID.BankId, previousBidderId);
    }

    private static void PlaceNewBid(Player player, WorldObject listing, uint bidAmount, IEnumerable<WorldObject> bidItems, List<WorldObject> removedNewBidItems)
    {
        var remainingAmount = (int)bidAmount;
        var bidTime = DateTime.UtcNow;

        foreach (var item in bidItems)
        {
            if (remainingAmount <= 0) break;

            var amount = Math.Min(item.StackSize ?? 1, remainingAmount);
            player.RemoveItemForTransfer(item.Guid.Full, out var removedItem, amount);
            ConfigureBidItem(removedItem, player.Guid.Full, listing.GetListingId(), bidTime);

            if (!AuctionManager.TryAddToItemsContainer(removedItem))
                throw new AuctionFailure($"Failed to add bid item to Auction Items Chest");

            remainingAmount -= amount;
            removedNewBidItems.Add(removedItem);
        }

        if (remainingAmount > 0)
            throw new AuctionFailure($"Insufficient bid items to meet the bid amount for listing {listing.GetListingId()}");
    }

    private static void ConfigureBidItem(WorldObject item, uint bidderId, uint listingId, DateTime bidTime)
    {
        item.SetProperty(FakeFloat.BidTimestamp, Time.GetUnixTime(bidTime));
        item.SetProperty(FakeIID.BidOwnerId, bidderId);
        item.SetProperty(FakeIID.ListingId, listingId);
    }

    private static void UpdateListingWithNewBid(WorldObject listing, Player player, uint bidAmount)
    {
        listing.SetProperty(FakeIID.HighestBidderId, player.Guid.Full);
        listing.SetProperty(FakeString.HighestBidderName, player.Name);
        listing.SetProperty(FakeInt.ListingHighBid, (int)bidAmount);
    }

    private static void NotifyPlayerOfSuccess(Player player, WorldObject listing, uint bidAmount)
    {
        player.SendAuctionMessage(
            $"Successfully created an auction bid on listing with Id = {listing.GetListingId()}, Seller = {listing.GetSellerName()}, BidAmount = {bidAmount}",
            ChatMessageType.Broadcast
        );
    }

    private static void HandleBidFailure(AuctionFailure ex, Player player, WorldObject listing, (uint PreviousBidderId, string PreviousBidderName, int PreviousHighBid) previousState, List<WorldObject> removedOldBidItems, List<WorldObject> removedNewBidItems)
    {
        LogErrorAndNotifyPlayer(ex, player);
        RestorePreviousListingState(listing, previousState);
        RevertOldBidItems(removedOldBidItems, previousState.PreviousBidderId, listing.GetListingId());
        RevertNewBidItems(player, removedNewBidItems);
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

    private static void RevertOldBidItems(List<WorldObject> removedOldBidItems, uint previousBidderId, uint listingId)
    {
        foreach (var item in removedOldBidItems)
        {
            item.SetProperty(FakeIID.BankId, 0);
            item.SetProperty(FakeIID.BidOwnerId, previousBidderId);
            item.SetProperty(FakeIID.ListingId, listingId);

            if (!BankManager.TryRemoveFromBankContainer(item) ||
                !AuctionManager.TryAddToItemsContainer(item))
                throw new AuctionFailure("Failed to restore previous bid items");
        }
    }

    private static void RevertNewBidItems(Player player, List<WorldObject> removedNewBidItems)
    {
        foreach (var item in removedNewBidItems)
        {
            item.RemoveProperty(FakeIID.BidOwnerId);
            item.RemoveProperty(FakeIID.ListingId);

            if (!AuctionManager.TryRemoveFromItemsContainer(item) ||
                (!player.TryCreateInInventoryWithNetworking(item) && !BankManager.TryAddToBankContainer(item)))
                throw new AuctionFailure("Failed to restore new bid items to the player or bank");
        }
    }

    public class AuctionSellState
    {
        public List<WorldObject> RemovedItems { get; }
        public Book ListingParchment { get; }
        public string CurrencyName { get; }
        public TimeSpan RemainingTime { get; }

        public AuctionSellState(List<WorldObject> removedItems, Book listingParchment, string currencyName, TimeSpan remainingTime)
        {
            RemovedItems = removedItems ?? throw new ArgumentNullException(nameof(removedItems));
            ListingParchment = listingParchment ?? throw new ArgumentNullException(nameof(listingParchment));
            CurrencyName = currencyName ?? throw new ArgumentNullException(nameof(currencyName));
            RemainingTime = remainingTime;
        }
    }

    public static void PlaceAuctionSell(this Player player, List<uint> itemList, uint currencyType, uint startPrice, ushort hoursDuration)
    {
        var startTime = DateTime.UtcNow;

        var endTime = Settings.IsDev ? startTime.AddSeconds(hoursDuration) : startTime.AddHours(hoursDuration);
        Book listingParchment = (Book)WorldObjectFactory.CreateNewWorldObject(365);
        string currencyName = GetCurrencyName(currencyType);

        // Initialize the auction state using the new constructor
        var state = new AuctionSellState(
            new List<WorldObject>(),
            listingParchment,
            currencyName,
            endTime - startTime
        );

        try
        {
            player.ValidateAuctionSell(hoursDuration);
            InitializeListingParchment(player, currencyType, startPrice, startTime, endTime, state);
            RemoveItemsForListing(player, itemList, state);
            TransferItemsToAuctionContainer(state);
            AddListingToAuctionContainer(state);
            FinalizeAuctionListing(player, state);
            player.ClearTags();
        }
        catch (AuctionFailure ex)
        {
            HandleAuctionSellFailure(player, state, ex.Message);
            throw;
        }
    }

    private static string GetCurrencyName(uint currencyType)
    {
        var weenie = DatabaseManager.World.GetCachedWeenie(currencyType);
        if (weenie == null)
            throw new AuctionFailure($"Failed to get currency name from weenie with Id = {currencyType}");
        return weenie.GetName();
    }

    private static void InitializeListingParchment(Player player, uint currencyType, uint startPrice, DateTime startTime, DateTime endTime, AuctionSellState state)
    {
        var parchment = state.ListingParchment;

        parchment.Name = "Auction Listing Invoice";
        parchment.SetProperty(FakeIID.SellerId, player.Guid.Full);
        parchment.SetProperty(FakeString.SellerName, player.Name);
        parchment.SetProperty(FakeInt.ListingCurrencyType, (int)currencyType);
        parchment.SetProperty(FakeInt.ListingStartPrice, (int)startPrice);
        parchment.SetProperty(FakeFloat.ListingStartTimestamp, (double)Time.GetUnixTime(startTime));
        parchment.SetProperty(FakeFloat.ListingEndTimestamp, (double)Time.GetUnixTime(endTime));
        parchment.SetProperty(FakeString.ListingStatus, "active");

        ModManager.Log($"[AuctionHouse] Initialized listing parchment with Id = {parchment.Guid.Full}", ModManager.LogLevel.Warn);
    }

    private static void RemoveItemsForListing(Player player, List<uint> itemList, AuctionSellState state)
    {
        foreach (var itemId in itemList)
        {
            player.RemoveItemForTransfer(itemId, out var removedItem);
            removedItem.SetProperty(FakeIID.ListingId, state.ListingParchment.Guid.Full);
            removedItem.SetProperty(FakeIID.ListingOwnerId, player.Guid.Full);
            state.RemovedItems.Add(removedItem);
        }
    }

    private static void TransferItemsToAuctionContainer(AuctionSellState state)
    {
        foreach (var item in state.RemovedItems)
        {
            if (item == null || !AuctionManager.TryAddToItemsContainer(item))
            {
                throw new AuctionFailure($"Failed to transfer listing item {item?.Name} to the auction container.");
            }
        }
    }

    private static void AddListingToAuctionContainer(AuctionSellState state)
    {
        if (!AuctionManager.TryAddToListingsContainer(state.ListingParchment))
        {
            throw new AuctionFailure($"Failed to transfer listing parchment {state.ListingParchment.Name} to the auction container.");
        }
    }
    private static void FinalizeAuctionListing(Player player, AuctionSellState state)
    {
        var remaining = Helpers.FormatTimeRemaining(state.RemainingTime);
        player.SendAuctionMessage($"Successfully created an auction listing with Id = {state.ListingParchment.Guid.Full}, Seller = {player.Name}, Currency = {state.CurrencyName}, TimeRemaining = {remaining}", ChatMessageType.Broadcast);

        foreach (var item in state.RemovedItems)
        {
            var message = $"--> Id = {item.Guid.Full}, {Helpers.BuildItemInfo(item)}, Count = {item.StackSize ?? 1}";
            player.SendAuctionMessage(message);
        }
        player.SetProperty(FakeBool.IsAuctionTagging, false);
        player.SendAuctionMessage("auction tagging has been disabled");
    }

    private static void HandleAuctionSellFailure(Player player, AuctionSellState state, string errorMessage)
    {
        ModManager.Log(errorMessage, ModManager.LogLevel.Error);
        player.SendAuctionMessage("Placing auction listing failed");
        player.SendAuctionMessage(errorMessage);

        foreach (var removedItem in state.RemovedItems)
        {
            removedItem.RemoveProperty(FakeIID.ListingId);

            if (!player.HasItemOnPerson(removedItem.Guid.Full, out _))
            {
                var actionChain = new ActionChain();
                actionChain.AddDelaySeconds(0.5);
                actionChain.AddAction(player, () =>
                {
                    player.SendAuctionMessage($"Attempting to return listing item {removedItem.NameWithMaterial}");
                    AuctionManager.TryRemoveFromItemsContainer(removedItem);

                    if (!player.TryCreateInInventoryWithNetworking(removedItem))
                    {
                        player.SendAuctionMessage($"Failed to return listing item {removedItem.NameWithMaterial}, attempting to send it to the bank.");
                        BankManager.TryAddToBankContainer(removedItem);
                    }

                    player.EnqueueBroadcast(new GameMessageSound(player.Guid, Sound.ReceiveItem));
                });
                actionChain.EnqueueChain();
            }
        }

        player.TryConsumeFromInventoryWithNetworking(state.ListingParchment.Guid.Full);
        state.ListingParchment.Destroy();
    }

    public static bool HasItemOnPerson(this Player player, uint itemId, out WorldObject foundItem)
    {
        foundItem = player.FindObject(itemId, SearchLocations.MyInventory | SearchLocations.MyEquippedItems, out var itemFoundInContainer, out var itemRootOwner, out var itemWasEquipped);
        return foundItem != null;
    }

    public static void RemoveItemForTransfer(this Player player, uint itemToTransfer, out WorldObject itemToRemove, int? amount = null)
    {
        if (player.IsBusy || player.Teleporting || player.suicideInProgress)
            throw new ItemTransferFailure($"The item cannot be transferred, you are too busy");

        var item = player.FindObject(itemToTransfer, SearchLocations.MyInventory | SearchLocations.MyEquippedItems, out var itemFoundInContainer, out var itemRootOwner, out var itemWasEquipped);

        if (item == null)
            throw new ItemTransferFailure($"The item cannot be transferred, item with Id = {itemToTransfer} was not found on your person");

        if (item.IsAttunedOrContainsAttuned)
            throw new ItemTransferFailure($"The item cannot be transferred {item.NameWithMaterial} is attuned");

        if (player.IsTrading && item.IsBeingTradedOrContainsItemBeingTraded(player.ItemsInTradeWindow))
            throw new ItemTransferFailure($"The item cannot be transferred {item.NameWithMaterial}, the item is currently being traded");

        var removeAmount = amount.HasValue ? amount.Value : item.StackSize ?? 1;

        if (!player.RemoveItemForGive(item, itemFoundInContainer, itemWasEquipped, itemRootOwner, removeAmount, out WorldObject itemToGive))
            throw new ItemTransferFailure($"The item cannot be transferred {item.NameWithMaterial}, failed to remove item from location");

        itemToRemove = itemToGive;
    }

    public static void InspectTagItem(this Player player, uint itemId)
    {
        try
        {
            player.ValidateAuctionTag(itemId, out WorldObject item);
            player.SendAuctionMessage($"Auction Tag Information, Id = {item.Guid.Full}, {Helpers.BuildItemInfo(item)}", ChatMessageType.Broadcast);
            player.AddTagItem(itemId);
        } catch(AuctionFailure ex)
        {
            player.SendAuctionMessage(ex.Message);
        }

    }

    public static void AddTagItem(this Player player, uint itemId)
    {
        player.ValidateAuctionTag(itemId, out WorldObject item);

        AuctionManager.TaggedItems.AddOrUpdate(
            player.Guid.Full,
            _ => new HashSet<uint> { itemId },
            (_, existingSet) =>
            {
                lock (existingSet)
                {
                    existingSet.Add(itemId);
                }
                return existingSet;
            }
        );

        player.SendAuctionMessage($"Added tagged listing item {item.NameWithMaterial}", ChatMessageType.Broadcast);
    }

    public static void ListTags(this Player player)
    {
        if (AuctionManager.TaggedItems.TryGetValue(player.Guid.Full, out var items) && items.Count > 0)
        {
            StringBuilder message = new StringBuilder();

            lock (items)
            {
                message.Append("^^^^^^^^^^^^^^^^^^^^^^^^^\n");
                message.Append("Auction Sell Tagged List\n");
                message.Append("-------------------------\n");
                foreach (var id in items)
                {
                    var item = player.FindObject(id, SearchLocations.MyInventory | SearchLocations.MyEquippedItems, out var itemFoundInContainer, out var itemRootOwner, out var itemWasEquipped);

                    if (item == null)
                        message.Append($"--> Id = {id}  Unable to find item\n");
                    else
                        message.Append($"--> Id = {item.Guid.Full}, {Helpers.BuildItemInfo(item)}\n");

                    message.Append("-------------------------\n");
                }
                message.Append("^^^^^^^^^^^^^^^^^^^^^^^^^\n");
            }

            CommandHandlerHelper.WriteOutputInfo(player.Session, message.ToString(), ChatMessageType.Broadcast);
        }
        else
            player.Session.Network.EnqueueSend(new GameMessageSystemChat("You don't have any tagged items", ChatMessageType.System));
    }

    public static void RemoveTagItem(this Player player, uint itemId)
    {
        lock (AuctionManager.TaggedItems)
        {
            if (AuctionManager.TaggedItems.TryGetValue(player.Guid.Full, out var items))
            {
                if (items.Remove(itemId))
                {
                    player.SendAuctionMessage($"You have removed item with Id = {itemId} from your tagged list");
                }
                else
                {
                    player.SendAuctionMessage($"Item with Id = {itemId} was not found in your tagged list");
                }
            }
            else
            {
                player.SendAuctionMessage($"You can't remove item with Id = {itemId}, you don't have a tagged list");
            }
        }
    }

    public static void ClearTags(this Player player)
    {
        lock (AuctionManager.TaggedItems)
        {
            if (AuctionManager.TaggedItems.TryGetValue(player.Guid.Full, out var items))
            {
                items.Clear();
            }

            player.SendAuctionMessage($"You have cleared your tagged list");
        }
    }

}
