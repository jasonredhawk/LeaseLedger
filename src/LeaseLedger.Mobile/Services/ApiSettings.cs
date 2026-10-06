namespace LeaseLedger.Mobile.Services;

public static class ApiSettings
{
    public static string BaseUrl
    {
        get
        {
#if ANDROID
            return "https://10.0.2.2:7208/";
#else
            return "https://localhost:7208/";
#endif
        }
    }

    public static string UnreachableMessage =>
        $"Can't reach LeaseLedger at {BaseUrl}. Start the website and try again.";
}
