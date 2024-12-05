using ACE.Entity;
using ACE.Mods.Legend.Lib.Common;
using ACE.Mods.Legend.Lib.Common.Errors;
using ACE.Mods.Legend.Lib.Container;
using ACE.Server.Managers;
using ACE.Server.Network.GameMessages.Messages;
using System.Collections.Concurrent;

namespace ACE.Mods.Legend.Lib.Bank
{
    public static class BankManager
    {
        private readonly static object BankLock = new object();

        private static double NextTickTime = 0;

        private static readonly double TickTime = 5;

        public static WeakReference<Chest>? _BankContainer = null;

        public static Chest BankContainer => GetOrCreateBankContainer();

        private static Chest CreateBankContainer()
        {
            return ContainerFactory
                .CreateContainer(Constants.BANK_CONTAINER_KEYCODE, Constants.BANK_CONTAINER_LOCATION);
        }

        public static void Tick(double currentUnixTime)
        {

            if (ServerManager.ShutdownInProgress)
                return;

            var BankLb = LandblockManager.GetLandblock(Constants.BANK_CONTAINER_LOCATION.LandblockId, false, true);
            if (BankLb.CreateWorldObjectsCompleted && BankLb.GetObject(BankContainer.Guid, false) == null)
                BankContainer.EnterWorld();

            if (NextTickTime > currentUnixTime)
                return;
        }

        private static Chest GetOrCreateBankContainer()
        {
            if (_BankContainer == null || !_BankContainer.TryGetTarget(out var chest))
            {
                chest = CreateBankContainer();
                _BankContainer = new WeakReference<Chest>(chest);
            }
            return chest;
        }

        public static bool TryAddToBankContainer(WorldObject item)
        {
            lock (BankLock)
            {
                return BankContainer.TryAddToInventory(item);
            }
        }
        public static bool TryRemoveFromBankContainer(WorldObject item)
        {
            lock (BankLock)
            {
                return BankContainer.TryRemoveFromInventory(item.Guid);
            }
        }
    }

    [HarmonyPatchCategory(nameof(BankPatches))]
    public static class BankPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(WorldManager), nameof(WorldManager.UpdateGameWorld))]
        public static void PostUpdateGameWorld(ref bool __result)
        {
            BankManager.Tick(Time.GetUnixTime());
        }

    }
}

