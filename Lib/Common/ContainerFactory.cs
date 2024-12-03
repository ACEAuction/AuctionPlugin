using ACE.Database;
using ACE.Database.Models.Shard;
using ACE.Entity;
using ACE.Entity.Enum.Properties;
using ACE.Server.Factories;
using ACE.Server.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACE.Mods.Legend.Lib.Common
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
        [HarmonyPatch(typeof(Container), nameof(Container.ClearUnmanagedInventory), new Type[] { typeof(bool) })]
        public static bool PreClearUnmanagedInventory(bool forceSave, ref Container __instance, ref bool __result)
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
    }
}
