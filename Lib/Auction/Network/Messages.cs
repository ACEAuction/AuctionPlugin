using ACE.Server.Network.GameMessages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACE.Mods.Legend.Lib.Auction.Network;
public enum AuctionGameMessageOpcode : uint
{
    AuctionQueryListingsResponse = 0x0317, 
}

public class CustomGameMessage : GameMessage
{
    public CustomGameMessage(GameMessageOpcode opcode, GameMessageGroup group)
        : base(opcode, group) {
    }

    public void WriteJson<T>(T data)
    {
        string jsonString = JsonSerializer.Serialize(data);
        var length = jsonString.Length;
        Writer.Write(length);
        Writer.Write(Encoding.UTF8.GetBytes(jsonString));
    }
}

public class GameMessageSendAuctionListings : CustomGameMessage
{
    public GameMessageSendAuctionListings(List<AuctionItem> items)
        : base((GameMessageOpcode)AuctionGameMessageOpcode.AuctionQueryListingsResponse, GameMessageGroup.UIQueue)
    {
        WriteJson(items);
    }
}

