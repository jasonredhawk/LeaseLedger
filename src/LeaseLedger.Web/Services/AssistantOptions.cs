namespace LeaseLedger.Web.Services;

public sealed class AssistantOptions
{
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";

    public string Model { get; set; } = "gpt-4.1-mini";

    public string ApiKey { get; set; } = "";
}
