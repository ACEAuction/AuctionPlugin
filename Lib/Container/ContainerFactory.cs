using ACE.Database;
using ACE.Shared;
using ACE.Database.Models.Shard;
using ACE.Entity;
using ACE.Entity.Enum.Properties;
using ACE.Server.Entity.Actions;
using ACE.Server.Factories;
using ACE.Server.Managers;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Network.GameMessages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ACE.Mods.Legend.Lib.Common;
using static ACE.Server.WorldObjects.Player;
using ACE.Mods.Legend.Lib.Mail;

namespace ACE.Mods.Legend.Lib.Container
{
    public static class ContainerFactory
    {
        private static uint GetContainerId(string keycode)
        {
            using (var ctx = new ShardDbContext())
            {
                var query = from container in ctx.Biota
                            join cType in ctx.BiotaPropertiesString on container.Id equals cType.ObjectId
                            where cType.Type == (ushort)PropertyString.Name && cType.Value == keycode
                            select container.Id;

                var containerId = query.FirstOrDefault();

                return containerId;
            }
        }

        public static Chest CreateContainer(string keycode, Position containerPosition)
        {
            var containerId = GetContainerId(keycode);

            Chest chest;
            var weenie = DatabaseManager.World.GetCachedWeenie((uint)WeenieClassName.W_CHEST_CLASS);

            var lb = LandblockManager.GetLandblock(containerPosition.LandblockId, false, true);

            if (lb == null)
                throw new Exception($"The landblock for the auction container with id: {containerId} does not exist");

            chest = (Chest)lb.GetObject(new ObjectGuid(containerId), false);

            if (chest != null)
                return chest;

            if (containerId == 0)
            {
                var guid = GuidManager.NewDynamicGuid();
                chest = (Chest)WorldObjectFactory.CreateWorldObject(weenie, guid);
            }
            else
            {
                // use the biota if it exists, else abort
                var biota = DatabaseManager.Shard.BaseDatabase.GetBiota(containerId);

                // should never happen
                if (biota == null)
                    throw new Exception($"Failed to retrieve container biota with id: {containerId}, contact an admin!");

                chest = (Chest)WorldObjectFactory.CreateWorldObject(biota);
            }

            chest.DisplayName = keycode;
            chest.Location = new Position(containerPosition);
            chest.TimeToRot = -1;
            chest.SetProperty(PropertyInt.ItemsCapacity, int.MaxValue);
            chest.SetProperty(PropertyString.Name, keycode);
            chest.SaveBiotaToDatabase();

            if (chest == null)
                throw new Exception($"Failed to create container with id: {containerId}");

            return chest;
        }
    }

    [HarmonyPatchCategory(nameof(ContainerPatches))]
    public static class ContainerPatches
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(ACE.Server.WorldObjects.Container), nameof(ACE.Server.WorldObjects.Container.ClearUnmanagedInventory), new Type[] { typeof(bool) })]
        public static bool PreClearUnmanagedInventory(bool forceSave, ref ACE.Server.WorldObjects.Container __instance, ref bool __result)
        {
            if
            (
                __instance is Storage ||
                __instance.WeenieClassId == (uint)WeenieClassName.W_STORAGE_CLASS ||
                __instance.Name == Constants.AUCTION_LISTINGS_CONTAINER_KEYCODE ||
                __instance.Name == Constants.AUCTION_ITEMS_CONTAINER_KEYCODE ||
                __instance.Name == Constants.MAIL_CONTAINER_KEYCODE
            )
            {
                __result = false; // Do not clear storage, ever.
                return false;
            }

            var success = true;
            var itemGuids = __instance.Inventory.Where(i => i.Value.GeneratorId == null).Select(i => i.Key).ToList();
            foreach (var itemGuid in itemGuids)
            {
                if (!__instance.TryRemoveFromInventory(itemGuid, out var item, forceSave))
                    success = false;

                if (success)
                    item.Destroy();
            }
            if (forceSave)
                __instance.SaveBiotaToDatabase();

            __result = success;
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ACE.Server.WorldObjects.Container), nameof(ACE.Server.WorldObjects.Container.Open), new Type[] { typeof(Player) })]
        public static bool PreOpen(Player player, ref ACE.Server.WorldObjects.Container __instance)
        {
            var localInstance = __instance;

            if (!IsCustomContainer(localInstance))
                return true;

            player.LastOpenedContainerId = localInstance.Guid;

            localInstance.Viewer = player.Guid.Full;

            if (!localInstance.IsOpen)
                localInstance.DoOnOpenMotionChanges();

            localInstance.IsOpen = true;

            var customContainerOpen = player.GetProperty(FakeBool.IsCustomContainerOpen);

            if (customContainerOpen.HasValue && customContainerOpen.Value)
            {
                player.Session.Network.EnqueueSend(new GameEventCloseGroundContainer(player.Session, localInstance));

                if (player.LastOpenedContainerId == localInstance.Guid)
                    player.LastOpenedContainerId = ObjectGuid.Invalid;
                player.SetProperty(FakeBool.IsCustomContainerOpen, false);
            } else
                player.SetProperty(FakeBool.IsCustomContainerOpen, true);

            ModManager.Log("SENDING INVENTORY");
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

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ACE.Server.WorldObjects.Container), "SendInventory", new Type[] { typeof(Player) })]
        public static bool PreSendInventory(Player player, ref ACE.Server.WorldObjects.Container __instance)
        {
            var localInstance = __instance;
            if (!IsCustomContainer(localInstance))
                return true;

            // send createobject for all objects in this container's inventory to player
            var itemsToSend = new List<GameMessage>();
            var inventory = new List<WorldObject>();

            if (localInstance.Name == Constants.MAIL_CONTAINER_KEYCODE)
                inventory = localInstance.Inventory.Values
                    .Where(item =>
                    {
                        var mailTo = item.GetProperty(ACE.Shared.FakeIID.MailTo);
                        return mailTo.HasValue && mailTo.Value == player.Guid.Full;
                    }).ToList();

            foreach (var item in inventory)
            {
                var mailTo = item.GetProperty(ACE.Shared.FakeIID.MailTo);
                ModManager.Log($"NAME = {item.Name}, mailTo = {mailTo}");
                // FIXME: only send messages for unknown objects
                itemsToSend.Add(new GameMessageCreateObject(item));

                if (item is ACE.Server.WorldObjects.Container container)
                {
                    foreach (var containerItem in container.Inventory.Values)
                        itemsToSend.Add(new GameMessageCreateObject(containerItem));
                }
            }

            player.Session.Network.EnqueueSend(new GameEventViewContents(player.Session, localInstance));

            // send sub-containers
            foreach (var container in inventory.Where(i => i is ACE.Server.WorldObjects.Container))
                player.Session.Network.EnqueueSend(new GameEventViewContents(player.Session, (ACE.Server.WorldObjects.Container)container));

            ModManager.Log($"Items to Send Count: {itemsToSend.Count}");
            player.Session.Network.EnqueueSend(itemsToSend);

            return false;
        }


        [HarmonyPrefix]
        [HarmonyPatch(typeof(Chest), nameof(Chest.CheckUseRequirements), new Type[] { typeof(WorldObject) })]
        public static bool PreCheckUseRequirements(WorldObject activator, ref Chest __instance, ref ActivationResult __result)
        {
            var localInstance = __instance;
            if (!IsCustomContainer(localInstance))
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
        [HarmonyPatch(typeof(ACE.Server.WorldObjects.Container), nameof(ACE.Server.WorldObjects.Container.FinishClose), new Type[] { typeof(Player) })]
        public static bool PreFinishClose(Player player, ref ACE.Server.WorldObjects.Container __instance)
        {
            var localInstance = __instance;
            if (!IsCustomContainer(localInstance))
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

            var wo = localInstance.FindObject(objectGuid, SearchLocations.Everywhere, out _, out _, out _);

            var lastOpenedContainer = (ACE.Server.WorldObjects.Container)localInstance.FindObject(localInstance.LastOpenedContainerId, SearchLocations.Everywhere, out _, out _, out _);

            if (wo == null)
                if (lastOpenedContainer != null && IsCustomContainer(lastOpenedContainer) && lastOpenedContainer.Inventory.TryGetValue(new ObjectGuid(objectGuid), out var item))
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

            localInstance.Examine(wo);

            return true;
        }


        [HarmonyPrefix]
        [HarmonyPatch(typeof(Player), "HandleActionPutItemInContainer_Verify", new Type[] { typeof(uint), typeof(uint), typeof(int), typeof(ACE.Server.WorldObjects.Container), typeof(WorldObject), typeof(ACE.Server.WorldObjects.Container), typeof(ACE.Server.WorldObjects.Container), typeof(bool) }, new ArgumentType[] { ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Out, ArgumentType.Out, ArgumentType.Out, ArgumentType.Out, ArgumentType.Out })]
        public static bool PreHandleActionPutItemInContainer_Verify(
                uint itemGuid,
                uint containerGuid,
                int placement,
                ref ACE.Server.WorldObjects.Container itemRootOwner,
                ref WorldObject item,
                ref ACE.Server.WorldObjects.Container containerRootOwner,
                ref ACE.Server.WorldObjects.Container container,
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

            item = localInstance.FindObject(itemGuid, SearchLocations.LocationsICanMove, out _, out itemRootOwner, out itemWasEquipped);
            container = localInstance.FindObject(containerGuid, SearchLocations.MyInventory | SearchLocations.Landblock | SearchLocations.LastUsedContainer, out _, out containerRootOwner, out _) as ACE.Server.WorldObjects.Container;
            var lastOpenedContainer = (ACE.Server.WorldObjects.Container)localInstance.FindObject(localInstance.LastOpenedContainerId, SearchLocations.Everywhere, out _, out _, out _);

            containerRootOwner = containerRootOwner;


            if (item == null)
                if (lastOpenedContainer != null && IsCustomContainer(lastOpenedContainer) && lastOpenedContainer.Inventory.TryGetValue(new ObjectGuid(itemGuid), out var lastOpenedContainerItem))
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
                    ModManager.Log($"CONTAINER ROOT OWNER = {containerRootOwner.Name}, ISOPEN = {containerRootOwner.IsOpen}", ModManager.LogLevel.Warn);
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

            if (item is ACE.Server.WorldObjects.Container)
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
                if (!container.IsOpen || (!IsCustomContainer(container) && container.Viewer != localInstance.Guid.Full))
                {
                    ModManager.Log($"ISOPEN ={container.IsOpen}  VIEWER = {container.Viewer} localInstance.GUID = ", ModManager.LogLevel.Warn);
                    localInstance.Session.Network.EnqueueSend(new GameEventInventoryServerSaveFailed(localInstance.Session, itemGuid, WeenieError.TheContainerIsClosed));

                    __result = false;
                    return false;
                }
            }

            __result = true;
            return false;
        }

     


        private static bool IsCustomContainer(ACE.Server.WorldObjects.Container localInstance)
        {
            return localInstance.Name == Constants.MAIL_CONTAINER_KEYCODE;
        }
    }
}

