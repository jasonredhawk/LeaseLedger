using LeaseLedger.Application;
using LeaseLedger.Mobile.Services;
using Microsoft.Extensions.Logging;

namespace LeaseLedger.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddSingleton<AuthTokenStore>();
        builder.Services.AddTransient<AuthHeaderHandler>();

        builder.Services.AddHttpClient<ILeaseLedgerClient, HttpLeaseLedgerClient>(client =>
            {
                client.BaseAddress = new Uri(ApiSettings.BaseUrl);
            })
            .ConfigurePrimaryHttpMessageHandler(CreateHandler)
            .AddHttpMessageHandler<AuthHeaderHandler>();

        builder.Services.AddHttpClient<IAuthClient, HttpAuthClient>(client =>
            {
                client.BaseAddress = new Uri(ApiSettings.BaseUrl);
            })
            .ConfigurePrimaryHttpMessageHandler(CreateHandler);

        builder.Services.AddScoped<IAssistant, LocalAssistant>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }

    private static HttpMessageHandler CreateHandler()
    {
        var handler = new HttpClientHandler();
#if DEBUG
        handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
#endif
        return handler;
    }
}
