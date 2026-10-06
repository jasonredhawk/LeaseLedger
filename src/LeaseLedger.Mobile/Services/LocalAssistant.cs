using LeaseLedger.Application;
using LeaseLedger.Shared;

namespace LeaseLedger.Mobile.Services;

public sealed class LocalAssistant(ILeaseLedgerClient client) : IAssistant
{
    public async Task<AssistantReply> AskAsync(AssistantRequest request, CancellationToken cancellationToken = default)
    {
        DashboardDto dashboard;
        try
        {
            dashboard = await client.GetDashboardAsync(cancellationToken);
        }
        catch (LeaseLedgerException exception)
        {
            return new AssistantReply(exception.Message, null, false, request.TenancyId, null, null, null);
        }

        if (MaintenanceChat.IsReport(request.Message))
        {
            try
            {
                var maintenance = await MaintenanceChat.ReplyAsync(client, request.Message, dashboard.Tenancies, request.TenancyId, cancellationToken);
                if (maintenance.LedgerChanged)
                    return maintenance;

                return AssistantContext.Enrich(maintenance, request.Message, dashboard.Tenancies, new Dictionary<Guid, Guid>(), DateTime.Now.Year);
            }
            catch (LeaseLedgerException exception)
            {
                return new AssistantReply(exception.Message, null, false, request.TenancyId, null, null, null);
            }
        }

        var result = AgentNavigator.Interpret(request.Message, dashboard.Tenancies, request.TenancyId);
        AssistantReply reply;
        if (result.Inspections && result.TenancyId is Guid tenancyId)
        {
            try
            {
                var detail = await client.GetTenancyAsync(tenancyId, cancellationToken);
                if (detail is null || !AssistantPages.TryOpen("inspections", new AssistantRouteArgs(null, detail.UnitId, null, null, null), out var opened))
                    reply = new AssistantReply("That tenancy was not found.", null, false, result.TenancyId, null, null, null);
                else
                    reply = new AssistantReply(result.Reply, opened.Href, false, result.TenancyId, opened.Label, null, null);
            }
            catch (LeaseLedgerException exception)
            {
                reply = new AssistantReply(exception.Message, null, false, result.TenancyId, null, null, null);
            }
        }
        else
        {
            reply = new AssistantReply(result.Reply, result.Href, result.External, result.TenancyId, result.LinkLabel, null, null);
        }

        return AssistantContext.Enrich(reply, request.Message, dashboard.Tenancies, new Dictionary<Guid, Guid>(), DateTime.Now.Year);
    }

    public Task<AssistantReply> ConfirmAsync(Guid actionId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new AssistantReply("Ask again, then confirm from the website.", null, false, null, null, null, null));
}
