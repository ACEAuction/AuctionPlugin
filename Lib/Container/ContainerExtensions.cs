using ACE.Mods.Legend.Lib.Auction;
using ACE.Mods.Legend.Lib.Bank;
using ACE.Mods.Legend.Lib.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACE.Mods.Legend.Lib.Container;

public static class ContainerExtensions
{
    public static bool IsCustomContainer(this ACE.Server.WorldObjects.Container container)
    {
        return container.Name == Constants.BANK_CONTAINER_KEYCODE ||
            container.Name == Constants.AUCTION_ITEMS_CONTAINER_KEYCODE ||
            container.Name == Constants.AUCTION_LISTINGS_CONTAINER_KEYCODE;
    }

    public static object GetCustomContainerLock(this ACE.Server.WorldObjects.Container container)
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
