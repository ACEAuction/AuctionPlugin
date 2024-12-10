using ACE.Entity;
using ACE.Mods.Legend.Lib.Common;
using ACE.Mods.Legend.Lib.Common.Errors;
using ACE.Mods.Legend.Lib.CustomContainer;
using ACE.Mods.Legend.Lib.Bank;
using ACE.Server.Managers;
using ACE.Shared;
using System.Collections.Concurrent;
using ACE.Database;
using ACE.Entity.Models;

namespace ACE.Mods.Legend.Lib.Auction;

public static class AuctionManager
{
    public readonly static object AuctionItemsLock = new object();

    public readonly static object AuctionListingsLock = new object();

    private readonly static object AuctionTickLock = new object();

    private static double NextTickTime = 0;

    private static readonly double TickTime = 5;

    public static WeakReference<Chest>? _listingsContainer = null;

    public static WeakReference<Chest>? _itemsContainer = null;
    public static Chest ListingsContainer => GetOrCreateListingsContainer();
    public static Chest ItemsContainer => GetOrCreateItemsContainer();

    public static readonly ConcurrentDictionary<uint, HashSet<uint>> TaggedItems = new();
    private static void Log(string message, ModManager.LogLevel level = ModManager.LogLevel.Info)
    {
        ModManager.Log($"[AuctionHouse] {message}", level);
    }

    private static Chest CreateListingsContainer()
    {
        var container = ContainerFactory
            .CreateContainer(Constants.AUCTION_LISTINGS_CONTAINER_KEYCODE, Constants.AUCTION_LISTINGS_CONTAINER_LOCATION);
        return container;
    }
    private static Chest CreateItemsContainer()
    {
        var container = ContainerFactory
            .CreateContainer(Constants.AUCTION_ITEMS_CONTAINER_KEYCODE, Constants.AUCTION_ITEMS_CONTAINER_LOCATION);
        return container;
    }

    public static void Tick(double currentUnixTime)
    {
        if (ServerManager.ShutdownInProgress)
            return;

        EnsureContainersAreLoaded();

        if (NextTickTime > currentUnixTime || !AreInventoriesLoaded())
            return;

        NextTickTime = currentUnixTime + TickTime;

        lock (AuctionTickLock)
        {
            try
            {
                var activeListing = GetExpiredListing(currentUnixTime);
                if (activeListing != null)
                {
                    Log($"Active listing Id = {activeListing.Guid.Full}");
                    var relatedItems = GetRelatedItems(activeListing);
                    ProcessExpiredListing(activeListing, relatedItems);
                }
            }
            catch (Exception ex)
            {
                Log($"Tick, Error occurred: {ex}", ModManager.LogLevel.Error);
            }
        }
    }

    private static void EnsureContainersAreLoaded()
    {
        var listingLb = LandblockManager.GetLandblock(Constants.AUCTION_LISTINGS_CONTAINER_LOCATION.LandblockId, false, true);
        var itemsLb = LandblockManager.GetLandblock(Constants.AUCTION_ITEMS_CONTAINER_LOCATION.LandblockId, false, true);

        if (listingLb.CreateWorldObjectsCompleted && listingLb.GetObject(ListingsContainer.Guid, false) == null)
            ListingsContainer.EnterWorld();

        if (itemsLb.CreateWorldObjectsCompleted && itemsLb.GetObject(ItemsContainer.Guid, false) == null)
            ItemsContainer.EnterWorld();
    }

    private static bool AreInventoriesLoaded()
    {
        return ListingsContainer.InventoryLoaded &&
               ItemsContainer.InventoryLoaded &&
               BankManager.BankContainer.InventoryLoaded;
    }

    private static WorldObject? GetExpiredListing(double currentUnixTime)
    {
        return ListingsContainer.Inventory.Values.FirstOrDefault(item =>
        {
            var status = item.GetListingStatus();
            var endTime = item.GetListingEndTimestamp();
            return (status == "active") && endTime < currentUnixTime;
        });
    }

    private static List<WorldObject> GetRelatedItems(WorldObject activeListing)
    {
        return ItemsContainer.Inventory.Values
            .Where(item =>
            {
                var bidOwnerId = item.GetBidOwnerId();
                var listingOwner = item.GetListingOwnerId();

                return (bidOwnerId > 0 && bidOwnerId == activeListing.GetHighestBidder()) ||
                       (listingOwner > 0 && listingOwner == activeListing.GetSellerId());
            })
            .ToList();
    }


    private static void ProcessExpiredListing(WorldObject activeListing, List<WorldObject> auctionItems)
    {
        var sellerId = activeListing.GetSellerId();
        var sellerName = activeListing.GetSellerName();
        var highestBidderId = activeListing.GetHighestBidder();

        var addedListingItems = new List<WorldObject>();
        var addedBidItems = new List<WorldObject>();

        var bidItems = auctionItems.Where(item => highestBidderId > 0 && item.GetBidOwnerId() == highestBidderId).ToList();
        var listingItems = auctionItems.Where(item => item.GetListingOwnerId() == sellerId).ToList();

        LogListingDetails(activeListing, auctionItems, sellerName, highestBidderId, bidItems.Count, listingItems.Count);

        try
        {
            ProcessItems(listingItems, item =>
            {
                var bankId = highestBidderId > 0 ? highestBidderId : sellerId;
                PrepareItemForBank(item, bankId, sellerId);
                TransferItemToBank(item, addedListingItems, "listing");
            });

            ProcessItems(bidItems, item =>
            {
                PrepareItemForBank(item, sellerId, highestBidderId, isBid: true);
                TransferItemToBank(item, addedBidItems, "bid");
            });

            FinalizeActiveListing(activeListing, "complete");
        }
        catch (AuctionFailure ex)
        {
            HandleAuctionFailure(activeListing, ex.Message);
            RestoreFailedItems(addedListingItems, sellerId, activeListing.GetListingId(), true);
            RestoreFailedItems(addedBidItems, highestBidderId, activeListing.GetListingId(), false);
            throw;
        }
    }

    private static void LogListingDetails(WorldObject listing, List<WorldObject> items, string sellerName, uint highestBidderId, int bidItemsCount, int listingItemsCount)
    {
        Log($"Processing Expired Items for {listing.Guid.Full} Count: {items.Count}", ModManager.LogLevel.Warn);
        Log($"ListingId = {listing.Guid.Full}");
        Log($"Seller = {sellerName}");
        Log($"HighestBidderId = {highestBidderId}");
        Log($"bidItems Count = {bidItemsCount}");
        Log($"listingItems Count = {listingItemsCount}");
    }

    private static void ProcessItems(IEnumerable<WorldObject> items, Action<WorldObject> processItem)
    {
        foreach (var item in items)
        {
            processItem(item);
        }
    }

    private static void PrepareItemForBank(WorldObject item, uint bankId, uint ownerId, bool isBid = false)
    {
        item.RemoveProperty(FakeIID.ListingId);
        item.RemoveProperty(isBid ? FakeIID.BidOwnerId : FakeIID.ListingOwnerId);
        item.SetProperty(FakeIID.BankId, bankId);
    }

    private static void TransferItemToBank(WorldObject item, List<WorldObject> addedItems, string itemType)
    {
        if (!ItemsContainer.TryRemoveFromInventory(item.Guid))
            throw new AuctionFailure($"Failed to remove expired auction {itemType} item with Id = {item.Guid.Full} from Auction Items Chest");

        if (!BankManager.BankContainer.TryAddToInventory(item))
            throw new AuctionFailure($"Failed to add completed auction {itemType} item with Id = {item.Guid.Full} to Bankbox");

        Log($"Removed expired {itemType} item {item.Name}", ModManager.LogLevel.Warn);
        addedItems.Add(item);
    }

    private static void FinalizeActiveListing(WorldObject listing, string status)
    {
        Log($"Completed Expired Items for {listing.Guid.Full}", ModManager.LogLevel.Warn);
        listing.SetProperty(FakeString.ListingStatus, status);
    }

    private static void HandleAuctionFailure(WorldObject listing, string errorMessage)
    {
        ModManager.Log(errorMessage, ModManager.LogLevel.Error);
        listing.SetProperty(FakeString.ListingStatus, "failed");
    }

    private static void RestoreFailedItems(IEnumerable<WorldObject> items, uint ownerId, uint listingId, bool isListing)
    {
        foreach (var item in items)
        {
            item.RemoveProperty(FakeIID.BankId);
            if (isListing)
            {
                item.SetProperty(FakeIID.ListingId, listingId);
                item.SetProperty(FakeIID.ListingOwnerId, ownerId);
            }
            else
            {
                item.SetProperty(FakeIID.BidOwnerId, ownerId);
                item.SetProperty(FakeIID.ListingId, listingId);
            }

            if (!BankManager.BankContainer.TryRemoveFromInventory(item.Guid))
                throw new AuctionFailure($"Failed to remove failed auction {(isListing ? "listing" : "bid")} item with Id = {item.Guid.Full} from Bankbox");

            if (!ItemsContainer.TryAddToInventory(item))
                throw new AuctionFailure($"Failed to add failed auction {(isListing ? "listing" : "bid")} item with Id = {item.Guid.Full} to Auction Items Chest");

            Log($"Restored failed {(isListing ? "listing" : "bid")} item {item.Name}", ModManager.LogLevel.Warn);
        }
    }

    private static Chest GetOrCreateListingsContainer()
    {
        if (_listingsContainer == null || !_listingsContainer.TryGetTarget(out var chest))
        {
            chest = CreateListingsContainer();
            _listingsContainer = new WeakReference<Chest>(chest);
        }
        return chest;
    }

    private static Chest GetOrCreateItemsContainer()
    {
        if (_itemsContainer == null || !_itemsContainer.TryGetTarget(out var chest))
        {
            chest = CreateItemsContainer();
            _itemsContainer = new WeakReference<Chest>(chest);
        }
        return chest;
    }

    internal static WorldObject? GetListingById(uint listingId)
    {
        ListingsContainer.Inventory.TryGetValue(new ObjectGuid(listingId), out var listing);
        return listing;
    }

    internal static string GetListingInfo(uint listingId)
    {
        var listing = GetListingById(listingId);

        if (listing == null)
            throw new AuctionFailure($"Failed to get detailed listing info for auction listing with Id = {listingId}");

        var currency = listing.GetCurrencyType();
        var weenie = DatabaseManager.World.GetCachedWeenie((uint)currency);

        if (weenie == null)
            throw new AuctionFailure($"Listing with Id = {listing.Guid.Full} does not have a valid currency weenie id");

        var highestBid = listing.GetHighestBid();
        var highestBidderName = listing.GetHighestBidderName();
        var seller = listing.GetSellerName();
        var startingPrice = listing.GetListingStartPrice();
        var endTime = Time.GetDateTimeFromTimestamp(listing.GetListingEndTimestamp());
        var timespan = endTime - DateTime.UtcNow;
        var remaining = Helpers.FormatTimeRemaining(timespan);

        var listingItems = GetListingItems(listingId);
        StringBuilder message = new StringBuilder();

        message.Append("^^^^^^^^^^^^^^^^^^^^^^^^^\n");
        message.Append($"Listing Id = {listingId} Currency = {currency} Seller = {seller} StartingPrice = {startingPrice} Status = {listing.GetListingStatus()} TimeRemaining = {remaining} \n");
        if (highestBidderName.Length > 0)
            message.Append($"HighestBidder = {highestBidderName} HighestBid = {highestBid}\n");
        message.Append("-------------------------\n");
        foreach (var item in listingItems)
        {
            if (item == null)
                message.Append($"------------[ITEM] Id = Unable to find item\n");
            else
                message.Append($"------------[ITEM] Id = {item.Guid.Full}, {Helpers.BuildItemInfo(item)}\n");

            message.Append("-------------------------\n");
        }
        message.Append("^^^^^^^^^^^^^^^^^^^^^^^^^\n");

        return message.ToString();
    }

    private static List<WorldObject> GetListingItems(uint listingId)
    {
        return ItemsContainer.Inventory.Values
            .Where(item => item.GetListingOwnerId() > 0 && item.GetListingId() == listingId)
            .ToList();
    }

    internal static List<WorldObject> GetActiveListings()
    {
        return ListingsContainer.Inventory.Values
            .Where(item => item.GetListingStatus() == "active")
            .ToList();
    }
}
