namespace LeaseLedger.Application;

public static class AssistantProposals
{
    public static Func<ILeaseLedgerClient, CancellationToken, Task<AssistantCommitResult>> SaveN1(Guid tenancyId, N1PreviewRequest request) =>
        async (client, cancellationToken) =>
        {
            var saved = await client.SaveN1Async(tenancyId, request, cancellationToken);
            return Done($"Saved the N1. {saved.Summary}", "n1", tenancyId, Tenancy(tenancyId));
        };

    public static Func<ILeaseLedgerClient, CancellationToken, Task<AssistantCommitResult>> SaveN2(Guid tenancyId, N2PreviewRequest request) =>
        async (client, cancellationToken) =>
        {
            var saved = await client.SaveN2Async(tenancyId, request, cancellationToken);
            return Done($"Saved the N2. {saved.Summary}", "n2", tenancyId, Tenancy(tenancyId));
        };

    public static Func<ILeaseLedgerClient, CancellationToken, Task<AssistantCommitResult>> SaveN3(Guid tenancyId, N3PreviewRequest request) =>
        async (client, cancellationToken) =>
        {
            var saved = await client.SaveN3Async(tenancyId, request, cancellationToken);
            return Done($"Saved the N3. {saved.Summary}", "n3", tenancyId, Tenancy(tenancyId));
        };

    public static Func<ILeaseLedgerClient, CancellationToken, Task<AssistantCommitResult>> SaveN4(Guid tenancyId, N4PreviewRequest request) =>
        async (client, cancellationToken) =>
        {
            var saved = await client.SaveN4Async(tenancyId, request, cancellationToken);
            return Done($"Saved the N4. {saved.Summary}", "n4", tenancyId, Tenancy(tenancyId));
        };

    public static Func<ILeaseLedgerClient, CancellationToken, Task<AssistantCommitResult>> SaveN5(Guid tenancyId, N5PreviewRequest request) =>
        async (client, cancellationToken) =>
        {
            var saved = await client.SaveN5Async(tenancyId, request, cancellationToken);
            return Done($"Saved the N5. {saved.Summary}", "n5", tenancyId, Tenancy(tenancyId));
        };

    public static Func<ILeaseLedgerClient, CancellationToken, Task<AssistantCommitResult>> SaveN6(Guid tenancyId, N6PreviewRequest request) =>
        async (client, cancellationToken) =>
        {
            var saved = await client.SaveN6Async(tenancyId, request, cancellationToken);
            return Done($"Saved the N6. {saved.Summary}", "n6", tenancyId, Tenancy(tenancyId));
        };

    public static Func<ILeaseLedgerClient, CancellationToken, Task<AssistantCommitResult>> SaveN7(Guid tenancyId, N7PreviewRequest request) =>
        async (client, cancellationToken) =>
        {
            var saved = await client.SaveN7Async(tenancyId, request, cancellationToken);
            return Done($"Saved the N7. {saved.Summary}", "n7", tenancyId, Tenancy(tenancyId));
        };

    public static Func<ILeaseLedgerClient, CancellationToken, Task<AssistantCommitResult>> Payment(Guid tenancyId, RecordPaymentRequest request) =>
        async (client, cancellationToken) =>
        {
            var receipt = await client.RecordPaymentAsync(tenancyId, request, cancellationToken);
            var args = new AssistantRouteArgs(tenancyId, null, null, null, receipt.ReceiptNumber);
            if (!AssistantPages.TryOpen("receipt", args, out var link))
                return new AssistantCommitResult($"Recorded receipt {receipt.ReceiptNumber}.", null, null, tenancyId, null);

            return new AssistantCommitResult($"Recorded receipt {receipt.ReceiptNumber}.", link.Href, link.Label, tenancyId, null);
        };

    public static Func<ILeaseLedgerClient, CancellationToken, Task<AssistantCommitResult>> Charge(Guid tenancyId, RecordChargeRequest request) =>
        async (client, cancellationToken) =>
        {
            await client.RecordChargeAsync(tenancyId, request, cancellationToken);
            return Done("Recorded the charge.", "payments", tenancyId, Tenancy(tenancyId));
        };

    public static Func<ILeaseLedgerClient, CancellationToken, Task<AssistantCommitResult>> Maintenance(CreateMaintenanceRequest request) =>
        async (client, cancellationToken) =>
        {
            var created = await client.CreateMaintenanceAsync(request, cancellationToken);
            return Done($"Opened maintenance request {created.Title}.", "maintenance", request.TenancyId, new AssistantRouteArgs(null, null, null, null, null));
        };

    public static Func<ILeaseLedgerClient, CancellationToken, Task<AssistantCommitResult>> Insurance(Guid tenancyId, SaveInsuranceRequest request) =>
        async (client, cancellationToken) =>
        {
            await client.SaveInsuranceAsync(tenancyId, request, cancellationToken);
            return Done("Saved insurance.", "insurance", tenancyId, Tenancy(tenancyId));
        };

    public static Func<ILeaseLedgerClient, CancellationToken, Task<AssistantCommitResult>> Inspection(Guid unitId, RecordInspectionRequest request) =>
        async (client, cancellationToken) =>
        {
            await client.RecordInspectionAsync(unitId, request, cancellationToken);
            return Done("Recorded the inspection.", "inspections", null, new AssistantRouteArgs(null, unitId, null, null, null));
        };

    public static Func<ILeaseLedgerClient, CancellationToken, Task<AssistantCommitResult>> FillPdf(Guid templateId, Guid tenancyId) =>
        async (client, cancellationToken) =>
        {
            var file = await client.FillPdfTemplateAsync(templateId, tenancyId, cancellationToken);
            var download = new AssistantDownload(file.FileName, file.Content);
            return Done($"Filled {file.FileName}.", "form", tenancyId, new AssistantRouteArgs(null, null, null, templateId, null), download);
        };

    private static AssistantRouteArgs Tenancy(Guid tenancyId) => new(tenancyId, null, null, null, null);

    private static AssistantCommitResult Done(string text, string page, Guid? tenancyId, AssistantRouteArgs args, AssistantDownload? download = null)
    {
        if (!AssistantPages.TryOpen(page, args, out var link))
            return new AssistantCommitResult(text, null, null, tenancyId, download);

        return new AssistantCommitResult(text, link.Href, link.Label, tenancyId ?? link.TenancyId, download);
    }
}
