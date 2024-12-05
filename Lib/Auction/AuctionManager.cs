using ACE.Entity;
using ACE.Mods.Legend.Lib.Common;
using ACE.Mods.Legend.Lib.Common.Errors;
using ACE.Mods.Legend.Lib.Container;
using ACE.Mods.Legend.Lib.Bank;
using ACE.Server.Managers;
using ACE.Server.Network.GameMessages.Messages;
using System.Collections.Concurrent;
using ACE.Server.Command.Handlers;
using ACE.Database;
using ACE.Entity.Models;

namespace ACE.Mods.Legend.Lib.Auction
{
    public static class AuctionManager
    {
        private readonly static object AuctionLock = new object();

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

            var listingLb = LandblockManager.GetLandblock(Constants.AUCTION_LISTINGS_CONTAINER_LOCATION.LandblockId, false, true);
            var itemsLb = LandblockManager.GetLandblock(Constants.AUCTION_ITEMS_CONTAINER_LOCATION.LandblockId, false, true);

            if (listingLb.CreateWorldObjectsCompleted && listingLb.GetObject(ListingsContainer.Guid, false) == null)
                ListingsContainer.EnterWorld();

            if (itemsLb.CreateWorldObjectsCompleted && itemsLb.GetObject(ListingsContainer.Guid, false) == null)
                ListingsContainer.EnterWorld();

            if (NextTickTime > currentUnixTime)
                return;

            NextTickTime = currentUnixTime + TickTime;

            if (!ListingsContainer.InventoryLoaded || !ItemsContainer.InventoryLoaded || !BankManager.BankContainer.InventoryLoaded)
                return;

            lock (AuctionLock)
            {
                try
                {
                    var activeListing = ListingsContainer.Inventory.Values
                        .FirstOrDefault(item =>
                        {
                            var status = item.GetListingStatus();
                            var endTime = item.GetListingEndTimestamp();
                            return (status == "active") && endTime < currentUnixTime;
                        });

                    if (activeListing != null)
                    {
                        Log($"Active listing Id = {activeListing.Guid.Full}");
                        var items = ItemsContainer.Inventory.Values
                            .Where(item =>
                            {
                                var bidOwnerId = item.GetBidOwnerId();
                                var listingOwner = item.GetListingOwnerId();
                                if (bidOwnerId > 0 && bidOwnerId == activeListing.GetHighestBidder())
                                    return true;
                                if (listingOwner > 0 && listingOwner == activeListing.GetSellerId())
                                    return true;
                                return false;
                            })
                            .ToList();
                        ProcessExpiredListing(activeListing, items);
                    }
                }
                catch (Exception ex)
                {
                    Log($"Tick, Error occurred: {ex}", ModManager.LogLevel.Error);
                }
            }
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

            Log($"Processing Expired Items for {activeListing.Guid.Full} Count: {auctionItems.Count}", ModManager.LogLevel.Warn);
            Log($"ListingId = {activeListing.Guid.Full}");
            Log($"Seller = {sellerName}");
            Log($"HighestBidderId = {highestBidderId}");
            Log($"bidItems Count = {bidItems.Count}");
            Log($"listingItems Count = {listingItems.Count}");

            try
            {
                foreach (var item in listingItems)
                {
                    var BankId = highestBidderId > 0 ? highestBidderId : sellerId;
                    item.RemoveProperty(FakeIID.ListingId);
                    item.RemoveProperty(FakeIID.ListingOwnerId);
                    item.SetProperty(FakeIID.BankId, BankId);

                    if (!TryRemoveFromItemsContainer(item))
                        throw new AuctionFailure($"Failed to remove expired auction listing item with Id = {item.Guid.Full} from Auction Items Chest");

                    if (!BankManager.TryAddToBankContainer(item))
                        throw new AuctionFailure($"Failed to add completed auction listing item with Id = {item.Guid.Full} to Bankbox");

                    Log($"Removed expired listing item {item.Name} ", ModManager.LogLevel.Warn);
                    addedListingItems.Add(item);
                }

                foreach (var item in bidItems)
                {
                    item.SetProperty(FakeIID.BankId, sellerId);
                    item.RemoveProperty(FakeIID.BidOwnerId);
                    item.RemoveProperty(FakeIID.ListingId);

                    if (!TryRemoveFromItemsContainer(item))
                        throw new AuctionFailure($"Failed to remove expired auction bid item with Id = {item.Guid.Full} from Auction Items Chest");

                    if (!BankManager.TryAddToBankContainer(item))
                        throw new AuctionFailure($"Failed to add completed auction bid item with Id = {item.Guid.Full} to Bankbox");

                    Log($"Removed expired bid item {item.Name} ", ModManager.LogLevel.Warn);
                    addedBidItems.Add(item);
                }

                Log($"Completed Expired Items for {activeListing.Guid.Full}", ModManager.LogLevel.Warn);
                activeListing.SetProperty(FakeString.ListingStatus, "complete");
            }
            catch (AuctionFailure ex)
            {
                ModManager.Log(ex.Message, ModManager.LogLevel.Error);
                activeListing.SetProperty(FakeString.ListingStatus, "failed");

                foreach (var item in addedListingItems)
                {
                    var BankId = highestBidderId > 0 ? highestBidderId : sellerId;
                    item.SetProperty(FakeIID.ListingId, activeListing.GetListingId());
                    item.SetProperty(FakeIID.ListingOwnerId, sellerId);
                    item.RemoveProperty(FakeIID.BankId);

                    if (!BankManager.TryRemoveFromBankContainer(item))
                        throw new AuctionFailure($"Failed to remove failed auction listing item with Id = {item.Guid.Full} from Bankbox");

                    if (!TryAddToItemsContainer(item))
                        throw new AuctionFailure($"Failed to add failed auction listing item with Id = {item.Guid.Full} to Auction Items Chest");

                    Log($"Removed failed list item {item.Name} ", ModManager.LogLevel.Warn);
                }

                foreach (var item in addedBidItems)
                {
                    item.RemoveProperty(FakeIID.BankId);
                    item.SetProperty(FakeIID.BidOwnerId, highestBidderId);
                    item.SetProperty(FakeIID.ListingId, activeListing.GetListingId());

                    if (!BankManager.TryRemoveFromBankContainer(item))
                        throw new AuctionFailure($"Failed to remove failed auction bid item with Id = {item.Guid.Full} from Bankbox");

                    if (!TryAddToItemsContainer(item))
                        throw new AuctionFailure($"Failed to add failed auction bid item with Id = {item.Guid.Full} to Auction Items Chest");

                    Log($"Removed failed bid item {item.Name} ", ModManager.LogLevel.Warn);
                }
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

        public static bool TryAddToListingsContainer(WorldObject item)
        {
            lock (AuctionLock)
            {
                return ListingsContainer.TryAddToInventory(item);
            }
        }

        public static bool TryAddToItemsContainer(WorldObject item)
        {
            lock (AuctionLock)
            {
                return ItemsContainer.TryAddToInventory(item);
            }
        }
        public static bool TryRemoveFromItemsContainer(WorldObject item)
        {
            lock (AuctionLock)
            {
                return ItemsContainer.TryRemoveFromInventory(item.Guid);
            }
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

    [HarmonyPatchCategory(nameof(AuctionPatches))]
    public static class AuctionPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(WorldManager), nameof(WorldManager.UpdateGameWorld))]
        public static void PostUpdateGameWorld(ref bool __result)
        {
            AuctionManager.Tick(Time.GetUnixTime());
        }

        [CommandHandler("ah-sell", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 3, "Create an auction listing using tagged sell items.", "Usage /ah-sell <CurrencyType WCID> <StartPrice> <DurationInHours>")]
        public static void HandleAuctionSell(Session session, params string[] parameters)
        {
            if (parameters.Length == 3 &&
                uint.TryParse(parameters[0], out var currencyType) &&
                uint.TryParse(parameters[1], out var startPrice) &&
                ushort.TryParse(parameters[2], out var hoursDuration))
            {

                if (AuctionManager.TaggedItems.TryGetValue(session.Player.Guid.Full, out var items) && items != null && items.Count > 0)
                {
                    try
                    {
                        session.Player.PlaceAuctionSell(items.ToList(), currencyType, startPrice, hoursDuration);
                    }
                    catch (Exception ex)
                    {
                        ModManager.Log(ex.Message, ModManager.LogLevel.Error);
                        session.Player.SendAuctionMessage($"An unexpected error occurred");
                    }
                }
                else
                    session.Network.EnqueueSend(new GameMessageSystemChat("You don't have any items tagged for listing, plsease use /ah-tag for more info", ChatMessageType.System));

            }
        }

        [CommandHandler("ah-list", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0, "Show auction house listings.", "Usage /ah-list [optional LISTING_ID]")]
        public static void HandleAuctionList(Session session, params string[] parameters)
        {
            try
            {
                if (parameters.Length == 1 &&
                    uint.TryParse(parameters[0], out var listingId))
                {

                    var listing = AuctionManager.GetListingById(listingId);
                    if (listing == null)
                        session.Player.SendAuctionMessage($"Failed to find information for auction listing with Id = {listingId}");

                    var info = AuctionManager.GetListingInfo(listingId);

                    CommandHandlerHelper.WriteOutputInfo(session, info, ChatMessageType.Broadcast);
                } else
                {
                    var listings = AuctionManager.GetActiveListings();
                    session.Player.SendAuctionMessage("...Active Auction Listings...");
                    foreach (var listing in listings)
                    {
                        var player = session.Player;
                        var currency = DatabaseManager.World.GetCachedWeenie((uint)listing.GetCurrencyType());
                        if (currency == null)
                            throw new AuctionFailure($"Listing with Id = {listing.Guid.Full} does not have a valid currency weenie id");

                        var endTime = Time.GetDateTimeFromTimestamp(listing.GetListingEndTimestamp());
                        var timespan = endTime - DateTime.UtcNow;
                        var remaining = Helpers.FormatTimeRemaining(timespan);

                        player.SendAuctionMessage($"[LISTING] Id = {listing.Guid.Full} Seller = {listing.GetSellerName()} Currency = {currency.GetName()} TimeRemaining = {remaining} ");
                    }
                }
            }
            catch (AuctionFailure ex)
            {
                session.Player.SendAuctionMessage(ex.Message);
            }
            catch (Exception ex)
            {
                ModManager.Log(ex.Message, ModManager.LogLevel.Error);
                session.Player.SendAuctionMessage($"An unexpected error occurred");
            }
        }

        [CommandHandler("ah-bid", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 2, "Bid on an auction listing.", "Usage /ah-bid <LISTING_ID> <BID_AMOUNT>")]
        public static void HandleAuctionBid(Session session, params string[] parameters)
        {
            if (parameters.Length == 2 &&
                uint.TryParse(parameters[0], out var listingId) &&
                uint.TryParse(parameters[1], out var bidAmount))
            {
                try
                {
                    session.Player.PlaceAuctionBid(listingId, bidAmount);
                }
                catch (AuctionFailure ex)
                {
                    session.Player.SendAuctionMessage(ex.Message);
                }
                catch (Exception ex)
                {
                    ModManager.Log(ex.Message, ModManager.LogLevel.Error);
                    session.Player.SendAuctionMessage($"An unexpected error occurred");
                }
            }
        }

        [CommandHandler("ah-tag", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 1, "Tag items in your inventory that will be included in an auction listing", "Usage: /ah-tag <inspect|list|add|remove> <addId|removeId>")]
        public static void HandleTag(Session session, params string[] parameters)
        {

            // Ensure we have the correct number of arguments
            if (parameters.Length < 1)
            {
                session.Network.EnqueueSend(new GameMessageSystemChat("Usage: /ah-tag <inspect|list|add|remove> <addId|removeId>", ChatMessageType.System));
                return;
            }

            string command = parameters[0].ToLower(); // First argument
            var targetId = parameters.Length > 1 ? parameters[1] : null; // Second argument if available

            switch (command)
            {
                case "inspect":
                    HandleInspectTag(session);
                    break;

                case "add":
                    if (!uint.TryParse(targetId, out uint addId))
                    {

                        session.Network.EnqueueSend(new GameMessageSystemChat("Usage: /ah-tag add <addId>", ChatMessageType.System));
                        return;
                    }
                    session.Player.AddTagItem(addId);
                    break;

                case "remove":
                    if (!uint.TryParse(targetId, out uint removeId))
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat("Usage: /ah-tag remove <removeId>", ChatMessageType.System));
                        return;
                    }
                    session.Player.RemoveTagItem(removeId);
                    break;

                case "clear":
                    session.Player.ClearTags();
                    break;

                case "list":
                    session.Player.ListTags();
                    break;

                default:
                    session.Network.EnqueueSend(new GameMessageSystemChat("Invalid command. Usage: /ah-tag <inspect|add|remove> <addId|removeId>", ChatMessageType.System));
                    break;
            }
        }

        private static void HandleInspectTag(Session session)
        {
            var target = session.Player.RequestedAppraisalTarget;

            if (target.HasValue)
            {
                var objectId = new ObjectGuid(target.Value);

                try
                {
                    var isTagging = session.Player.GetAuctionTagging();
                    session.Player.SetProperty(FakeBool.IsAuctionTagging, !isTagging);
                    session.Player.SendAuctionMessage($"Toggling auction tag inspect = {!isTagging}");
                }
                catch (AuctionFailure ex)
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat(ex.Message, ChatMessageType.System));
                }
                catch (Exception ex)
                {
                    ModManager.Log(ex.Message, ModManager.LogLevel.Error);
                    session.Player.SendAuctionMessage($"An unexpected error occurred");
                }
            }
        }
    }
}
