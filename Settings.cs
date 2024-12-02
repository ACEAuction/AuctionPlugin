using ACE.Mods.Legend.Lib.Auction;
using ACE.Mods.Legend.Lib.Common;

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
    }
}