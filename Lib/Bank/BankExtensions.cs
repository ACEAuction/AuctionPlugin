using ACE.Mods.Legend.Lib.Common;
using ACE.Shared;

namespace ACE.Mods.Legend.Lib.Bank;

public static class BankExtensions
{
    public static uint GetBankId(this WorldObject item) =>
        item.GetProperty(FakeIID.BankId) ?? 0;

    public static bool IsBank(this Container item) => item.Name == Constants.BANK_CONTAINER_KEYCODE;

    public static int BankItemCount(this Player player) => player.GetProperty(FakeInt.BankItemCount) ?? 0;

    public static void ClearBank(this Player player)
    {
        var bankItems = BankManager.BankContainer.Inventory.Values
            .Where(item => item.GetBankId() == player.Account.AccountId)
            .ToList();

        foreach (var item in bankItems)
        {
            BankManager.TryRemoveFromInventory(item, player.Account.AccountId);
            item.Destroy();
        }
    }

}
