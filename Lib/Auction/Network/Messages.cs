using ACE.Mods.Legend.Lib.Common;
using ACE.Mods.Legend.Lib.Database.Models;
using ACE.Server.Network.GameMessages;

namespace ACE.Mods.Legend.Lib.Auction.Network;

public enum AuctionGameMessageOpcode : uint
{
    CreateSellOrderRequest = 0x10001,
    CreateSellOrderResponse = 0x10002,
    GetPostListingsRequest = 0x10003,
    GetPostListingsResponse = 0x10004,
    GetInboxItemsRequest = 0x10005,
    GetInboxItemsResponse = 0x10006,
    InboxNotificationResponse = 0x10007,
    CollectInboxItemsRequest = 0x10008,
    CollectInboxItemsResponse = 0x10008,
}
public class GameMessageCollectInboxItemResponse : GameMessage
{
    public GameMessageCollectInboxItemResponse(JsonResponse<object> response)
        : base((GameMessageOpcode)AuctionGameMessageOpcode.CollectInboxItemsResponse, GameMessageGroup.UIQueue)
    {
        this.WriteJson(response);
    }
}

public class GameMessageInboxNotificationResponse : GameMessage
{
    public GameMessageInboxNotificationResponse(JsonResponse<object> response)
        : base((GameMessageOpcode)AuctionGameMessageOpcode.InboxNotificationResponse, GameMessageGroup.UIQueue)
    {
        this.WriteJson(response);
    }
}

public class GameMessageGetInboxItemsResponse : GameMessage
{
    public GameMessageGetInboxItemsResponse(JsonResponse<List<MailItem>> response)
        : base((GameMessageOpcode)AuctionGameMessageOpcode.GetInboxItemsResponse, GameMessageGroup.UIQueue)
    {
        this.WriteJson(response);
    }
}

public class GameMessageGetPostListingsResponse : GameMessage
{
    public GameMessageGetPostListingsResponse(JsonResponse<List<AuctionListing>> response)
        : base((GameMessageOpcode)AuctionGameMessageOpcode.GetPostListingsResponse, GameMessageGroup.UIQueue)
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
