using ACE.Entity;
using ACE.Mods.Legend.Lib.Common;
using ACE.Mods.Legend.Lib.Common.Errors;
using ACE.Mods.Legend.Lib.Container;
using ACE.Server.Managers;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Shared;
using System.Collections.Concurrent;

namespace ACE.Mods.Legend.Lib.Mail
{
    public static class MailManager
    {
        private readonly static object MailLock = new object();

        private static double NextTickTime = 0;

        private static readonly double TickTime = 5;


        public static WeakReference<Chest>? _mailContainer = null;

        public static Chest MailContainer => GetOrCreateMailContainer();


        private static Chest CreateMailContainer()
        {
            return ContainerFactory
                .CreateContainer(Constants.MAIL_CONTAINER_KEYCODE, Constants.MAIL_CONTAINER_LOCATION);
        }

        public static void Tick(double currentUnixTime)
        {
            var mailLb = LandblockManager.GetLandblock(Constants.MAIL_CONTAINER_LOCATION.LandblockId, false, true);
            if (mailLb.CreateWorldObjectsCompleted && mailLb.GetObject(MailContainer.Guid, false) == null)
                MailContainer.EnterWorld();

            if (NextTickTime > currentUnixTime)
                return;
        }

        private static Chest GetOrCreateMailContainer()
        {
            if (_mailContainer == null || !_mailContainer.TryGetTarget(out var chest))
            {
                chest = CreateMailContainer();
                _mailContainer = new WeakReference<Chest>(chest);
            }
            return chest;
        }

        public static bool TryAddToMailContainer(WorldObject item)
        {
            lock (MailLock)
            {
                return MailContainer.TryAddToInventory(item);
            }
        }
        public static bool TryRemoveFromMailContainer(WorldObject item)
        {
            lock (MailLock)
            {
                return MailContainer.TryRemoveFromInventory(item.Guid);
            }
        }
    }

    [HarmonyPatchCategory(nameof(MailPatches))]
    public static class MailPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(WorldManager), nameof(WorldManager.UpdateGameWorld))]
        public static void PostUpdateGameWorld(ref bool __result)
        {
            MailManager.Tick(Time.GetUnixTime());
        }

    }
}

