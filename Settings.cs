using ACE.Mods.Legend.Lib.Auction;
using ACE.Mods.Legend.Lib.Container;
using ACE.Mods.Legend.Lib.Bank;

namespace ACE.Mods.Legend
{
    public class Settings
    {
        public List<string> ContainerFactory{ get; set; } = new()
        {
            nameof(ContainerPatches),
        };

        public List<string> AuctionManager { get; set; } = new()
        {
            nameof(AuctionPatches),
        };

        public List<string> BankManager { get; set; } = new()
        {
            nameof(BankPatches),
        };

    }
}