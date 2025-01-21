using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ACE.Server.Network.GameMessages;

namespace ACE.Mods.Legend.Lib.Auction.Network;

public static class NetworkingExtensions
{
    public static void WriteJson<T>(this GameMessage message, JsonResponse<T> response)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(response, options);
        var length = jsonString.Length;
        message.Writer.Write(length);
        message.Writer.Write(Encoding.UTF8.GetBytes(jsonString));
    }

    public static JsonRequest<T>? ReadJson<T>(this ClientMessage message)
    {
        try
        {
            int length = message.Payload.ReadInt32();

            var jsonString = message.Payload.ReadString();

            ModManager.Log($"Logging ReadJson() string payload");
            ModManager.Log(jsonString);

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<JsonRequest<T>>(jsonString, options);
        }
        catch (JsonException ex)
        {
            ModManager.Log($"JSON deserialization error: {ex.Message}", ModManager.LogLevel.Error);
            ModManager.Log($"Payload: {message.Payload}", ModManager.LogLevel.Error);

            return null;
        }
    }
}
}
