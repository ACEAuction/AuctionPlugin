using ACE.Database;
using ACE.Entity.Models;
using ACE.Mods.Legend.Lib.Common;
using ACE.Mods.Legend.Lib.Common.Errors;
using ACE.Mods.Legend.Lib.Bank;
using ACE.Server.Command.Handlers;
using ACE.Server.Factories;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Shared;
using static ACE.Server.WorldObjects.Player;
using System.Globalization;


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
    public static double GetLastFailListingTimestamp(this WorldObject item) =>
        item.GetProperty(FakeFloat.LastFailedListingTimestamp) ?? 0;
    public static double GetBidTimestamp(this WorldObject item) =>
        item.GetProperty(FakeFloat.BidTimestamp) ?? 0;
    public static uint GetBidOwnerId(this WorldObject item) =>
        item.GetProperty(FakeIID.BidOwnerId) ?? 0;
    public static bool GetAuctionTagging(this Player player) =>
        player.GetProperty(FakeBool.IsAuctionTagging) ?? false;
    public static bool IsAuctionItemsContainer(this Container item) => item.Name == Constants.AUCTION_ITEMS_CONTAINER_KEYCODE;

    public static void SendAuctionMessage(this Player player, string message, ChatMessageType messageType = ChatMessageType.System)
    {
        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"{AuctionPrefix} {message}", messageType));
    }

    public static void ValidateAuctionTag(this Player player, uint tagId, out WorldObject taggedItem)
    {
        if (AuctionManager.TaggedItems.TryGetValue(player.Guid.Full, out var items) && items != null && items.Contains(tagId))
            throw new AuctionFailure($"The tagged item with Id = {tagId} is already tagged", FailureCode.Auction.Unknown);

        var item = player.FindObject(tagId, SearchLocations.MyInventory | SearchLocations.MyEquippedItems, out var itemFoundInContainer, out var itemRootOwner, out var itemWasEquipped);

        if (item == null)
            throw new AuctionFailure($"The tagged item with Id = {tagId} was not found on your person", FailureCode.Auction.Unknown);

        if (item.IsAttunedOrContainsAttuned)
            throw new AuctionFailure($"{item.NameWithMaterial} cannot be tagged, it is attuned or bonded", FailureCode.Auction.Unknown);

        if (player.IsTrading && item.IsBeingTradedOrContainsItemBeingTraded(player.ItemsInTradeWindow))
            throw new AuctionFailure($"The item {item.NameWithMaterial} cannot be tagged, the item is currently being traded", FailureCode.Auction.Unknown);

        taggedItem = item;
    }

    private static void ValidateAuctionBid(this Player player, WorldObject listing, uint bidAmount)
    {
        if(listing.GetListingStatus() != "active")
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

        if (numOfItems < listingHighBid || numOfItems < listingStartPrice )
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
    }

    public class AuctionSellState
    {
        public List<WorldObject> RemovedItems { get; }
        public Book ListingParchment { get; }
        public string CurrencyName { get; }

        public uint CurrencyWcid { get; }
        public int StackSize { get; }
        public uint NumberOfStacks { get; }
        public TimeSpan RemainingTime { get; }

        public AuctionSellState(List<WorldObject> removedItems, Book listingParchment, string currencyName, uint currencyWcid, int stackSize, uint numOfStacks, TimeSpan remainingTime)
        {
            RemovedItems = removedItems ?? throw new ArgumentNullException(nameof(removedItems));
            ListingParchment = listingParchment ?? throw new ArgumentNullException(nameof(listingParchment));
            CurrencyName = currencyName ?? throw new ArgumentNullException(nameof(currencyName));
            CurrencyWcid = currencyWcid;
            StackSize = stackSize;
            NumberOfStacks = numOfStacks;
            RemainingTime = remainingTime;
        }
    }

    public static void PlaceAuctionSell(this Player player, uint itemId, int stackSize, uint numOfStacks, uint currencyType, uint buyoutPrice, ushort hoursDuration)
    {
        var startTime = DateTime.UtcNow;
        var endTime = Settings.IsDev ? startTime.AddSeconds(hoursDuration) : startTime.AddHours(hoursDuration);
        Book listingParchment = (Book)WorldObjectFactory.CreateNewWorldObject(365);
        string currencyName = GetCurrencyName(currencyType);

        var state = new AuctionSellState(
            new List<WorldObject>(),
            listingParchment,
            currencyName,
            currencyType,
            stackSize,
            numOfStacks,
            endTime - startTime
        );

        try
        {
            if (hoursDuration > MaxAuctionHours)
                throw new AuctionFailure($"Failed validation for auction sell, an auction end time can not exceed 168 hours (a week)", FailureCode.Auction.DurationLimitReached);
            InitializeListingParchment(player, currencyType, buyoutPrice, startTime, endTime, state);
            ProcessSell(player, itemId, (int)stackSize, numOfStacks, state);
        }
        catch (Exception ex)
        {
            HandleAuctionSellFailure(player, state, ex.Message);
        }
    }

    private static string GetCurrencyName(uint currencyType)
    {
        var weenie = DatabaseManager.World.GetCachedWeenie(currencyType);
        if (weenie == null)
            throw new AuctionFailure($"Failed to get currency name from weenie with Id = {currencyType}", FailureCode.Auction.InvalidCurrencyFailure);
        return weenie.GetName();
    }

    private static void InitializeListingParchment(Player player, uint currencyType, uint buyoutPrice, DateTime startTime, DateTime endTime, AuctionSellState state)
    {
        var parchment = state.ListingParchment;

        parchment.Name = "Auction Listing Invoice";
        parchment.SetProperty(FakeIID.SellerId, player.Account.AccountId);
        parchment.SetProperty(FakeString.SellerName, player.Name);
        parchment.SetProperty(FakeInt.ListingCurrencyType, (int)currencyType);
        parchment.SetProperty(FakeInt.ListingStartPrice, (int)buyoutPrice);
        parchment.SetProperty(FakeFloat.ListingStartTimestamp, (double)Time.GetUnixTime(startTime));
        parchment.SetProperty(FakeFloat.ListingEndTimestamp, (double)Time.GetUnixTime(endTime));
        parchment.SetProperty(FakeString.ListingStatus, "active");

        ModManager.Log($"[AuctionHouse] Initialized listing parchment with Id = {parchment.Guid.Full}", ModManager.LogLevel.Warn);
    }

    private static void ProcessSell(Player player, uint itemId, int stackSize, uint numOfStacks, AuctionSellState state)
    {
        // Retrieve the item to be sold
        var sellItem = player.GetInventoryItem(itemId)
            ?? throw new AuctionFailure("The specified item could not be found in the player's inventory.", FailureCode.Auction.ItemNotFoundFailure);

        // Validate conditions
        if (sellItem.ItemWorkmanship != null && numOfStacks > 1)
            throw new AuctionFailure("A loot-generated item cannot be traded if the number of stacks is greater than 1.", FailureCode.Auction.UniqueItemFailure);

        // Determine the items to sell
        var sellItems = (sellItem.Workmanship != null)
            ? new List<WorldObject> { sellItem }
            : player.GetInventoryItemsOfWCID(sellItem.WeenieClassId).ToList();

        // Calculate the total number of items required
        var totalItemsRequired = (int)(stackSize * numOfStacks);
        var remainingAmount = totalItemsRequired;

        // Create and transfer stacks
        for (uint stackIndex = 0; stackIndex < numOfStacks; stackIndex++)
        {
            if (remainingAmount <= 0) break;

            int itemsInCurrentStack = 0;
            var currentStack = new List<WorldObject>();

            foreach (var item in sellItems) // Iterate through available items
            {
                if (remainingAmount <= 0 || itemsInCurrentStack >= stackSize) break;

                // Determine the transfer amount for the current item
                int transferAmount = Math.Min(item.StackSize ?? 1, Math.Min(stackSize - itemsInCurrentStack, remainingAmount));
                player.RemoveItemForTransfer(item.Guid.Full, out var removedItem, transferAmount);

                // Configure sell item properties
                ConfigureSellItem(removedItem, player.Account.AccountId, state.ListingParchment.Guid.Full);

                if (!AuctionManager.TryAddToInventory(removedItem))
                {
                    throw new AuctionFailure(
                        $"Failed to add sell item to Auction Items Chest. ID: {removedItem.Guid.Full}, Name: {removedItem.NameWithMaterial}",
                        FailureCode.Auction.TransferItemToFailure);
                }

                itemsInCurrentStack += transferAmount;
                remainingAmount -= transferAmount;
                currentStack.Add(removedItem);

                // Remove empty items from the source list
                if (item.StackSize == 0) sellItems.Remove(item);
            }

            // Ensure the current stack is valid
            if (currentStack.Count > 0)
                state.RemovedItems.AddRange(currentStack);

            // If the current stack is incomplete, throw an error
            if (itemsInCurrentStack < stackSize && stackIndex < numOfStacks - 1)
            {
                throw new AuctionFailure(
                    $"Unable to create a full stack of size {stackSize}. Stack #{stackIndex + 1} is incomplete.",
                    FailureCode.Auction.IncompleteStack);
            }
        }

        // Ensure all required items have been processed
        if (remainingAmount > 0)
        {
            throw new AuctionFailure(
                $"Insufficient sell items to meet the required quantity for listing. Listing ID: {state.ListingParchment.Guid.Full}",
                FailureCode.Auction.InsufficientAmountFailure);
        }

        // Finalize the auction listing
        AddListingToAuctionContainer(state);
        FinalizeAuctionListing(player, state);
    }


    private static void ConfigureSellItem(WorldObject removedItem, uint accountId, uint listingId)
    {
        removedItem.SetProperty(FakeIID.ListingOwnerId, accountId);
        removedItem.SetProperty(FakeIID.ListingId, listingId);
    }

    private static void TransferSellItemsToAuctionContainer(AuctionSellState state)
    {
        foreach (var item in state.RemovedItems)
        {
            if (item == null || !AuctionManager.TryAddToInventory(item))
            {
                throw new AuctionFailure($"Failed to transfer listing item {item?.NameWithMaterial} to the auction items container.", FailureCode.Auction.TransferItemToFailure);
            }
        }
    }

    private static void AddListingToAuctionContainer(AuctionSellState state)
    {
        if (!AuctionManager.ListingsContainer.TryAddToInventory(state.ListingParchment))
        {
            throw new AuctionFailure($"Failed to transfer listing parchment {state.ListingParchment.NameWithMaterial} to the auction items container.", FailureCode.Auction.TransferItemToFailure);
        }
    }
    private static void FinalizeAuctionListing(Player player, AuctionSellState state)
    {
        var remaining = Helpers.FormatTimeRemaining(state.RemainingTime);
        var message = $"Successfully created an auction listing with Id = {state.ListingParchment.Guid.Full}, Seller = {player.Name}, Currency = {state.CurrencyName}, StackSize = {state.StackSize}, NumberOfStacks = {state.NumberOfStacks}, TimeRemaining = {remaining}";

        player.SendAuctionMessage(message, ChatMessageType.Broadcast);

        foreach (var item in state.RemovedItems)
        {
            player.SendAuctionMessage($"--> Id = {item.Guid.Full}, {Helpers.BuildItemInfo(item)}, Count = {item.StackSize ?? 1}");
        }
    }

    private static void HandleAuctionSellFailure(Player player, AuctionSellState state, string errorMessage)
    {
        ModManager.Log(errorMessage, ModManager.LogLevel.Error);
        player.SendAuctionMessage("Placing auction listing failed");
        player.SendAuctionMessage(errorMessage);

        foreach (var removedItem in state.RemovedItems)
        {
            removedItem.RemoveProperty(FakeIID.ListingId);
            removedItem.RemoveProperty(FakeIID.ListingOwnerId);

            if (!player.HasItemOnPerson(removedItem.Guid.Full, out _))
            {
                player.SendAuctionMessage($"Attempting to return listing item {removedItem.NameWithMaterial}");

                if (!AuctionManager.TryRemoveFromInventory(removedItem))
                    break;

                if (!player.TryCreateInInventoryWithNetworking(removedItem))
                {
                    player.SendAuctionMessage($"Failed to return listing item {removedItem.NameWithMaterial}, attempting to send it to the bank.");
                    BankManager.TryAddToInventory(removedItem, player.Account.AccountId);
                }

                player.EnqueueBroadcast(new GameMessageSound(player.Guid, Sound.ReceiveItem));
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

    private static void RemoveItemForTransfer(this Player player, uint itemToTransfer, out WorldObject itemToRemove, int? amount = null)
    {
        if (player.IsBusy || player.Teleporting || player.suicideInProgress)
            throw new AuctionFailure($"The item cannot be transferred, you are too busy", FailureCode.Auction.TransferBusyFailure);

        var item = player.FindObject(itemToTransfer, SearchLocations.MyInventory | SearchLocations.MyEquippedItems, out var itemFoundInContainer, out var itemRootOwner, out var itemWasEquipped);

        if (item == null)
            throw new AuctionFailure($"The item cannot be transferred, item with Id = {itemToTransfer} was not found on your person", FailureCode.Auction.TransferItemNotFoundFailure);

        if (item.IsAttunedOrContainsAttuned)
            throw new AuctionFailure($"The item cannot be transferred {item.NameWithMaterial} is attuned", FailureCode.Auction.TransferItemAttunedFailure);

        if (player.IsTrading && item.IsBeingTradedOrContainsItemBeingTraded(player.ItemsInTradeWindow))
            throw new AuctionFailure($"The item cannot be transferred {item.NameWithMaterial}, the item is currently being traded", FailureCode.Auction.TransferItemsInTradeWindowFailure);

        var removeAmount = amount.HasValue ? amount.Value : item.StackSize ?? 1;

        if (!player.RemoveItemForGive(item, itemFoundInContainer, itemWasEquipped, itemRootOwner, removeAmount, out WorldObject itemToGive))
            throw new AuctionFailure($"The item cannot be transferred {item.NameWithMaterial}, failed to remove item from location", FailureCode.Auction.TransferRemoveItemForGiveFailure);

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
    public static void TagAllInventory(this Player player)
    {
        foreach(var item in player.Inventory.Values.ToList())
        {
            try
            {
                player.AddTagItem(item.Guid.Full);
            } catch { }
        }
    }

}
