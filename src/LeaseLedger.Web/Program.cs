using System.Text;
using System.Text.Json.Serialization;
using LeaseLedger.Infrastructure;
using LeaseLedger.Web.Api;
using LeaseLedger.Web.Components;
using LeaseLedger.Web.Identity;
using LeaseLedger.Web.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using LeaseLedger.Application;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("LeaseLedger")
    ?? throw new InvalidOperationException("Connection string 'LeaseLedger' is missing.");

builder.Services.AddLeaseLedgerInfrastructure(connectionString);
var stripeSecret = builder.Configuration["Stripe:SecretKey"];
if (!string.IsNullOrWhiteSpace(stripeSecret))
    builder.Services.AddScoped<IPaymentGateway>(_ => new StripePaymentGateway(stripeSecret));
builder.Services.AddScoped<IUserClaimsPrincipalFactory<AppUser>, AppUserClaimsPrincipalFactory>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddCascadingAuthenticationState();
builder.Services.Configure<AssistantOptions>(builder.Configuration.GetSection("Assistant"));
builder.Services.AddScoped<AssistantActionStore>();
builder.Services.AddScoped<AssistantToolRunner>();
builder.Services.AddHttpClient(nameof(LeaseLedgerAssistant), (serviceProvider, client) =>
{
    var assistantOptions = serviceProvider.GetRequiredService<IOptions<AssistantOptions>>().Value;
    var baseUrl = string.IsNullOrWhiteSpace(assistantOptions.BaseUrl)
        ? "https://api.openai.com/v1"
        : assistantOptions.BaseUrl.Trim().TrimEnd('/');
    client.BaseAddress = new Uri(baseUrl + "/");
    client.Timeout = TimeSpan.FromSeconds(45);
});
builder.Services.AddScoped<IAssistant>(serviceProvider =>
{
    var assistantHttp = serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(LeaseLedgerAssistant));
    return ActivatorUtilities.CreateInstance<LeaseLedgerAssistant>(serviceProvider, assistantHttp);
});
builder.Services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>();
builder.Services.AddScoped<IAuthClient, WebAuthClient>();
builder.Services.AddScoped<JwtTokenService>();

var signingKey = builder.Configuration["Jwt:SigningKey"]
    ?? throw new InvalidOperationException("Jwt:SigningKey is missing.");
builder.Services.AddAuthentication()
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            RoleClaimType = System.Security.Claims.ClaimTypes.Role,
            NameClaimType = System.Security.Claims.ClaimTypes.Name
        };
    });

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.Configure<HubOptions>(options =>
{
    options.MaximumReceiveMessageSize = 10 * 1024 * 1024;
});

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var database = scope.ServiceProvider.GetRequiredService<LeaseLedgerDbContext>();
    await database.Database.MigrateAsync();
    if (app.Environment.IsDevelopment())
        await DemoSeed.SeedAsync(scope.ServiceProvider);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapLeaseLedgerApi();
app.MapStripeWebhook();
app.MapAccountEndpoints();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
