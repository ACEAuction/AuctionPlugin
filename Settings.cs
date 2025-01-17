using ACE.Mods.Legend.Lib.Auction;

namespace ACE.Mods.Legend
{
    public class Settings
    {
        public bool IsDev { get; set; } = false;

        public List<string> AuctionManager { get; set; } = new()
        {
            nameof(AuctionPatches),
        };
    }
}