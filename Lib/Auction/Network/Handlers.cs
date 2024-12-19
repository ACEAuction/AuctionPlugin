using ACE.Server.Network.GameMessages;

namespace ACE.Mods.Legend.Lib.Auction.Network;


/*
 * public static class GameMessageAuctionQueryListings
{
    [GameMessage((GameMessageOpcode)ModdedGameMessageOpcode.AuctionQueryListingsRequest, SessionState.WorldConnected)]
    public static void Handle(ClientMessage clientMessage, Session session)
    {
        ModManager.Log($"WE HAVE DONE IT, OPCODE = {clientMessage.Opcode}");
        session.Player.SendAuctionMessage("AUCTION HOUSE HAS CALLED US");
        session.Network.EnqueueSend(new GameMessageSendAuctionListings(session.Player.Guid.Full));
    }
}
*/
