using ACE.Mods.Legend.Lib.Auction;
using ACE.Mods.Legend.Lib.Common;
using ACE.Mods.Legend.Lib.Mail;

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

        public List<string> MailManager { get; set; } = new()
        {
            nameof(MailPatches),
        };

    }
}