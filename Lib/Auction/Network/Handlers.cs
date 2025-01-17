using ACE.Mods.Legend.Lib.Auction.Models;
using ACE.Mods.Legend.Lib.Common.Errors;
using ACE.Mods.Legend.Lib.Database.Models;
using ACE.Server.Network.Enum;
using ACE.Server.Network.GameMessages;

namespace ACE.Mods.Legend.Lib.Auction.Network;


 public static class GameMessageAuctionSellRequest
{
    static Settings Settings => PatchClass.Settings;

    [GameMessage((GameMessageOpcode)AuctionGameMessageOpcode.AuctionSellRequest, SessionState.WorldConnected)]
    public static void Handle(ClientMessage clientMessage, Session session)
    {
        var opcode = clientMessage.Opcode;

        try
        {
            var request = clientMessage.ReadJson<AuctionSellRequest>();



            if (request == null || request.Data == null)
                throw new AuctionFailure("Failed to parse AuctionSellRequest data!", FailureCode.Auction.SellValidation);

            var auctionSellRequest = request.Data;

            // Use reflection to get all properties of AuctionSellRequest
            foreach (var property in auctionSellRequest.GetType().GetProperties())
            {
                // Print property name and value
                var propertyName = property.Name;
                var propertyValue = property.GetValue(auctionSellRequest);
                ModManager.Log($"{propertyName}: {propertyValue}");
            }

            request.Data.Validate();

            var hoursDuration = request.Data.HoursDuration;
            var startTime = DateTime.UtcNow;
            var endTime = Settings.IsDev ? startTime.AddSeconds(request.Data.HoursDuration) : startTime.AddHours(hoursDuration);

            var createAuctionSell = new CreateAuctionSell()
            {
                SellerId = session.AccountId,
                SellerName = session.Player.Name,
                ItemId = request.Data.ItemId,
                NumberOfStacks = request.Data.NumberOfStacks,
                StackSize = request.Data.StackSize,
                CurrencyType = request.Data.CurrencyType,
                StartPrice = request.Data.StartPrice,
                BuyoutPrice = request.Data.BuyoutPrice,
                StartTime = startTime,
                HoursDuration = hoursDuration,
                EndTime = endTime
            };

            var sellOrder = session.Player.PlaceAuctionSell(createAuctionSell);

            var successResponse = new JsonResponse<AuctionSellOrder>(data: sellOrder);
            session.Network.EnqueueSend(new GameMessageAuctionSellResponse(successResponse));
        }
        catch (AuctionFailure ex)
        {
            ModManager.Log(ex.ToString(), ModManager.LogLevel.Error);
            var response = new JsonResponse<AuctionSellOrder>(data: null, success: false, errorCode: (int)ex.Code, ex.Message);
            session.Network.EnqueueSend(new GameMessageAuctionSellResponse(response));
        }
        catch (Exception ex)
        {
            ModManager.Log(ex.ToString(), ModManager.LogLevel.Error);
            var response = new JsonResponse<AuctionSellOrder>(data: null, success: false, errorCode: (int)FailureCode.Auction.Unknown, "Internal Server Error!");
            session.Network.EnqueueSend(new GameMessageAuctionSellResponse(response));
        }
    }
}
