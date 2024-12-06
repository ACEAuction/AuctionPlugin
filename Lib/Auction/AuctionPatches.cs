using ACE.Database;
using ACE.Entity;
using ACE.Entity.Models;
using ACE.Mods.Legend.Lib.Common;
using ACE.Mods.Legend.Lib.Common.Errors;
using ACE.Server.Command.Handlers;
using ACE.Server.Managers;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Shared;

namespace ACE.Mods.Legend.Lib.Auction
{
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
                }
                else
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
