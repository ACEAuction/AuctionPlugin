using ACE.Mods.Legend.Lib.Common;
using ACE.Mods.Legend.Lib.Container;
using ACE.Server.Managers;

namespace ACE.Mods.Legend.Lib.Bank;

public static class BankManager
{
    public readonly static object BankLock = new object();

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
}

