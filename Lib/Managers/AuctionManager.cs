using ACE.Entity;
using ACE.Mods.AuctionHouse.Lib.Extensions;
using ACE.Server.Managers;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Shared;
using System.Collections.Concurrent;

namespace ACE.Mods.AuctionHouse.Lib.Managers
{
    public static class AuctionManager
    {
        private readonly static object AuctionLock = new object();

        private static double NextTickTime = 0;

        private static readonly double TickTime = 5;

        private static readonly string CONTAINER_KEYCODE = ContainerManager.LISTING_CONTAINER_KEYCODE;

        private static readonly Position CONTAINER_LOCATION = ContainerManager.LISTING_CONTAINER_LOCATION;

        public static WeakReference<Chest>? _listingContainer = null;

        public static Chest ListingContainer => GetOrCreateAuctionContainer();

        private static bool IsInitialized = false;

        public static readonly ConcurrentDictionary<uint, HashSet<uint>> TaggedItems = new();

        private static Chest CreateAuctionContainer()
        {
            return ContainerManager.CreateContainer(CONTAINER_KEYCODE, CONTAINER_LOCATION);
        }

        public static void Tick(double currentUnixTime)
        {
            if (!IsInitialized)
            {
                var lb = LandblockManager.GetLandblock(CONTAINER_LOCATION.LandblockId, false, true);
                if (lb == null || !lb.CreateWorldObjectsCompleted)
                    return;

                IsInitialized = true;
            }

            if (NextTickTime > currentUnixTime)
            {
                return;
            }

            NextTickTime = currentUnixTime + TickTime;

            try
            {
                lock (AuctionLock)
                {
                    if (!ListingContainer.InventoryLoaded)
                        return;

                    var activeListings = ListingContainer.Inventory.Values.Where(item => item.GetProperty(FakeString.ListingStatus) == "active").ToList();

                    // Process active auctions
                    foreach (var activeAuction in activeListings)
                    {
                        var endTime = activeAuction.GetProperty(FakeFloat.ListingEndTimestamp);

                        //if (endTime.HasValue && endTime.Value < currentUnixTime)  
                    }

                    Console.WriteLine($"[AuctionManager] Tick, active auction count: {activeListings.Count}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AuctionManager] Tick, Error occurred: {ex}");
            }
        }

        private static Chest GetOrCreateAuctionContainer()
        {
            if (_listingContainer == null || !_listingContainer.TryGetTarget(out var chest))
            {
                chest = CreateAuctionContainer();
                _listingContainer = new WeakReference<Chest>(chest);
            }
            return chest;
        }

        public static bool TryAddToListingContainer(WorldObject item)
        {
            lock(AuctionLock)
            {
                return ListingContainer.TryAddToInventory(item);
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

        [CommandHandler("ah-list", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 3, "Create an auction listing using tagged items.", "Usage /ah-list <CurrencyType WCID> <StartPrice> <DurationInHours>")]
        public static void HandleAuctionList(Session session, params string[] parameters)
        {
            if (parameters.Length == 3 &&
                uint.TryParse(parameters[0], out var currencyType) &&
                uint.TryParse(parameters[1], out var startPrice) &&
                ushort.TryParse(parameters[2], out var hoursDuration))
            {

                if (AuctionManager.TaggedItems.TryGetValue(session.Player.Guid.Full, out var items) && items != null && items.Count > 0)
                {
                    session.Player.PlaceAuctionListing(items.ToList(), currencyType, startPrice, hoursDuration);
                }
                else 
                    session.Network.EnqueueSend(new GameMessageSystemChat("You don't have any items tagged for listing, plsease use /ah-tag for more info", ChatMessageType.System));

            }
        }

        [CommandHandler("ah-tag", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 1, "Tag items in your inventory that will be batched into an auction listing", "Usage: /ah-tag <inspect|list|add|remove> <addId|removeId>")]
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
                    HandleAddTag(session, addId);
                    break;

                case "remove":
                    if (!uint.TryParse(targetId, out uint removeId))
                    {
                        session.Network.EnqueueSend(new GameMessageSystemChat("Usage: /ah-tag remove <removeId>", ChatMessageType.System));
                        return;
                    }
                    HandleRemoveTag(session, removeId);
                    break;

                case "clear":
                    HandleClearTag(session);
                    break;

                case "list":
                    HandleListTags(session);
                    break;

                default:
                    session.Network.EnqueueSend(new GameMessageSystemChat("Invalid command. Usage: /ah-tag <inspect|add|remove> <addId|removeId>", ChatMessageType.System));
                    break;
            }
        }

        private static void HandleListTags(Session session)
        {
            session.Player.ListTags();
        }

        private static void HandleClearTag(Session session)
        {
            session.Player.ClearTags();
        }

        private static void HandleRemoveTag(Session session, uint targetId)
        {
            session.Player.RemoveTagItem(targetId);
        }

        private static void HandleAddTag(Session session, uint targetId)
        {
            session.Player.AddTagItem(targetId);
        }

        private static void HandleInspectTag(Session session)
        {
            var target = session.Player.CurrentAppraisalTarget;

            if (target.HasValue)
            {
                var objectId = new ObjectGuid(target.Value);

                try
                {
                    session.Player.InspectTagItem(objectId.Full);
                } catch (Exception ex)
                {
                    session.Network.EnqueueSend(new GameMessageSystemChat(ex.Message, ChatMessageType.System));
                }
            }
        }
    }
}
