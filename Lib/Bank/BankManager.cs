using ACE.Entity;
using ACE.Entity.Enum.Properties;
using ACE.Mods.Legend.Lib.Common;
using ACE.Mods.Legend.Lib.CustomContainer;
using ACE.Server.Managers;
using ACE.Server.WorldObjects;
using ACE.Shared;

namespace ACE.Mods.Legend.Lib.Bank;

public static class BankManager
{
    public readonly static object BankLock = new object();

    private static double NextTickTime = 0;

    private static readonly double TickTime = 5;

    public static WeakReference<Chest>? _BankContainer = null;

    public readonly static int MAX_ITEM_CAPACITY = 255;

    private static void Log(string message, ModManager.LogLevel level = ModManager.LogLevel.Info)
    {
        ModManager.Log($"[Bank] {message}", level);
    }

    public static Chest BankContainer => GetOrCreateBankContainer();

    private static Chest CreateBankContainer()
    {
        var keycode = Constants.BANK_CONTAINER_KEYCODE;
        var location = Constants.BANK_CONTAINER_LOCATION;

        return ContainerFactory
            .CreateContainer(keycode, location, onCreate: (Chest chest) =>
            {
                chest.DisplayName = keycode;
                chest.Location = new Position(location);
                chest.TimeToRot = -1;
                chest.SetProperty(PropertyInt.ItemsCapacity, int.MaxValue);
                chest.SetProperty(PropertyInt.ContainersCapacity, int.MaxValue);
                chest.SetProperty(PropertyInt.EncumbranceCapacity, int.MaxValue);
                chest.SetProperty(PropertyString.Name, keycode);
                chest.SaveBiotaToDatabase();
            });
    }

    public static void Tick(double currentUnixTime)
    {
        if (ServerManager.ShutdownInProgress)
            return;

        var bankLb = LandblockManager.GetLandblock(Constants.BANK_CONTAINER_LOCATION.LandblockId, false, true);
        if (bankLb.CreateWorldObjectsCompleted && bankLb.GetObject(BankContainer.Guid, false) == null)
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

    public static bool TryAddToInventory(WorldObject item, uint accountId)
    {
        if (item.UseBackpackSlot)
            return HandleAddToInventory(item, accountId);

        var containerItems = BankContainer.Inventory.Values
            .Where(item =>
            {
                var bankId = item.GetBankId();
                return bankId > 0 && bankId == accountId && !item.UseBackpackSlot;
            }).OrderByDescending(item => item.ItemType).ToList();

        if (containerItems.Count >= MAX_ITEM_CAPACITY)
        {
            Log($"Failed to add item with Name = {item.NameWithMaterial}, ItemId = {item.Guid.Full} to bank inventory, container has reached maximum capacity for owner AccountId = {accountId}", ModManager.LogLevel.Warn);
            return false;
        }

        return HandleAddToInventory(item, accountId);
    }

    private static bool HandleAddToInventory(WorldObject item, uint accountId)
    {
        var result = BankContainer.TryAddToInventory(item);

        if (result)
        {
            item.SetProperty(FakeIID.BankId, accountId);
            Log($"Successfully added item Name = {item.NameWithMaterial}, ItemId = {item.Guid.Full} to playerBank AccountId = {accountId}");
        }

        return result;
    }

    public static bool TryRemoveFromInventory(WorldObject item, uint accountId) 
    {
        if (!BankContainer.Inventory.ContainsKey(item.Guid))
        {
            Log($"Failed to remove item with Name = {item.NameWithMaterial}, ItemId = {item.Guid.Full} from bank inventory for playerBank AccountId = {accountId} because item does not exist for item Name = {item.Name}, Id = {item.Guid.Full}", ModManager.LogLevel.Warn);
            return false;
        }

        var result = BankContainer.TryRemoveFromInventory(item.Guid);

        if (result)
        {
            item.RemoveProperty(FakeIID.BankId);
            Log($"Successfully removed item Name = {item.NameWithMaterial}, ItemId = {item.Guid.Full} from playerBank AccountId = {accountId}", ModManager.LogLevel.Warn);
        }

        return result;
    }
}
