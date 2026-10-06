using LeaseLedger.Application;
using Stripe;
using Stripe.Checkout;

namespace LeaseLedger.Web.Services;

public sealed class StripePaymentGateway : IPaymentGateway
{
    public const string IntegrationIdentifier = "leaseledger-bhqwnrxk";
    private readonly StripeClient client;

    public StripePaymentGateway(string secretKey)
    {
        client = new StripeClient(new StripeClientOptions
        {
            ApiKey = secretKey
        });
    }

    public bool IsConfigured => true;

    public async Task<string?> CreateCheckoutAsync(GatewayCheckout request, CancellationToken cancellationToken = default)
    {
        var options = new SessionCreateOptions
        {
            Mode = request.Kind == "plan" ? "subscription" : "payment",
            SuccessUrl = request.SuccessUrl,
            CancelUrl = request.CancelUrl,
            ClientReferenceId = request.TenancyId?.ToString() ?? request.LandlordUserId.ToString(),
            IntegrationIdentifier = IntegrationIdentifier,
            Metadata = new Dictionary<string, string>
            {
                ["kind"] = request.Kind,
                ["landlord_user_id"] = request.LandlordUserId.ToString(),
                ["tenancy_id"] = request.TenancyId?.ToString() ?? ""
            },
            LineItems =
            [
                request.Kind == "plan"
                    ? new SessionLineItemOptions { Quantity = 1, Price = await NoticesPriceIdAsync(cancellationToken) }
                    : new SessionLineItemOptions
                    {
                        Quantity = 1,
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            Currency = "cad",
                            UnitAmount = request.AmountCents,
                            ProductData = new SessionLineItemPriceDataProductDataOptions { Name = request.Description }
                        }
                    }
            ]
        };

        var session = await client.V1.Checkout.Sessions.CreateAsync(options, null, cancellationToken);
        return session.Url;
    }

    public async Task<GatewayCheckoutStatus?> GetCheckoutAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var session = await client.V1.Checkout.Sessions.GetAsync(sessionId, null, null, cancellationToken);
        session.Metadata.TryGetValue("kind", out var kind);
        session.Metadata.TryGetValue("tenancy_id", out var tenancyId);
        session.Metadata.TryGetValue("landlord_user_id", out var landlordId);
        return new GatewayCheckoutStatus(
            session.PaymentStatus == "paid",
            session.AmountTotal ?? 0,
            kind,
            string.IsNullOrWhiteSpace(tenancyId) ? null : tenancyId,
            landlordId,
            session.CustomerId,
            session.SubscriptionId);
    }

    public async Task<string?> CreatePortalAsync(string customerId, string returnUrl, CancellationToken cancellationToken = default)
    {
        var session = await client.V1.BillingPortal.Sessions.CreateAsync(new Stripe.BillingPortal.SessionCreateOptions
        {
            Customer = customerId,
            ReturnUrl = returnUrl
        }, null, cancellationToken);
        return session.Url;
    }

    private async Task<string> NoticesPriceIdAsync(CancellationToken cancellationToken)
    {
        const string productName = "LeaseLedger notices";
        var products = await client.V1.Products.ListAsync(new ProductListOptions { Active = true, Limit = 100 }, null, cancellationToken);
        var product = products.Data.FirstOrDefault(item => item.Name == productName)
            ?? await client.V1.Products.CreateAsync(new ProductCreateOptions { Name = productName }, null, cancellationToken);

        var prices = await client.V1.Prices.ListAsync(new PriceListOptions { Product = product.Id, Active = true, Limit = 20 }, null, cancellationToken);
        var price = prices.Data.FirstOrDefault(item => item.Currency == "cad" && item.UnitAmount == 2900 && item.Recurring?.Interval == "month");
        price ??= await client.V1.Prices.CreateAsync(new PriceCreateOptions
        {
            Product = product.Id,
            Currency = "cad",
            UnitAmount = 2900,
            Recurring = new PriceRecurringOptions { Interval = "month" }
        }, null, cancellationToken);
        return price.Id;
    }
}
