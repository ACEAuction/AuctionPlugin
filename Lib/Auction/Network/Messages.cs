﻿using ACE.Mods.Legend.Lib.Database.Models;
using ACE.Server.Network.GameMessages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACE.Mods.Legend.Lib.Auction.Network;

public enum AuctionGameMessageOpcode : uint
{
    GetListings = 0x10000,
    CreateSellOrderRequest = 0x10001,
    CreateSellOrderResponse = 0x10002
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

public class GameMessageGetListings : GameMessage
{
    public GameMessageGetListings(JsonResponse<List<AuctionListing>> response)
        : base((GameMessageOpcode)AuctionGameMessageOpcode.GetListings, GameMessageGroup.UIQueue)
    {
        this.WriteJson(response);
    }
}
public class GameMessageCreateSellOrderResponse : GameMessage
{
    public GameMessageCreateSellOrderResponse(JsonResponse<AuctionSellOrder> response)
        : base((GameMessageOpcode)AuctionGameMessageOpcode.CreateSellOrderResponse, GameMessageGroup.UIQueue)
    {
        this.WriteJson(response);
    }
}
