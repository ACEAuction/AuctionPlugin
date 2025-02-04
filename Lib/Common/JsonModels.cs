namespace ACE.Mods.Legend.Lib.Common;

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
