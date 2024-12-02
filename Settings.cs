using ACE.Mods.AuctionHouse.Lib.Managers;

namespace ACE.Mods.AuctionHouse
{
    public class Settings
    {
        public List<string> ContainerManager{ get; set; } = new()
        {
            nameof(ContainerPatches),
        };

        public List<string> AuctionManager { get; set; } = new()
        {
            nameof(AuctionPatches),
        };
    }
}