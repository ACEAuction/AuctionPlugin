using ACE.Server.Managers;

namespace ACE.Mods.Legend.Lib.Bank
{
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
