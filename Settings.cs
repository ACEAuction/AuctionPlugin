using ACE.Mods.Legend.Lib.CustomContainer;
using ACE.Mods.Legend.Lib.Bank;
using ACE.Mods.Legend.Lib.Auction;

namespace ACE.Mods.Legend
{
    public class Settings
    {
        public bool IsDev { get; set; } = false;
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