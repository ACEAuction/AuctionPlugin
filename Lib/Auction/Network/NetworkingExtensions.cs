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
        var stopwatch = Stopwatch.StartNew();  

        var options = new JsonSerializerOptions { WriteIndented = false };
        string jsonString = JsonSerializer.Serialize(response, options);

        byte[] jsonBytes = Encoding.UTF8.GetBytes(jsonString);

        message.Writer.Write(jsonBytes.Length);
        message.Writer.Write(jsonBytes);

        stopwatch.Stop();  

        if (jsonBytes.Length < 1000)
        {
            ModManager.Log("Logging WriteJson() string payload");
            ModManager.Log(jsonString);
        }
        else
        {
            ModManager.Log($"Payload too large to log. Size: {jsonBytes.Length} bytes");
        }

        ModManager.Log($"WriteJson took {stopwatch.ElapsedMilliseconds} ms");
    }

    public static JsonRequest<T>? ReadJson<T>(this ClientMessage message)
    {
        try
        {
            int length = message.Payload.ReadInt32();

            var jsonString = message.Payload.ReadString();

            ModManager.Log($"Logging ReadJson() string payload");
            ModManager.Log(jsonString);

            return JsonSerializer.Deserialize<JsonRequest<T>>(jsonString);
        }
        catch (JsonException ex)
        {
            ModManager.Log($"JSON deserialization error: {ex.Message}", ModManager.LogLevel.Error);

            return null;
        }
    }
}
