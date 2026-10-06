namespace LeaseLedger.Application;

public interface IPaymentGateway
{
    bool IsConfigured { get; }

    Task<string?> CreateCheckoutAsync(GatewayCheckout request, CancellationToken cancellationToken = default);

    Task<GatewayCheckoutStatus?> GetCheckoutAsync(string sessionId, CancellationToken cancellationToken = default);

    Task<string?> CreatePortalAsync(string customerId, string returnUrl, CancellationToken cancellationToken = default);
}

public sealed record GatewayCheckout(
    string Kind,
    Guid? TenancyId,
    Guid LandlordUserId,
    long AmountCents,
    string SuccessUrl,
    string CancelUrl,
    string Description);

public sealed record GatewayCheckoutStatus(
    bool Paid,
    long AmountCents,
    string? Kind,
    string? TenancyId,
    string? LandlordUserId,
    string? CustomerId,
    string? SubscriptionId);
