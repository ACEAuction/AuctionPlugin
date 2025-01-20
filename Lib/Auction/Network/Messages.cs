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
    AuctionSellRequest = 0x10001,
    AuctionSellResponse = 0x10002
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
public class JsonRequest<T>
{
    public T? Data { get; set; }

    public JsonRequest(T? data)
    {
        Data = data;
    }
}

public class GameMessageAuctionGetAllListings : GameMessage
{
    public GameMessageAuctionGetAllListings(JsonResponse<List<AuctionItem>> response)
        : base((GameMessageOpcode)AuctionGameMessageOpcode.AuctionGetAllListings, GameMessageGroup.UIQueue)
    {
        this.WriteJson(response);
    }
}
public class GameMessageAuctionSellResponse : GameMessage
{
    public GameMessageAuctionSellResponse(JsonResponse<AuctionSellOrder> response)
        : base((GameMessageOpcode)AuctionGameMessageOpcode.AuctionSellResponse, GameMessageGroup.UIQueue)
    {
        this.WriteJson(response);
    }
}
