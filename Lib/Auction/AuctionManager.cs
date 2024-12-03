using ACE.Adapter.GDLE.Models;
using ACE.Entity;
using ACE.Mods.Legend.Lib.Common;
using ACE.Mods.Legend.Lib.Common.Errors;
using ACE.Mods.Legend.Lib.Container;
using ACE.Mods.Legend.Lib.Mail;
using ACE.Server.Managers;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Shared;
using System.Collections.Concurrent;

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

        private static Chest CreateListingsContainer()
        {
            return ContainerFactory
                .CreateContainer(Constants.AUCTION_LISTINGS_CONTAINER_KEYCODE, Constants.AUCTION_LISTINGS_CONTAINER_LOCATION);
        }
        private static Chest CreateItemsContainer()
        {
            return ContainerFactory
                .CreateContainer(Constants.AUCTION_ITEMS_CONTAINER_KEYCODE, Constants.AUCTION_ITEMS_CONTAINER_LOCATION);
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

            if (!ListingsContainer.InventoryLoaded || !ItemsContainer.InventoryLoaded || !MailManager.MailContainer.InventoryLoaded)
                return;

            lock (AuctionLock)
            {
                try
                {
                    var activeListing = ListingsContainer.Inventory.Values
                        .FirstOrDefault(item =>
                        {
                            var status = item.GetProperty(FakeString.ListingStatus);
                            var endTime = item.GetProperty(FakeFloat.ListingEndTimestamp);
                            return status != null && status == "active" && endTime.HasValue && endTime.Value < currentUnixTime;
                        });

                    if (activeListing != null)
                    {
                        ModManager.Log($"Active listing Id = {activeListing.Guid.Full}");
                        var items = ItemsContainer.Inventory.Values
                            .Where(item =>
                            {
                                var listingId = item.GetProperty(FakeIID.ListingId);
                                return listingId.HasValue && listingId.Value == activeListing.Guid.Full;
                            })
                            .ToList();
                        ProcessExpiredListing(activeListing, items);
                    }
                }
                catch (Exception ex)
                {
                    ModManager.Log($"[AuctionManager] Tick, Error occurred: {ex}", ModManager.LogLevel.Error);
                }
            }
        }

        private static void ProcessExpiredListing(WorldObject activeListing, List<WorldObject> auctionItems)
        {
            ModManager.Log($"Processing Expired Items for {activeListing.Guid.Full} Count: {auctionItems.Count}", ModManager.LogLevel.Warn);

            var sellerId = activeListing.GetProperty(FakeIID.SellerId);
            var highestBidderId = activeListing.GetProperty(FakeIID.HighestBidderId);

            if (!sellerId.HasValue)
                throw new AuctionFailure($"Failed to process expired listing, a SellerId is not assigned to the listing with Id {activeListing.Guid.Full}");

            try
            {
                foreach(var item in auctionItems)
                {
                    if (!TryRemoveFromItemsContainer(item))
                        throw new AuctionFailure("Failed to removed expired items");

                    if (!MailManager.TryAddToMailContainer(item))
                        throw new AuctionFailure($"Failed to add completed auction item with Id = {item.Guid.Full} to Mailbox");
                    else
                    {
                        item.RemoveProperty(FakeIID.ListingId);
                        var mailTo = highestBidderId.HasValue ? highestBidderId.Value : sellerId.Value;
                        item.SetProperty(FakeIID.MailTo, mailTo);
                    }
                }

                ModManager.Log($"Completed Expired Items for {activeListing.Guid.Full}", ModManager.LogLevel.Warn);
                activeListing.SetProperty(FakeString.ListingStatus, "complete");
            }
            catch (AuctionFailure ex)
            {
                ModManager.Log(ex.Message, ModManager.LogLevel.Error);

                activeListing.SetProperty(FakeString.ListingStatus, "failed");

                foreach(var item in auctionItems)
                {
                    MailManager.TryRemoveFromMailContainer(item);
                    item.SetProperty(FakeIID.ListingId, activeListing.Guid.Full);
                    item.RemoveProperty(FakeIID.MailTo);
                    if (ItemsContainer.Inventory.ContainsKey(item.Guid))
                        TryAddToItemsContainer(item);
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

        [CommandHandler("ah-sell", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 3, "Create an auction listing using tagged sell items.", "Usage /ah-list <CurrencyType WCID> <StartPrice> <DurationInHours>")]
        public static void HandleAuctionList(Session session, params string[] parameters)
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
                    session.Player.InspectTagItem(objectId.Full);
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
