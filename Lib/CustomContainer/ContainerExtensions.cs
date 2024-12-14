using ACE.Mods.Legend.Lib.Auction;
using ACE.Mods.Legend.Lib.Bank;
using ACE.Mods.Legend.Lib.Common;


namespace ACE.Mods.Legend.Lib.CustomContainer;

public static class ContainerExtensions
{
    public static int GetContainerCapacity(this Container item) =>
      item.GetProperty(Entity.Enum.Properties.PropertyInt.ContainersCapacity) ?? 0;
    public static int GetItemsCapacity(this WorldObject item) => item.GetProperty(Entity.Enum.Properties.PropertyInt.ItemsCapacity) ?? 0;
    public static bool IsMaxItemCapacity(this Container item) =>
        item.GetItemsCapacity() <= item.Inventory.Values.Where(i => !i.UseBackpackSlot).Count(); 

    public static bool IsCustomContainer(this Container container) =>
        container.Name == Constants.BANK_CONTAINER_KEYCODE ||
        container.Name == Constants.AUCTION_ITEMS_CONTAINER_KEYCODE ||
        container.Name == Constants.AUCTION_LISTINGS_CONTAINER_KEYCODE;

    public static object GetCustomContainerLock(this Container container)
    {
        if (container.Name == Constants.BANK_CONTAINER_KEYCODE)
        {
            return BankManager.BankLock;
        }

        if (container.Name == Constants.AUCTION_ITEMS_CONTAINER_KEYCODE)
        {
            return AuctionManager.AuctionItemsLock;
        }

        if (container.Name == Constants.AUCTION_LISTINGS_CONTAINER_KEYCODE)
        {
            return AuctionManager.AuctionItemsLock;
        }

        throw new Exception("Failed to find custom container lock");
    }
}
