using ACE.Mods.Legend.Lib.Database.Models;
using ACE.Server.Network.GameMessages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACE.Mods.Legend.Lib.Auction.Network;

public enum AuctionGameMessageOpcode : uint
{
    AuctionGetAllListings = 0x10000,
    AuctionSell = 0x10001
}

public class JsonResponse<T>
{
    public bool Success { get; set; }
    public int? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public T? Data { get; set; }

    public JsonResponse(T? data, bool success = true, int? errorCode = null, string? errorMessage = null)
    {
        Success = success;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage ?? string.Empty;
        Data = data;
    }
}

public class CustomGameMessage<T> : GameMessage
{
    public CustomGameMessage(GameMessageOpcode opcode, GameMessageGroup group)
        : base(opcode, group)
    {
    }

    public void WriteJson(JsonResponse<T> response)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(response, options);
        var length = jsonString.Length;
        Writer.Write(length);
        Writer.Write(Encoding.UTF8.GetBytes(jsonString));
    }
}

public class GameMessageAuctionGetAllListings : CustomGameMessage<List<AuctionItem>>
{
    public GameMessageAuctionGetAllListings(JsonResponse<List<AuctionItem>> response)
        : base((GameMessageOpcode)AuctionGameMessageOpcode.AuctionGetAllListings, GameMessageGroup.UIQueue)
    {
        WriteJson(response);
    }
}
public class GameMessageAuctionSell : CustomGameMessage<AuctionListing>
{
    public GameMessageAuctionSell(JsonResponse<AuctionListing> response)
        : base((GameMessageOpcode)AuctionGameMessageOpcode.AuctionSell, GameMessageGroup.UIQueue)
    {
        WriteJson(response);
    }
}
