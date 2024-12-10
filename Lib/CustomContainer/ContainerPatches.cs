using ACE.Entity;
using ACE.Entity.Enum.Properties;
using ACE.Mods.Legend.Lib.Auction;
using ACE.Mods.Legend.Lib.Bank;
using ACE.Mods.Legend.Lib.Common;
using ACE.Server.Entity.Actions;
using ACE.Server.Managers;
using ACE.Server.Network.GameMessages;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Shared;
using static ACE.Server.WorldObjects.Player;

namespace ACE.Mods.Legend.Lib.CustomContainer;

[HarmonyPatchCategory(nameof(ContainerPatches))]
public static class ContainerPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Container), nameof(Container.ClearUnmanagedInventory), new Type[] { typeof(bool) })]
    public static bool PreClearUnmanagedInventory(bool forceSave, ref Container __instance, ref bool __result)
    {
        var localInstance = __instance;

        /// Custom containers should never clear their inventory
        if
        (
            localInstance is Storage ||
            localInstance.WeenieClassId == (uint)WeenieClassName.W_STORAGE_CLASS ||
            localInstance.Name == Constants.AUCTION_LISTINGS_CONTAINER_KEYCODE ||
            localInstance.Name == Constants.AUCTION_ITEMS_CONTAINER_KEYCODE ||
            localInstance.Name == Constants.BANK_CONTAINER_KEYCODE
        )
        {
            __result = false; // Do not clear storage, ever.
            return false;
        }

        var success = true;
        var itemGuids = localInstance.Inventory.Where(i => i.Value.GeneratorId == null).Select(i => i.Key).ToList();
        foreach (var itemGuid in itemGuids)
        {
            if (!localInstance.TryRemoveFromInventory(itemGuid, out var item, forceSave))
                success = false;

            if (success)
                item.Destroy();
        }
        if (forceSave)
            localInstance.SaveBiotaToDatabase();

        __result = success;
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Container), nameof(Container.Open), new Type[] { typeof(Player) })]
    public static bool PreOpen(Player player, ref Container __instance)
    {
        var localInstance = __instance;

        if (!localInstance.IsCustomContainer())
            return true;

        player.LastOpenedContainerId = localInstance.Guid;

        localInstance.Viewer = player.Guid.Full;
        var sendActionChain = new ActionChain();
        sendActionChain.AddDelaySeconds(0.5);
        sendActionChain.AddAction(player, () =>
        {
            //SendUpdateForMyInventory(player, localInstance);
        });
        sendActionChain.EnqueueChain();

        if (!localInstance.IsOpen)
            localInstance.DoOnOpenMotionChanges();

        var customContainerOpen = player.GetProperty(FakeBool.IsCustomContainerOpen);

        if (customContainerOpen.HasValue && customContainerOpen.Value && localInstance.IsOpen)
        {
            player.Session.Network.EnqueueSend(new GameEventCloseGroundContainer(player.Session, localInstance));

            if (player.LastOpenedContainerId == localInstance.Guid)
                player.LastOpenedContainerId = ObjectGuid.Invalid;
            player.SetProperty(FakeBool.IsCustomContainerOpen, false);
        }
        else
        {
            player.SetProperty(FakeBool.IsCustomContainerOpen, true);
        }

        localInstance.IsOpen = true;

        localInstance.SendInventory(player);

        if (!(localInstance is Chest) && !localInstance.ResetMessagePending && localInstance.ResetInterval.HasValue)
        {
            var actionChain = new ActionChain();
            if (localInstance.ResetInterval.Value < 15)
                actionChain.AddDelaySeconds(15);
            else
                actionChain.AddDelaySeconds(localInstance.ResetInterval.Value);
            actionChain.AddAction(localInstance, localInstance.Reset);
            actionChain.AddAction(localInstance, () =>
            {
                //localInstance.Close(player);
            });
            actionChain.EnqueueChain();
            localInstance.ResetMessagePending = true;
        }

        return false;
    }

    public class PatchedGameEventViewContents : GameEventMessage
    {
        public PatchedGameEventViewContents(Session session, Container container, List<WorldObject> inventory)
            : base(GameEventType.ViewContents, GameMessageGroup.UIQueue, session)
        {
            base.Writer.Write(container.Guid.Full);
            base.Writer.Write((uint)inventory.Count);
            foreach (WorldObject item in inventory)
            {
                base.Writer.Write(item.Guid.Full);
                if (item.WeenieType == WeenieType.Container)
                {
                    base.Writer.Write(1u);
                }
                else if (item.RequiresPackSlot)
                {
                    base.Writer.Write(2u);
                }
                else
                {
                    base.Writer.Write(0u);
                }
            }
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Container), "SendInventory", new Type[] { typeof(Player) })]
    public static bool PreSendInventory(Player player, ref Container __instance)
    {
        var localInstance = __instance;
        if (!localInstance.IsCustomContainer())
            return true;

        // send createobject for all objects in this container's inventory to player
        var itemsToSend = new List<GameMessage>();
        var inventory = new List<WorldObject>();

        if (localInstance.Name == Constants.BANK_CONTAINER_KEYCODE)
            inventory = localInstance.Inventory.Values
                .Where(item =>
                {
                    var bankId = item.GetBankId();
                    return bankId > 0 && bankId == player.Account.AccountId;
                }).OrderByDescending(item => item.ItemType).ToList();

        if (localInstance.Name == Constants.AUCTION_ITEMS_CONTAINER_KEYCODE)
            inventory = localInstance.Inventory.Values
                .Where(item =>
                {
                    var bidOwnerId = item.GetBidOwnerId();
                    var listingOwner = item.GetListingOwnerId();

                    if (bidOwnerId > 0 && bidOwnerId == player.Guid.Full)
                        return true;
                    if (listingOwner > 0 && listingOwner == player.Guid.Full)
                        return true;
                    return false;
                }).OrderByDescending(item => item.Value).ToList();

        foreach (var item in inventory)
        {
            // FIXME: only send messages for unknown objects
            itemsToSend.Add(new GameMessageCreateObject(item));

            if (item is Container container)
            {
                foreach (var containerItem in container.Inventory.Values)
                {
                    itemsToSend.Add(new GameMessageCreateObject(containerItem));
                }
            }
        }

        player.Session.Network.EnqueueSend(new PatchedGameEventViewContents(player.Session, localInstance, inventory));

        // FIX SUB CONTAINERS WITH

        // send sub-containersC
        //foreach (var container in inventory.Where(i => i is Container))
        //player.Session.Network.EnqueueSend(new PatchedGameEventViewContents(player.Session, (Container)container), new List<WorldObject>());

        player.Session.Network.EnqueueSend(itemsToSend);

        return false;
    }


    [HarmonyPrefix]
    [HarmonyPatch(typeof(Chest), nameof(Chest.CheckUseRequirements), new Type[] { typeof(WorldObject) })]
    public static bool PreCheckUseRequirements(WorldObject activator, ref Chest __instance, ref ActivationResult __result)
    {
        var localInstance = __instance;
        if (!localInstance.IsCustomContainer())
            return true;

        if (!(activator is Player player))
        {
            __result = new ActivationResult(false);
            return false;
        }

        if (localInstance.IsOpen)
        {
            if (localInstance.Viewer == player.Guid.Full)
            {
                // current player has this chest open, close it
                //localInstance.Close(player);
            }
            else
            {
                // another player has this chest open -- ensure they are within range
                var currentViewer = localInstance.CurrentLandblock.GetObject(localInstance.Viewer) as Player;

                if (currentViewer == null)
                {

                }
                //localInstance.Close(null);    // current viewer not found, close it
            }
        }

        __result = new ActivationResult(true);
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Container), nameof(Container.FinishClose), new Type[] { typeof(Player) })]
    public static bool PreFinishClose(Player player, ref Container __instance)
    {
        var localInstance = __instance;
        if (!localInstance.IsCustomContainer())
            return true;

        localInstance.IsOpen = false;
        localInstance.Viewer = 0;

        if (player != null)
        {
            player.Session.Network.EnqueueSend(new GameEventCloseGroundContainer(player.Session, localInstance));

            if (player.LastOpenedContainerId == localInstance.Guid)
                player.LastOpenedContainerId = ObjectGuid.Invalid;
        }

        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Player), nameof(Player.HandleActionIdentifyObject), new Type[] { typeof(uint) })]
    public static bool PreHandleActionIdentifyObject(uint objectGuid, ref Player __instance)
    {
        var localInstance = __instance;

        if (objectGuid == 0)
        {
            // Deselect the formerly selected Target
            //selectedTarget = ObjectGuid.Invalid;
            localInstance.RequestedAppraisalTarget = null;
            localInstance.CurrentAppraisalTarget = null;
            return false;
        }

        var wo = localInstance.FindObject(objectGuid, Player.SearchLocations.Everywhere, out _, out _, out _);

        var lastOpenedContainer = (Container)localInstance.FindObject(localInstance.LastOpenedContainerId, Player.SearchLocations.Everywhere, out _, out _, out _);

        if (wo == null)
            if (lastOpenedContainer != null && lastOpenedContainer.IsCustomContainer() && lastOpenedContainer.Inventory.TryGetValue(new ObjectGuid(objectGuid), out var item))
                wo = item;

        if (wo == null)

        {
            //log.DebugFormat("{0}.HandleActionIdentifyObject({1:X8}): couldn't find object", Name, objectGuid);
            localInstance.Session.Network.EnqueueSend(new GameEventIdentifyObjectResponse(localInstance.Session, objectGuid));
            return false;
        }

        var currentTime = Time.GetUnixTime();


        // compare with previously requested appraisal target
        if (objectGuid == localInstance.RequestedAppraisalTarget)
        {
            if (objectGuid == localInstance.CurrentAppraisalTarget)
            {
                // continued success, rng roll no longer needed
                localInstance.Session.Network.EnqueueSend(new GameEventIdentifyObjectResponse(localInstance.Session, wo, true));
                localInstance.OnAppraisal(wo, true);
                return false;
            }

            if (currentTime < localInstance.AppraisalRequestedTimestamp + 5.0f)
            {
                // rate limit for unsuccessful appraisal spam
                localInstance.Session.Network.EnqueueSend(new GameEventIdentifyObjectResponse(localInstance.Session, wo, false));
                localInstance.OnAppraisal(wo, false);
                return false;
            }
        }

        localInstance.RequestedAppraisalTarget = objectGuid;
        localInstance.AppraisalRequestedTimestamp = currentTime;

        var isAuctionTagging = localInstance.GetAuctionTagging();
        if (isAuctionTagging)
            localInstance.InspectTagItem(objectGuid);

        localInstance.Examine(wo);

        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Player), nameof(Player.HandleActionStackableMerge), new Type[] { typeof(uint), typeof(uint), typeof(int) })]
    public static bool PreHandleActionStackableMerge(uint mergeFromGuid, uint mergeToGuid, int amount, ref Player __instance)
    {
        var localInstance = __instance;
        var auctionItemsGuid = AuctionManager.ItemsContainer.Guid.Full;

        if (amount <= 0)
        {
            return true;
        }

        localInstance.FindObject(mergeFromGuid, Player.SearchLocations.LocationsICanMove, out _, out var sourceStackRootOwner, out _);
        localInstance.FindObject(mergeToGuid, Player.SearchLocations.LocationsICanMove, out _, out var targetStackRootOwner, out _);

        if (sourceStackRootOwner.IsAuctionItemsContainer() || targetStackRootOwner.IsAuctionItemsContainer())
        {
            localInstance.Session.Network.EnqueueSend(new GameEventInventoryServerSaveFailed(localInstance.Session, mergeFromGuid));
            return false;
        }
        else
            return true;
    }



    [HarmonyPrefix]
    [HarmonyPatch(typeof(Player), "HandleActionPutItemInContainer_Verify", new Type[] { typeof(uint), typeof(uint), typeof(int), typeof(Container), typeof(WorldObject), typeof(Container), typeof(Container), typeof(bool) }, new ArgumentType[] { ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Out, ArgumentType.Out, ArgumentType.Out, ArgumentType.Out, ArgumentType.Out })]
    public static bool PreHandleActionPutItemInContainer_Verify(
            uint itemGuid,
            uint containerGuid,
            int placement,
            ref Container itemRootOwner,
            ref WorldObject item,
            ref Container containerRootOwner,
            ref Container container,
            ref bool itemWasEquipped,
            ref Player __instance,
            ref bool __result
        )
    {

        var localInstance = __instance;

        itemRootOwner = null;
        item = null;
        container = null;
        containerRootOwner = null;
        itemWasEquipped = false;


        if (localInstance.suicideInProgress)
        {
            localInstance.Session.Network.EnqueueSend(new GameEventWeenieError(localInstance.Session, WeenieError.YoureTooBusy));
            localInstance.Session.Network.EnqueueSend(new GameEventInventoryServerSaveFailed(localInstance.Session, itemGuid));
            __result = false;
            return false;
        }

        if (localInstance.IsBusy)
        {
            if (localInstance.PickupState != PickupState.Return || localInstance.NextPickup != null)
            {
                localInstance.Session.Network.EnqueueSend(new GameEventWeenieError(localInstance.Session, WeenieError.YoureTooBusy));
                localInstance.Session.Network.EnqueueSend(new GameEventInventoryServerSaveFailed(localInstance.Session, itemGuid));
            }
            else
                localInstance.NextPickup = () => { localInstance.HandleActionPutItemInContainer(itemGuid, containerGuid, placement); };

            __result = false;
            return false;
        }

        //OnPutItemInContainer(itemGuid, containerGuid, placement);

        item = localInstance.FindObject(itemGuid, Player.SearchLocations.LocationsICanMove, out _, out itemRootOwner, out itemWasEquipped);
        container = localInstance.FindObject(containerGuid, Player.SearchLocations.MyInventory | Player.SearchLocations.Landblock | Player.SearchLocations.LastUsedContainer, out _, out containerRootOwner, out _) as Container;
        var lastOpenedContainer = (Container)localInstance.FindObject(localInstance.LastOpenedContainerId, Player.SearchLocations.Everywhere, out _, out _, out _);

        containerRootOwner = containerRootOwner;


        if (item == null && lastOpenedContainer != null && lastOpenedContainer.IsCustomContainer() && lastOpenedContainer.Inventory.TryGetValue(new ObjectGuid(itemGuid), out var lastOpenedContainerItem))
        {
            item = lastOpenedContainerItem;
            containerRootOwner = localInstance;
            itemRootOwner = lastOpenedContainer;
        }

        if (item == null)
        {
            localInstance.Session.Network.EnqueueSend(new GameEventCommunicationTransientString(localInstance.Session, "Source item not found!")); // Custom error message
            localInstance.Session.Network.EnqueueSend(new GameEventInventoryServerSaveFailed(localInstance.Session, itemGuid));
            __result = false;
            return false;
        }

        if (!item.Guid.IsDynamic() || item is Creature || item.Stuck)
        {
            //log.WarnFormat("Player 0x{0:X8}:{1} tried to move item 0x{2:X8}:{3}.", localInstance.Guid.Full, localInstance.Name, item.Guid.Full, item.Name);
            localInstance.Session.Network.EnqueueSend(new GameEventWeenieError(localInstance.Session, WeenieError.Stuck));
            localInstance.Session.Network.EnqueueSend(new GameEventInventoryServerSaveFailed(localInstance.Session, itemGuid));
            __result = false;
            return false;
        }

        if (itemRootOwner != localInstance && containerRootOwner == localInstance && !localInstance.HasEnoughBurdenToAddToInventory(item))
        {
            localInstance.Session.Network.EnqueueSend(new GameEventCommunicationTransientString(localInstance.Session, "You are too encumbered to carry that!"));
            localInstance.Session.Network.EnqueueSend(new GameEventInventoryServerSaveFailed(localInstance.Session, itemGuid));
            __result = false;
            return false;
        }

        if (container == null)
        {
            localInstance.Session.Network.EnqueueSend(new GameEventCommunicationTransientString(localInstance.Session, "Target container not found!")); // Custom error message
            localInstance.Session.Network.EnqueueSend(new GameEventInventoryServerSaveFailed(localInstance.Session, itemGuid));
            __result = false;
            return false;
        }

        if (container != null && (container.IsCustomContainer() || (itemRootOwner != null && itemRootOwner.IsCustomContainer())))
        {

            // prevent add/remove from auction house items chest 
            if (container.IsAuctionItemsContainer() || itemRootOwner.IsAuctionItemsContainer())
            {
                localInstance.Session.Network.EnqueueSend(new GameEventInventoryServerSaveFailed(localInstance.Session, itemGuid));
                __result = false;
                return false;
            }
        }

        if (container is Corpse)
        {
            localInstance.Session.Network.EnqueueSend(new GameEventCommunicationTransientString(localInstance.Session, $"You cannot put {item.Name} in that.")); // Custom error message
            localInstance.Session.Network.EnqueueSend(new GameEventInventoryServerSaveFailed(localInstance.Session, itemGuid));
            __result = false;
            return false;
        }

        if (localInstance.IsTrading && item.IsBeingTradedOrContainsItemBeingTraded(localInstance.ItemsInTradeWindow))
        {
            localInstance.Session.Network.EnqueueSend(new GameEventInventoryServerSaveFailed(localInstance.Session, itemGuid, WeenieError.TradeItemBeingTraded));
            __result = false;
            return false;
        }

        if (containerRootOwner != localInstance) // Is our target on the landscape?
        {
            if (itemRootOwner == localInstance && item.IsAttunedOrContainsAttuned)
            {
                localInstance.Session.Network.EnqueueSend(new GameEventInventoryServerSaveFailed(localInstance.Session, itemGuid, WeenieError.AttunedItem));
                __result = false;
                return false;
            }

            if (itemRootOwner == localInstance && item is PetDevice petDevice && petDevice.Pet is not null)
            {
                localInstance.Session.Network.EnqueueSend(new GameEventCommunicationTransientString(localInstance.Session, "You must unsummon your pet before you can transfer this item!"));
                localInstance.Session.Network.EnqueueSend(new GameEventInventoryServerSaveFailed(localInstance.Session, itemGuid, WeenieError.None));
                __result = false;
                return false;
            }

            if (containerRootOwner != null && !containerRootOwner.IsOpen)
            {
                localInstance.Session.Network.EnqueueSend(new GameEventInventoryServerSaveFailed(localInstance.Session, itemGuid, WeenieError.TheContainerIsClosed));
                __result = false;
                return false;
            }
        }

        if (containerRootOwner is Corpse corpse)
        {
            if (!corpse.IsMonster)
            {
                localInstance.Session.Network.EnqueueSend(new GameEventInventoryServerSaveFailed(localInstance.Session, itemGuid, WeenieError.Dead));
                __result = false;
                return false;
            }
        }

        if (container is Hook hook)
        {
            if (PropertyManager.GetBool("house_hook_limit").Item)
            {
                if (hook.House.HouseMaxHooksUsable != -1 && hook.House.HouseCurrentHooksUsable <= 0)
                {
                    localInstance.Session.Network.EnqueueSend(new GameEventInventoryServerSaveFailed(localInstance.Session, itemGuid, WeenieError.YouHaveUsedAllTheHooks));
                    __result = false;
                    return false;
                }
            }

            if (PropertyManager.GetBool("house_hookgroup_limit").Item)
            {
                var itemHookGroup = item.HookGroup ?? HookGroupType.Undef;
                var houseHookGroupMax = hook.House.GetHookGroupMaxCount(itemHookGroup);
                var houseHookGroupCurrent = hook.House.GetHookGroupCurrentCount(itemHookGroup);
                if (houseHookGroupMax != -1 && houseHookGroupCurrent >= houseHookGroupMax)
                {
                    localInstance.Session.Network.EnqueueSend(new GameEventInventoryServerSaveFailed(localInstance.Session, itemGuid));
                    localInstance.Session.Player.SendWeenieErrorWithString(WeenieErrorWithString.MaxNumberOf_Hooked, itemHookGroup.ToSentence());
                    __result = false;
                    return false;
                }
            }
        }

        if (item is Container)
        {
            // Blocking all attempts to put containers in things that aren't Players and Storage. This may not be retail, but at this time appears to be best catch all solution to Quest stamp bypass issue.
            if (container is not Player && container is not Storage)
            {
                //Session.Network.EnqueueSend(new GameEventCommunicationTransientString(Session, $"You cannot put {item.Name} in that.")); // Custom error message
                localInstance.Session.Network.EnqueueSend(new GameEventInventoryServerSaveFailed(localInstance.Session, itemGuid));

                __result = false;
                return false;
            }
        }

        if (containerRootOwner == null) // container is on landscape, so you must have it open
        {
            if (!container.IsOpen || (!container.IsCustomContainer()))
            {
                localInstance.Session.Network.EnqueueSend(new GameEventInventoryServerSaveFailed(localInstance.Session, itemGuid, WeenieError.TheContainerIsClosed));

                __result = false;
                return false;
            }
        }

        __result = true;
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Container), nameof(Container.TryAddToInventory), new Type[] { typeof(WorldObject), typeof(int), typeof(bool), typeof(bool) })]
    public static bool PreTryAddToInventory(WorldObject worldObject, int placementPosition, bool limitToMainPackOnly, bool burdenCheck, ref Container __instance, ref bool __result)
    {
        var localInstance = __instance;
        if (!localInstance.IsCustomContainer())
            return true;


        if (worldObject == null)
        {
            __result = false;
            return false;
        }

        var isBank = localInstance.IsBank();

        // use a single bank container instance 
        if (isBank)
            localInstance = BankManager.BankContainer;

        object containerLock = localInstance.GetCustomContainerLock();

        lock(containerLock)
        {
            __result = localInstance.TryAddToInventory(worldObject, out _, placementPosition, limitToMainPackOnly, burdenCheck);
            return false;
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Container), nameof(Container.TryRemoveFromInventory), new Type[] { typeof(ObjectGuid), typeof(bool) })]
    public static bool PreTryRemoveFromInventory(ObjectGuid objectGuid, bool forceSave, ref Container __instance, ref bool __result)
    {
        var localInstance = __instance;
        if (!localInstance.IsCustomContainer())
            return true;

        var isBank = localInstance.IsBank();

        object containerLock = localInstance.GetCustomContainerLock();

        if (isBank)
            localInstance = BankManager.BankContainer;

        lock (containerLock)
        {
            __result = localInstance.TryRemoveFromInventory(objectGuid, out WorldObject item, forceSave);
            if (__result)
            {
                // set the bank id here any time a player attempts to remove or add an item to the bank. 
                if (isBank)
                {
                    item.RemoveProperty(FakeIID.BankId);
                    item.SaveBiotaToDatabase();
                }
            }

            return false;
        }
    }


    [HarmonyPrefix]
    [HarmonyPatch(typeof(Container), nameof(Container.TryAddToInventory), new Type[] { typeof(WorldObject), typeof(Container), typeof(int), typeof(bool), typeof(bool) }, new ArgumentType[] { ArgumentType.Normal, ArgumentType.Out, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal })]
    public static bool PreTryAddToInventory(WorldObject worldObject, Container container, int placementPosition, bool limitToMainPackOnly, bool burdenCheck, ref Container __instance, ref bool __result)
    {
        var localInstance = __instance;
        if (!localInstance.IsCustomContainer())
            return true;

        IList<WorldObject> containerItems;

        if (worldObject.UseBackpackSlot)
        {
            containerItems = localInstance.Inventory.Values.Where(i => i.UseBackpackSlot).ToList();

            var containrCapacity = localInstance.GetContainerCapacity();
            if (containrCapacity <= containerItems.Count)
            {
                container = null;
                __result = false;

                return false;
            }
        }
        else
        {
            containerItems = localInstance.Inventory.Values.Where(i => !i.UseBackpackSlot).ToList();

            var itemsCapacity = localInstance.GetItemsCapacity();
            if (itemsCapacity <= containerItems.Count)
            {
                // Can we add this to any side pack?
                if (!limitToMainPackOnly)
                {
                    var containers = localInstance.Inventory.Values.OfType<Container>().ToList();
                    containers.Sort((a, b) => (a.Placement ?? 0).CompareTo(b.Placement ?? 0));

                    foreach (var sidePack in containers)
                    {
                        if (sidePack.TryAddToInventory(worldObject, out container, placementPosition, true))
                        {
                            localInstance.EncumbranceVal += (worldObject.EncumbranceVal ?? 0);
                            localInstance.Value += (worldObject.Value ?? 0);

                            __result = true;
                            return false;
                        }
                    }
                }

                container = null;
                __result = false;

                return false;
            }
        }

        if (localInstance.Inventory.ContainsKey(worldObject.Guid))
        {
            container = null;
            __result = false;
            return false;
        }

        worldObject.Location = null;
        worldObject.Placement = ACE.Entity.Enum.Placement.Resting;

        worldObject.OwnerId = localInstance.Guid.Full;
        worldObject.ContainerId = localInstance.Guid.Full;
        worldObject.Container = localInstance;
        worldObject.PlacementPosition = placementPosition; // Server only variable that we use to remember/restore the order in which items exist in a container

        // Move all the existing items PlacementPosition over.
        if (!worldObject.UseBackpackSlot)
            containerItems.Where(i => !i.UseBackpackSlot && i.PlacementPosition >= placementPosition).ToList().ForEach(i => i.PlacementPosition++);
        else
            containerItems.Where(i => i.UseBackpackSlot && i.PlacementPosition >= placementPosition).ToList().ForEach(i => i.PlacementPosition++);

        localInstance.Inventory.Add(worldObject.Guid, worldObject);

        localInstance.EncumbranceVal += (worldObject.EncumbranceVal ?? 0);
        localInstance.Value += (worldObject.Value ?? 0);

        container = localInstance;

        localInstance.OnAddItem();

        __result = true;
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Chest), "SetEphemeralValues")]
    public static bool PreSetEphemeralValues(ref Chest __instance)
    {
        var localInstance = __instance;
        if (!localInstance.IsCustomContainer())
            return true;

        localInstance.ActivationResponse |= ActivationResponse.Use;   // todo: fix broken data

        localInstance.CurrentMotionState = Chest.motionClosed;              // do any chests default to open?

        if (localInstance.IsLocked)
            localInstance.DefaultLocked = true;

        if (localInstance.DefaultLocked) // ignore regen interval, only regen on relock
            localInstance.NextGeneratorRegenerationTime = double.MaxValue;

        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Container), nameof(Container.GetFreeInventorySlots), new Type[] { typeof(bool) })]
    public static bool PreGetFreeInventorySlots(bool includeSidePacks, ref Container __instance, ref int __result)
    {
        var localInstance = __instance;
        if (!localInstance.IsCustomContainer())
            return true;

        int freeSlots = localInstance.GetItemsCapacity() - localInstance.CountPackItems();

        if (includeSidePacks)
        {
            foreach (var sidePack in localInstance.Inventory.Values.OfType<Container>())
                freeSlots += (sidePack.ItemCapacity ?? 0) - sidePack.CountPackItems();
        }

        __result = freeSlots;
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Container), nameof(Container.GetFreeContainerSlots))]
    public static bool PreGetFreeContainerSlots(ref Container __instance, ref int __result)
    {
        var localInstance = __instance;
        if (!localInstance.IsCustomContainer())
            return true;

        __result = localInstance.GetContainerCapacity() - localInstance.CountContainers();
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Player), "DoHandleActionPutItemInContainer", new Type[] { typeof(WorldObject), typeof(Container), typeof(bool), typeof(Container), typeof(Container), typeof(int) })]
    public static bool PreDoHandleActionPutItemInContainer(WorldObject item, Container itemRootOwner, bool itemWasEquipped, Container container, Container containerRootOwner, int placement, ref Player __instance, ref bool __result)
    {
        var localInstance = __instance;

        //Console.WriteLine($"-> DoHandleActionPutItemInContainer({item.Name}, {itemRootOwner?.Name}, {itemWasEquipped}, {container?.Name}, {containerRootOwner?.Name}, {placement})");

        Position prevLocation = null;
        Landblock prevLandblock = null;

        var prevContainer = item.Container;


        localInstance.OnPutItemInContainer(item.Guid.Full, container.Guid.Full, placement);

        if (item.CurrentLandblock != null) // Movement is an item pickup off the landblock
        {
            prevLocation = new Position(item.Location);
            prevLandblock = item.CurrentLandblock;

            item.CurrentLandblock.RemoveWorldObject(item.Guid, false, true);
            item.Location = null;
        }
        else if (itemWasEquipped) // Movement is an equipped item to a container on the landblock
        {
            var dequipObjectAction = containerRootOwner == localInstance ? DequipObjectAction.DequipToPack : DequipObjectAction.DequipToOffPlayerContainer;

            if (!localInstance.TryDequipObjectWithNetworking(item.Guid, out _, dequipObjectAction))
            {
                localInstance.Session.Network.EnqueueSend(new GameEventCommunicationTransientString(localInstance.Session, "TryDequipObjectWithNetworking failed!")); // Custom error message
                localInstance.Session.Network.EnqueueSend(new GameEventInventoryServerSaveFailed(localInstance.Session, item.Guid.Full));
                __result = false;
                return false;
            }
        }
        else // Movement is within the same pack or between packs in a container on the landblock
        {
            var itemRootCreature = itemRootOwner as Creature;

            if (itemRootOwner != null && !itemRootOwner.TryRemoveFromInventory(item.Guid) && (itemRootCreature == null || !itemRootCreature.TryDequipObject(item.Guid, out _, out _)))
            {
                localInstance.Session.Network.EnqueueSend(new GameEventCommunicationTransientString(localInstance.Session, "TryRemoveFromInventory failed!")); // Custom error message
                localInstance.Session.Network.EnqueueSend(new GameEventInventoryServerSaveFailed(localInstance.Session, item.Guid.Full));
            }

            if (itemRootOwner == localInstance && containerRootOwner != localInstance)
            {
                // We must update the database with the latest ContainerId and WielderId properties.
                // If we don't, the player can drop the item, log out, and log back in. If the landblock hasn't queued a database save in that time,
                // the player will end up loading with this object in their inventory even though the landblock is the true owner. This is because
                // when we load player inventory, the database still has the record that shows this player as the ContainerId for the item.
                localInstance.DeepSave(item);
            }
        }

        var burdenCheck = itemRootOwner != localInstance && containerRootOwner == localInstance;

        if (!container.TryAddToInventory(item, placement, true, burdenCheck))
        {
            localInstance.Session.Network.EnqueueSend(new GameEventCommunicationTransientString(localInstance.  Session, $"Unable to put {item.Name} into container")); // Custom error message
            localInstance.Session.Network.EnqueueSend(new GameEventInventoryServerSaveFailed(localInstance.Session, item.Guid.Full));

            if (prevLocation != null)
            {
                var landblockReturn = new ActionChain();

                landblockReturn.AddDelaySeconds(1);
                landblockReturn.AddAction(prevLandblock, () => localInstance.RemoveTrackedObject(item, false));
                landblockReturn.AddDelaySeconds(1);
                landblockReturn.AddAction(prevLandblock, () =>
                {
                    item.Location = new Position(prevLocation);
                    LandblockManager.AddObject(item);
                });
                landblockReturn.EnqueueChain();
            }
            else if (itemRootOwner == null || !itemRootOwner.TryAddToInventory(item))
            {
                ModManager.Log($"{localInstance.Name}.DoHandleActionPutItemInContainer({item.Name} ({item.Guid}), {itemRootOwner?.Name} ({itemRootOwner?.Guid}), {itemWasEquipped}, {container.Name} ({container.Guid}), {containerRootOwner?.Name} ({containerRootOwner?.Guid}), {placement}) - removed item from original location, failed to add to new container, failed to re-add to original location", ModManager.LogLevel.Error);
            }

            __result = false;
            return false;
        }

        if (container != containerRootOwner && containerRootOwner != null)
        {
            containerRootOwner.EncumbranceVal += (item.EncumbranceVal ?? 0);
            containerRootOwner.Value += (item.Value ?? 0);
        }

        var isBank = container.IsBank();

        if (isBank)
            item.SetProperty(FakeIID.BankId, localInstance.Account.AccountId);

        // when moving from a non-stuck container to a different container,
        // the database must be synced immediately
        if (isBank || prevContainer != null && !prevContainer.Stuck && container != prevContainer)
            item.SaveBiotaToDatabase();

        localInstance.Session.Network.EnqueueSend(
            new GameMessagePublicUpdateInstanceID(item, PropertyInstanceId.Container, container.Guid),
            new GameEventItemServerSaysContainId(localInstance.Session, item, container));

        __result = true;
        return false;
    }
}
