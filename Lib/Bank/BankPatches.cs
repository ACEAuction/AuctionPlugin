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

        [CommandHandler("bank-clear", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0, "Clears your bank, deleting all items.")]
        public static void HandleBankClear(Session session, params string[] parameters)
        {
            try
            {
                session.Player.ClearBank();
            }
            catch (Exception ex)
            {
                ModManager.Log(ex.Message, ModManager.LogLevel.Error);
            }
        }
    }
}
