using ACE.Mods.Auction.Lib.Auction.Network.Models;
using ACE.Mods.Auction.Lib.Common;
using ACE.Mods.Auction.Lib.Common.Errors;
using ACE.Mods.Auction.Lib.Database.Models;
using ACE.Server.Network.Enum;
using ACE.Server.Network.GameMessages;

namespace ACE.Mods.Auction.Lib.Auction.Network;

public static class GameMessageCollectInboxItemsRequest
{
    [GameMessage((GameMessageOpcode)AuctionGameMessageOpcode.CollectInboxItemsRequest, SessionState.WorldConnected)]
    public static void Handle(ClientMessage clientMessage, Session session)
    {
        try
        {
            var request = clientMessage.ReadJson<CollectInboxItemsRequest>();

            if (request == null || request.Data == null)
                throw new AuctionFailure("Failed to collect inbox items, payload is invalid!", FailureCode.Auction.CollectInboxItemsRequest);

            AuctionManager.CollectAuctionInboxItems(session.Player, request.Data.InboxItems);
            var successResponse = new JsonResponse<object>(data: null);
            session.Network.EnqueueSend(new GameMessageCollectInboxItemResponse(successResponse));
        }
        catch (AuctionFailure ex)
        {
            ModManager.Log(ex.ToString(), ModManager.LogLevel.Error);
            var failureResponse = new JsonResponse<object>(data: null, success: false, errorCode: (int)ex.Code, ex.Message);
            session.Network.EnqueueSend(new GameMessageCollectInboxItemResponse(failureResponse));
        }
        catch (Exception ex)
        {
            ModManager.Log(ex.ToString(), ModManager.LogLevel.Error);
            var failureResponse = new JsonResponse<object>(data: null, success: false, errorCode: (int)FailureCode.Auction.Unknown, "Internal Server Error!");
            session.Network.EnqueueSend(new GameMessageCollectInboxItemResponse(failureResponse));
        }
    }
}

public static class GameMessageGetInboxItemsRequest
{ 
    [GameMessage((GameMessageOpcode)AuctionGameMessageOpcode.GetInboxItemsRequest, SessionState.WorldConnected)]
    public static void Handle(ClientMessage clientMessage, Session session)
    {
        try
        {
            var request = clientMessage.ReadJson<GetInboxItemsRequest>();

            if (request == null || request.Data == null)
                throw new AuctionFailure("Failed to get inbox items, payload is invalid!", FailureCode.Auction.GetInboxItemsRequest);

            request.Data.Validate();

            var inboxItems = AuctionManager.GetPendingMailItems(session.AccountId, request.Data.PageSize, request.Data.PageNumber);
            var successResponse = new JsonResponse<List<MailItem>>(data: inboxItems);
            session.Network.EnqueueSend(new GameMessageGetInboxItemsResponse(successResponse));
        }
        catch (AuctionFailure ex)
        {
            ModManager.Log(ex.ToString(), ModManager.LogLevel.Error);
            var failureResponse = new JsonResponse<List<MailItem>>(data: null, success: false, errorCode: (int)ex.Code, ex.Message);
            session.Network.EnqueueSend(new GameMessageGetInboxItemsResponse(failureResponse));
        }
        catch (Exception ex)
        {
            ModManager.Log(ex.ToString(), ModManager.LogLevel.Error);
            var failureResponse = new JsonResponse<List<MailItem>>(data: null, success: false, errorCode: (int)FailureCode.Auction.Unknown, "Internal Server Error!");
            session.Network.EnqueueSend(new GameMessageGetInboxItemsResponse(failureResponse));
        }
    }
}

public static class GameMessageCreateSellOrderRequest
{
    [GameMessage((GameMessageOpcode)AuctionGameMessageOpcode.CreateSellOrderRequest, SessionState.WorldConnected)]
    public static void Handle(ClientMessage clientMessage, Session session)
    {
        try
        {
            var request = clientMessage.ReadJson<CreateSellOrderRequest>();

            if (request == null || request.Data == null)
                throw new AuctionFailure("The Sell order request data is invalid!", FailureCode.Auction.SellValidation);

            request.Data.Validate();

            var sellOrder = AuctionManager.CreateAuctionSellOrder(session.Player, request: request.Data);
            var successResponse = new JsonResponse<AuctionSellOrder>(data: sellOrder);
            session.Network.EnqueueSend(new GameMessageCreateSellOrderResponse(successResponse));
        }
        catch (AuctionFailure ex)
        {
            ModManager.Log(ex.ToString(), ModManager.LogLevel.Error);
            var failureResponse = new JsonResponse<AuctionSellOrder>(data: null, success: false, errorCode: (int)ex.Code, ex.Message);
            session.Network.EnqueueSend(new GameMessageCreateSellOrderResponse(failureResponse));
        }
        catch (Exception ex)
        {
            ModManager.Log(ex.ToString(), ModManager.LogLevel.Error);
            var failureResponse = new JsonResponse<AuctionSellOrder>(data: null, success: false, errorCode: (int)FailureCode.Auction.Unknown, "Internal Server Error!");
            session.Network.EnqueueSend(new GameMessageCreateSellOrderResponse(failureResponse));
        }
    }
}

public static class GameMessageGetPostListingsRequest
{
    [GameMessage((GameMessageOpcode)AuctionGameMessageOpcode.GetPostListingsRequest, SessionState.WorldConnected)]
    public static void Handle(ClientMessage clientMessage, Session session)
    {
        try
        {
            var request = clientMessage.ReadJson<GetPostListingsRequest>();

            if (request == null || request.Data == null)
                throw new AuctionFailure("The get listings request data is invalid!", FailureCode.Auction.GetPostListingsRequest);

            request.Data.Validate();

            List<AuctionListing> listings = AuctionManager.GetPostAuctionListings(session.AccountId, request.Data);
            var failureResponse = new JsonResponse<List<AuctionListing>>(data: listings);
            session.Network.EnqueueSend(new GameMessageGetPostListingsResponse(failureResponse));
        }
        catch (AuctionFailure ex)
        {
            ModManager.Log(ex.ToString(), ModManager.LogLevel.Error);
            var failureResponse = new JsonResponse<List<AuctionListing>>(data: null, success: false, errorCode: (int)ex.Code, ex.Message);
            session.Network.EnqueueSend(new GameMessageGetPostListingsResponse(failureResponse));
        }
        catch (Exception ex)
        {
            ModManager.Log(ex.ToString(), ModManager.LogLevel.Error);
            var failureResponse = new JsonResponse<List<AuctionListing>>(data: null, success: false, errorCode: (int)FailureCode.Auction.Unknown, "Internal Server Error!");
            session.Network.EnqueueSend(new GameMessageGetPostListingsResponse(failureResponse));
        }
    }
}
