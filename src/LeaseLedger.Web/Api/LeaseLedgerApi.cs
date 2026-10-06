using LeaseLedger.Application;
using Stripe;
using LeaseLedger.Infrastructure;
using LeaseLedger.Web.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace LeaseLedger.Web.Api;

public static class LeaseLedgerApi
{
    public static void MapLeaseLedgerApi(this WebApplication app)
    {
        var open = app.MapGroup("/api").AddEndpointFilter<ApiExceptionFilter>();
        open.MapPost("/auth/login", Login);
        open.MapPost("/auth/register", Register);

        var api = app.MapGroup("/api")
            .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme })
            .AddEndpointFilter<ApiExceptionFilter>();

        api.MapGet("/me", (ILeaseLedgerClient client, CancellationToken ct) => client.GetCurrentUserAsync(ct));
        api.MapGet("/agent-history", (ILeaseLedgerClient client, CancellationToken ct) => client.GetAgentHistoryAsync(ct));
        api.MapPut("/agent-history", async (AgentHistoryDto history, ILeaseLedgerClient client, CancellationToken ct) =>
        {
            await client.SaveAgentHistoryAsync(history, ct);
            return Results.NoContent();
        });
        api.MapGet("/dashboard", (ILeaseLedgerClient client, CancellationToken ct) => client.GetDashboardAsync(ct));
        api.MapGet("/properties", (ILeaseLedgerClient client, CancellationToken ct) => client.GetPropertiesAsync(ct));
        api.MapPost("/properties", (CreatePropertyRequest request, ILeaseLedgerClient client, CancellationToken ct) => client.CreatePropertyAsync(request, ct));
        api.MapPost("/properties/{propertyId:guid}/units", (Guid propertyId, CreateUnitRequest request, ILeaseLedgerClient client, CancellationToken ct) => client.AddUnitAsync(propertyId, request, ct));
        api.MapPost("/units/{unitId:guid}/tenancies", (Guid unitId, CreateTenancyRequest request, ILeaseLedgerClient client, CancellationToken ct) => client.CreateTenancyAsync(unitId, request, ct));
        api.MapGet("/tenancies/{tenancyId:guid}", async (Guid tenancyId, ILeaseLedgerClient client, CancellationToken ct) =>
            await client.GetTenancyAsync(tenancyId, ct) is { } tenancy ? Results.Ok(tenancy) : Results.NotFound());
        api.MapPost("/tenancies/{tenancyId:guid}/charges", async (Guid tenancyId, RecordChargeRequest request, ILeaseLedgerClient client, CancellationToken ct) =>
        {
            await client.RecordChargeAsync(tenancyId, request, ct);
            return Results.NoContent();
        });
        api.MapPost("/tenancies/{tenancyId:guid}/payments", (Guid tenancyId, RecordPaymentRequest request, ILeaseLedgerClient client, CancellationToken ct) => client.RecordPaymentAsync(tenancyId, request, ct));
        api.MapGet("/receipts/{receiptNumber}", async (string receiptNumber, ILeaseLedgerClient client, CancellationToken ct) =>
            await client.GetReceiptAsync(receiptNumber, ct) is { } receipt ? Results.Ok(receipt) : Results.NotFound());
        api.MapGet("/maintenance", (ILeaseLedgerClient client, CancellationToken ct) => client.GetMaintenanceAsync(ct));
        api.MapPost("/maintenance", (CreateMaintenanceRequest request, ILeaseLedgerClient client, CancellationToken ct) => client.CreateMaintenanceAsync(request, ct));
        api.MapPost("/maintenance/{requestId:guid}/status", (Guid requestId, UpdateMaintenanceStatusRequest request, ILeaseLedgerClient client, CancellationToken ct) => client.UpdateMaintenanceStatusAsync(requestId, request, ct));
        api.MapPost("/tenancies/{tenancyId:guid}/n1", (Guid tenancyId, N1PreviewRequest request, ILeaseLedgerClient client, CancellationToken ct) => client.PreviewN1Async(tenancyId, request, ct));
        api.MapPost("/tenancies/{tenancyId:guid}/n2", (Guid tenancyId, N2PreviewRequest request, ILeaseLedgerClient client, CancellationToken ct) => client.PreviewN2Async(tenancyId, request, ct));
        api.MapPost("/tenancies/{tenancyId:guid}/n4", (Guid tenancyId, N4PreviewRequest request, ILeaseLedgerClient client, CancellationToken ct) => client.PreviewN4Async(tenancyId, request, ct));
        api.MapPost("/tenancies/{tenancyId:guid}/n1/save", (Guid tenancyId, N1PreviewRequest request, ILeaseLedgerClient client, CancellationToken ct) => client.SaveN1Async(tenancyId, request, ct));
        api.MapPost("/tenancies/{tenancyId:guid}/n2/save", (Guid tenancyId, N2PreviewRequest request, ILeaseLedgerClient client, CancellationToken ct) => client.SaveN2Async(tenancyId, request, ct));
        api.MapPost("/tenancies/{tenancyId:guid}/n4/save", (Guid tenancyId, N4PreviewRequest request, ILeaseLedgerClient client, CancellationToken ct) => client.SaveN4Async(tenancyId, request, ct));
        api.MapGet("/tenancies/{tenancyId:guid}/notices", (Guid tenancyId, ILeaseLedgerClient client, CancellationToken ct) => client.GetNoticesAsync(tenancyId, ct));
        api.MapGet("/tenancies/{tenancyId:guid}/year/{year:int}", async (Guid tenancyId, int year, ILeaseLedgerClient client, CancellationToken ct) =>
            await client.GetYearEndAsync(tenancyId, year, ct) is { } yearEnd ? Results.Ok(yearEnd) : Results.NotFound());
        api.MapGet("/units/{unitId:guid}/inspections", async (Guid unitId, ILeaseLedgerClient client, CancellationToken ct) =>
            await client.GetInspectionsAsync(unitId, ct) is { } log ? Results.Ok(log) : Results.NotFound());
        api.MapPost("/units/{unitId:guid}/inspections", (Guid unitId, RecordInspectionRequest request, ILeaseLedgerClient client, CancellationToken ct) => client.RecordInspectionAsync(unitId, request, ct));
        api.MapPost("/tenancies/{tenancyId:guid}/n3", (Guid tenancyId, N3PreviewRequest request, ILeaseLedgerClient client, CancellationToken ct) => client.PreviewN3Async(tenancyId, request, ct));
        api.MapPost("/tenancies/{tenancyId:guid}/n5", (Guid tenancyId, N5PreviewRequest request, ILeaseLedgerClient client, CancellationToken ct) => client.PreviewN5Async(tenancyId, request, ct));
        api.MapPost("/tenancies/{tenancyId:guid}/n6", (Guid tenancyId, N6PreviewRequest request, ILeaseLedgerClient client, CancellationToken ct) => client.PreviewN6Async(tenancyId, request, ct));
        api.MapPost("/tenancies/{tenancyId:guid}/n7", (Guid tenancyId, N7PreviewRequest request, ILeaseLedgerClient client, CancellationToken ct) => client.PreviewN7Async(tenancyId, request, ct));
        api.MapPost("/tenancies/{tenancyId:guid}/n3/save", (Guid tenancyId, N3PreviewRequest request, ILeaseLedgerClient client, CancellationToken ct) => client.SaveN3Async(tenancyId, request, ct));
        api.MapPost("/tenancies/{tenancyId:guid}/n5/save", (Guid tenancyId, N5PreviewRequest request, ILeaseLedgerClient client, CancellationToken ct) => client.SaveN5Async(tenancyId, request, ct));
        api.MapPost("/tenancies/{tenancyId:guid}/n6/save", (Guid tenancyId, N6PreviewRequest request, ILeaseLedgerClient client, CancellationToken ct) => client.SaveN6Async(tenancyId, request, ct));
        api.MapPost("/tenancies/{tenancyId:guid}/n7/save", (Guid tenancyId, N7PreviewRequest request, ILeaseLedgerClient client, CancellationToken ct) => client.SaveN7Async(tenancyId, request, ct));
        api.MapPost("/notices/{noticeId:guid}/service", async (Guid noticeId, RecordServiceRequest request, ILeaseLedgerClient client, CancellationToken ct) =>
        {
            await client.RecordNoticeServiceAsync(noticeId, request, ct);
            return Results.NoContent();
        });
        api.MapPost("/notices/{noticeId:guid}/acknowledge", async (Guid noticeId, AcknowledgeNoticeRequest request, ILeaseLedgerClient client, CancellationToken ct) =>
        {
            await client.AcknowledgeNoticeAsync(noticeId, request, ct);
            return Results.NoContent();
        });
        api.MapPost("/notices/{noticeId:guid}/refer/{contactId:guid}", async (Guid noticeId, Guid contactId, ILeaseLedgerClient client, CancellationToken ct) =>
        {
            await client.ReferNoticeAsync(noticeId, contactId, ct);
            return Results.NoContent();
        });
        api.MapPost("/maintenance/{requestId:guid}/work-order", (Guid requestId, AssignWorkOrderRequest request, ILeaseLedgerClient client, CancellationToken ct) => client.AssignWorkOrderAsync(requestId, request, ct));
        api.MapGet("/tenancies/{tenancyId:guid}/insurance", async (Guid tenancyId, ILeaseLedgerClient client, CancellationToken ct) =>
            await client.GetInsuranceAsync(tenancyId, ct) is { } record ? Results.Ok(record) : Results.NotFound());
        api.MapPost("/tenancies/{tenancyId:guid}/insurance", (Guid tenancyId, SaveInsuranceRequest request, ILeaseLedgerClient client, CancellationToken ct) => client.SaveInsuranceAsync(tenancyId, request, ct));
        api.MapGet("/legal-contacts", (ILeaseLedgerClient client, CancellationToken ct) => client.GetLegalContactsAsync(ct));
        api.MapPost("/legal-contacts", (SaveLegalContactRequest request, ILeaseLedgerClient client, CancellationToken ct) => client.SaveLegalContactAsync(request, ct));
        api.MapGet("/tenancies/{tenancyId:guid}/payment-instructions", async (Guid tenancyId, ILeaseLedgerClient client, CancellationToken ct) =>
            await client.GetPaymentInstructionsAsync(tenancyId, ct) is { } instructions ? Results.Ok(instructions) : Results.NotFound());
        api.MapPost("/tenancies/{tenancyId:guid}/pad", async (Guid tenancyId, AcknowledgeNoticeRequest request, ILeaseLedgerClient client, CancellationToken ct) =>
        {
            await client.AuthorizePadAsync(tenancyId, request, ct);
            return Results.NoContent();
        });
        api.MapPost("/checkout", (StartCheckoutRequest request, ILeaseLedgerClient client, CancellationToken ct) => client.StartCheckoutAsync(request, ct));
        api.MapPost("/checkout/confirm", (string sessionId, ILeaseLedgerClient client, CancellationToken ct) => client.ConfirmCheckoutAsync(sessionId, ct));
        api.MapGet("/plan", (ILeaseLedgerClient client, CancellationToken ct) => client.GetPlanAsync(ct));
        api.MapGet("/pdf-templates", (ILeaseLedgerClient client, CancellationToken ct) => client.GetPdfTemplatesAsync(ct));
        api.MapPost("/pdf-templates", (UploadPdfTemplateRequest request, ILeaseLedgerClient client, CancellationToken ct) => client.UploadPdfTemplateAsync(request, ct));
        api.MapGet("/pdf-templates/{templateId:guid}", async (Guid templateId, ILeaseLedgerClient client, CancellationToken ct) =>
            await client.GetPdfTemplateAsync(templateId, ct) is { } template ? Results.Ok(template) : Results.NotFound());
        api.MapGet("/pdf-templates/{templateId:guid}/file", async (Guid templateId, ILeaseLedgerClient client, CancellationToken ct) =>
            await client.GetPdfTemplateFileAsync(templateId, ct) is { } file ? Results.Ok(file) : Results.NotFound());
        api.MapPut("/pdf-templates/{templateId:guid}", (Guid templateId, UpdatePdfTemplateRequest request, ILeaseLedgerClient client, CancellationToken ct) => client.UpdatePdfTemplateAsync(templateId, request, ct));
        api.MapDelete("/pdf-templates/{templateId:guid}", async (Guid templateId, ILeaseLedgerClient client, CancellationToken ct) =>
        {
            await client.DeletePdfTemplateAsync(templateId, ct);
            return Results.NoContent();
        });
        api.MapPost("/pdf-templates/{templateId:guid}/fill/{tenancyId:guid}", (Guid templateId, Guid tenancyId, ILeaseLedgerClient client, CancellationToken ct) => client.FillPdfTemplateAsync(templateId, tenancyId, ct));
    }

    public static void MapStripeWebhook(this WebApplication app)
    {
        app.MapPost("/api/stripe/webhook", async (HttpRequest request, IConfiguration config, LeaseLedgerService ledger, CancellationToken ct) =>
        {
            var secret = config["Stripe:WebhookSecret"];
            if (string.IsNullOrWhiteSpace(secret))
                return Results.Ok();

            var json = await new StreamReader(request.Body).ReadToEndAsync(ct);
            try
            {
                var stripeEvent = EventUtility.ConstructEvent(json, request.Headers["Stripe-Signature"], secret);
                if (stripeEvent.Data.Object is Stripe.Checkout.Session session && stripeEvent.Type == "checkout.session.completed")
                    await ledger.FulfillCheckoutAsync(session.Id, ct);
            }
            catch (StripeException)
            {
                return Results.BadRequest();
            }

            return Results.Ok();
        }).DisableAntiforgery();
    }

    public static void MapAccountEndpoints(this WebApplication app)
    {
        app.MapGet("/account/logout", async (SignInManager<AppUser> signIn) =>
        {
            await signIn.SignOutAsync();
            return Results.Redirect("/login");
        });

        if (!app.Environment.IsDevelopment())
            return;

        app.MapGet("/account/demo-login", async (string role, UserManager<AppUser> users, SignInManager<AppUser> signIn) =>
        {
            var email = string.Equals(role, "tenant", StringComparison.OrdinalIgnoreCase)
                ? DemoSeed.TenantEmail
                : DemoSeed.LandlordEmail;
            var user = await users.FindByEmailAsync(email);
            if (user is null)
                return Results.Redirect("/login");

            await signIn.SignInAsync(user, isPersistent: true);
            return Results.Redirect("/");
        });
    }

    private static async Task<IResult> Login(LoginRequest request, IAccountGateway accounts, UserManager<AppUser> users, JwtTokenService tokens, CancellationToken cancellationToken)
    {
        var outcome = await accounts.ValidatePasswordAsync(request, cancellationToken);
        if (!outcome.Succeeded || outcome.UserId is null)
            return Results.Json(new ApiError(outcome.Error ?? "Sign-in failed."), statusCode: StatusCodes.Status400BadRequest);

        var user = await users.FindByIdAsync(outcome.UserId.Value.ToString());
        if (user is null)
            return Results.Json(new ApiError("Sign-in failed."), statusCode: StatusCodes.Status400BadRequest);

        return Results.Ok(new AccessTokenResponse(outcome.ToDto(), tokens.Create(user, outcome.Role!)));
    }

    private static async Task<IResult> Register(RegisterRequest request, IAccountGateway accounts, UserManager<AppUser> users, JwtTokenService tokens, CancellationToken cancellationToken)
    {
        var outcome = await accounts.RegisterAsync(request, cancellationToken);
        if (!outcome.Succeeded || outcome.UserId is null)
            return Results.Json(new ApiError(outcome.Error ?? "Registration failed."), statusCode: StatusCodes.Status400BadRequest);

        var user = await users.FindByIdAsync(outcome.UserId.Value.ToString());
        if (user is null)
            return Results.Json(new ApiError("Registration failed."), statusCode: StatusCodes.Status400BadRequest);

        return Results.Ok(new AccessTokenResponse(outcome.ToDto(), tokens.Create(user, outcome.Role!)));
    }
}

public sealed class ApiExceptionFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Unauthorized();
        }
        catch (LeaseLedgerException exception)
        {
            return Results.Json(new ApiError(exception.Message), statusCode: StatusCodes.Status400BadRequest);
        }
    }
}
