using LeaseLedger.Application;

namespace LeaseLedger.Infrastructure;

public sealed class UnconfiguredPaymentGateway : IPaymentGateway
{
    public bool IsConfigured => false;

    public Task<string?> CreateCheckoutAsync(GatewayCheckout request, CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(null);

    public Task<GatewayCheckoutStatus?> GetCheckoutAsync(string sessionId, CancellationToken cancellationToken = default) =>
        Task.FromResult<GatewayCheckoutStatus?>(null);

    public Task<string?> CreatePortalAsync(string customerId, string returnUrl, CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(null);
}
