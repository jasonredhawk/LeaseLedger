namespace LeaseLedger.Mobile.Services;

public sealed class AuthTokenStore
{
    private const string Key = "leaseledger_token";

    public string? Token
    {
        get
        {
            var value = Preferences.Get(Key, "");
            return string.IsNullOrEmpty(value) ? null : value;
        }
        set
        {
            if (string.IsNullOrEmpty(value))
                Preferences.Remove(Key);
            else
                Preferences.Set(Key, value);
        }
    }
}
