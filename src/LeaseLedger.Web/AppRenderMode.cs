using Microsoft.AspNetCore.Components.Web;

namespace LeaseLedger.Web;

public static class AppRenderMode
{
    public static InteractiveServerRenderMode Interactive { get; } = new(prerender: false);
}
