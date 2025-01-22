using ACE.Database;
using ACE.Entity.Models;
using ACE.Mods.Legend.Lib.Auction.Models;
using ACE.Mods.Legend.Lib.Common.Errors;
using ACE.Mods.Legend.Lib.Database.Models;
using ACE.Server.Network.Enum;
using ACE.Server.Network.GameMessages;

namespace ACE.Mods.Legend.Lib.Auction.Network;


public static class GameMessageCreateSellOrderRequest
{

    [GameMessage((GameMessageOpcode)AuctionGameMessageOpcode.CreateSellOrderRequest, SessionState.WorldConnected)]
    public static void Handle(ClientMessage clientMessage, Session session)
    {
        try
        {
            var opcode = clientMessage.Opcode;
            var request = clientMessage.ReadJson<SellOrderRequest>();

            if (request == null || request.Data == null)
                throw new AuctionFailure("The Sell order request data is invalid!", FailureCode.Auction.SellValidation);

            request.Data.Validate();

            var sellOrder = session.Player.CreateAuctionSellOrder(request: request.Data);
            var successResponse = new JsonResponse<AuctionSellOrder>(data: sellOrder);
            session.Network.EnqueueSend(new GameMessageCreateSellOrderResponse(successResponse));
        }
        catch (AuctionFailure ex)
        {
            ModManager.Log(ex.ToString(), ModManager.LogLevel.Error);
            var response = new JsonResponse<AuctionSellOrder>(data: null, success: false, errorCode: (int)ex.Code, ex.Message);
            session.Network.EnqueueSend(new GameMessageCreateSellOrderResponse(response));
        }
        catch (Exception ex)
        {
            ModManager.Log(ex.ToString(), ModManager.LogLevel.Error);
            var response = new JsonResponse<AuctionSellOrder>(data: null, success: false, errorCode: (int)FailureCode.Auction.Unknown, "Internal Server Error!");
            session.Network.EnqueueSend(new GameMessageCreateSellOrderResponse(response));
        }
    }
}

public static class GameMessageGetListingsRequest
{
    static Settings Settings => PatchClass.Settings;

    [GameMessage((GameMessageOpcode)AuctionGameMessageOpcode.GetListingsRequest, SessionState.WorldConnected)]
    public static void Handle(ClientMessage clientMessage, Session session)
    {
        try
        {
            var request = clientMessage.ReadJson<GetListingsRequest>();

            if (request == null || request.Data == null)
                throw new AuctionFailure("The get listings request data is invalid!", FailureCode.Auction.GetListingsRequest);

            request.Data.Validate();

            List<AuctionListing> listings = session.Player.GetAuctionListings(request.Data);

            var response = new JsonResponse<List<AuctionListing>>(data: listings);
            session.Network.EnqueueSend(new GameMessageGetListingsResponse(response));
        }
        catch (AuctionFailure ex)
        {
            ModManager.Log(ex.ToString(), ModManager.LogLevel.Error);
            var response = new JsonResponse<List<AuctionListing>>(data: null, success: false, errorCode: (int)ex.Code, ex.Message);
            session.Network.EnqueueSend(new GameMessageGetListingsResponse(response));
        }
        catch (Exception ex)
        {
            ModManager.Log(ex.ToString(), ModManager.LogLevel.Error);
            var response = new JsonResponse<List<AuctionListing>>(data: null, success: false, errorCode: (int)FailureCode.Auction.Unknown, "Internal Server Error!");
            session.Network.EnqueueSend(new GameMessageGetListingsResponse(response));
        }
    }
}
