using ACE.Mods.Auction.Lib.Auction;

namespace ACE.Mods.Auction
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