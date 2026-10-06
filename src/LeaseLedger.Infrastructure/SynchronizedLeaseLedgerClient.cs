using LeaseLedger.Application;

namespace LeaseLedger.Infrastructure;

public sealed class SynchronizedLeaseLedgerClient(LeaseLedgerService inner) : ILeaseLedgerClient
{
    private readonly SemaphoreSlim gate = new(1, 1);

    public Task<CurrentUserDto?> GetCurrentUserAsync(CancellationToken cancellationToken = default) => Run(() => inner.GetCurrentUserAsync(cancellationToken));
    public Task<DashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default) => Run(() => inner.GetDashboardAsync(cancellationToken));
    public Task<IReadOnlyList<PropertyDto>> GetPropertiesAsync(CancellationToken cancellationToken = default) => Run(() => inner.GetPropertiesAsync(cancellationToken));
    public Task<PropertyDto> CreatePropertyAsync(CreatePropertyRequest request, CancellationToken cancellationToken = default) => Run(() => inner.CreatePropertyAsync(request, cancellationToken));
    public Task<UnitDto> AddUnitAsync(Guid propertyId, CreateUnitRequest request, CancellationToken cancellationToken = default) => Run(() => inner.AddUnitAsync(propertyId, request, cancellationToken));
    public Task<TenancyDto> CreateTenancyAsync(Guid unitId, CreateTenancyRequest request, CancellationToken cancellationToken = default) => Run(() => inner.CreateTenancyAsync(unitId, request, cancellationToken));
    public Task<TenancyDetailDto?> GetTenancyAsync(Guid tenancyId, CancellationToken cancellationToken = default) => Run(() => inner.GetTenancyAsync(tenancyId, cancellationToken));
    public Task RecordChargeAsync(Guid tenancyId, RecordChargeRequest request, CancellationToken cancellationToken = default) => Run(() => inner.RecordChargeAsync(tenancyId, request, cancellationToken));
    public Task<ReceiptDto> RecordPaymentAsync(Guid tenancyId, RecordPaymentRequest request, CancellationToken cancellationToken = default) => Run(() => inner.RecordPaymentAsync(tenancyId, request, cancellationToken));
    public Task<ReceiptDto?> GetReceiptAsync(string receiptNumber, CancellationToken cancellationToken = default) => Run(() => inner.GetReceiptAsync(receiptNumber, cancellationToken));
    public Task<IReadOnlyList<MaintenanceRequestDto>> GetMaintenanceAsync(CancellationToken cancellationToken = default) => Run(() => inner.GetMaintenanceAsync(cancellationToken));
    public Task<MaintenanceRequestDto> CreateMaintenanceAsync(CreateMaintenanceRequest request, CancellationToken cancellationToken = default) => Run(() => inner.CreateMaintenanceAsync(request, cancellationToken));
    public Task<MaintenanceRequestDto> UpdateMaintenanceStatusAsync(Guid requestId, UpdateMaintenanceStatusRequest request, CancellationToken cancellationToken = default) => Run(() => inner.UpdateMaintenanceStatusAsync(requestId, request, cancellationToken));
    public Task<N1PreviewDto> PreviewN1Async(Guid tenancyId, N1PreviewRequest request, CancellationToken cancellationToken = default) => Run(() => inner.PreviewN1Async(tenancyId, request, cancellationToken));
    public Task<N2PreviewDto> PreviewN2Async(Guid tenancyId, N2PreviewRequest request, CancellationToken cancellationToken = default) => Run(() => inner.PreviewN2Async(tenancyId, request, cancellationToken));
    public Task<N4PreviewDto> PreviewN4Async(Guid tenancyId, N4PreviewRequest request, CancellationToken cancellationToken = default) => Run(() => inner.PreviewN4Async(tenancyId, request, cancellationToken));
    public Task<NoticeRecordDto> SaveN1Async(Guid tenancyId, N1PreviewRequest request, CancellationToken cancellationToken = default) => Run(() => inner.SaveN1Async(tenancyId, request, cancellationToken));
    public Task<NoticeRecordDto> SaveN2Async(Guid tenancyId, N2PreviewRequest request, CancellationToken cancellationToken = default) => Run(() => inner.SaveN2Async(tenancyId, request, cancellationToken));
    public Task<NoticeRecordDto> SaveN4Async(Guid tenancyId, N4PreviewRequest request, CancellationToken cancellationToken = default) => Run(() => inner.SaveN4Async(tenancyId, request, cancellationToken));
    public Task<IReadOnlyList<NoticeRecordDto>> GetNoticesAsync(Guid tenancyId, CancellationToken cancellationToken = default) => Run(() => inner.GetNoticesAsync(tenancyId, cancellationToken));
    public Task<YearEndDto?> GetYearEndAsync(Guid tenancyId, int year, CancellationToken cancellationToken = default) => Run(() => inner.GetYearEndAsync(tenancyId, year, cancellationToken));
    public Task<InspectionLogDto?> GetInspectionsAsync(Guid unitId, CancellationToken cancellationToken = default) => Run(() => inner.GetInspectionsAsync(unitId, cancellationToken));
    public Task<InspectionDto> RecordInspectionAsync(Guid unitId, RecordInspectionRequest request, CancellationToken cancellationToken = default) => Run(() => inner.RecordInspectionAsync(unitId, request, cancellationToken));
    public Task<TerminationPreviewDto> PreviewN5Async(Guid tenancyId, N5PreviewRequest request, CancellationToken cancellationToken = default) => Run(() => inner.PreviewN5Async(tenancyId, request, cancellationToken));
    public Task<TerminationPreviewDto> PreviewN6Async(Guid tenancyId, N6PreviewRequest request, CancellationToken cancellationToken = default) => Run(() => inner.PreviewN6Async(tenancyId, request, cancellationToken));
    public Task<TerminationPreviewDto> PreviewN7Async(Guid tenancyId, N7PreviewRequest request, CancellationToken cancellationToken = default) => Run(() => inner.PreviewN7Async(tenancyId, request, cancellationToken));
    public Task<N3PreviewDto> PreviewN3Async(Guid tenancyId, N3PreviewRequest request, CancellationToken cancellationToken = default) => Run(() => inner.PreviewN3Async(tenancyId, request, cancellationToken));
    public Task<NoticeRecordDto> SaveN5Async(Guid tenancyId, N5PreviewRequest request, CancellationToken cancellationToken = default) => Run(() => inner.SaveN5Async(tenancyId, request, cancellationToken));
    public Task<NoticeRecordDto> SaveN6Async(Guid tenancyId, N6PreviewRequest request, CancellationToken cancellationToken = default) => Run(() => inner.SaveN6Async(tenancyId, request, cancellationToken));
    public Task<NoticeRecordDto> SaveN7Async(Guid tenancyId, N7PreviewRequest request, CancellationToken cancellationToken = default) => Run(() => inner.SaveN7Async(tenancyId, request, cancellationToken));
    public Task<NoticeRecordDto> SaveN3Async(Guid tenancyId, N3PreviewRequest request, CancellationToken cancellationToken = default) => Run(() => inner.SaveN3Async(tenancyId, request, cancellationToken));
    public Task RecordNoticeServiceAsync(Guid noticeId, RecordServiceRequest request, CancellationToken cancellationToken = default) => Run(() => inner.RecordNoticeServiceAsync(noticeId, request, cancellationToken));
    public Task AcknowledgeNoticeAsync(Guid noticeId, AcknowledgeNoticeRequest request, CancellationToken cancellationToken = default) => Run(() => inner.AcknowledgeNoticeAsync(noticeId, request, cancellationToken));
    public Task<WorkOrderDto> AssignWorkOrderAsync(Guid requestId, AssignWorkOrderRequest request, CancellationToken cancellationToken = default) => Run(() => inner.AssignWorkOrderAsync(requestId, request, cancellationToken));
    public Task<InsuranceRecordDto?> GetInsuranceAsync(Guid tenancyId, CancellationToken cancellationToken = default) => Run(() => inner.GetInsuranceAsync(tenancyId, cancellationToken));
    public Task<InsuranceRecordDto> SaveInsuranceAsync(Guid tenancyId, SaveInsuranceRequest request, CancellationToken cancellationToken = default) => Run(() => inner.SaveInsuranceAsync(tenancyId, request, cancellationToken));
    public Task<IReadOnlyList<LegalContactDto>> GetLegalContactsAsync(CancellationToken cancellationToken = default) => Run(() => inner.GetLegalContactsAsync(cancellationToken));
    public Task<LegalContactDto> SaveLegalContactAsync(SaveLegalContactRequest request, CancellationToken cancellationToken = default) => Run(() => inner.SaveLegalContactAsync(request, cancellationToken));
    public Task ReferNoticeAsync(Guid noticeId, Guid contactId, CancellationToken cancellationToken = default) => Run(() => inner.ReferNoticeAsync(noticeId, contactId, cancellationToken));
    public Task<PaymentInstructionsDto?> GetPaymentInstructionsAsync(Guid tenancyId, CancellationToken cancellationToken = default) => Run(() => inner.GetPaymentInstructionsAsync(tenancyId, cancellationToken));
    public Task AuthorizePadAsync(Guid tenancyId, AcknowledgeNoticeRequest request, CancellationToken cancellationToken = default) => Run(() => inner.AuthorizePadAsync(tenancyId, request, cancellationToken));
    public Task<CheckoutStartDto> StartCheckoutAsync(StartCheckoutRequest request, CancellationToken cancellationToken = default) => Run(() => inner.StartCheckoutAsync(request, cancellationToken));
    public Task<CheckoutStartDto> ConfirmCheckoutAsync(string sessionId, CancellationToken cancellationToken = default) => Run(() => inner.ConfirmCheckoutAsync(sessionId, cancellationToken));
    public Task<PlanDto> GetPlanAsync(CancellationToken cancellationToken = default) => Run(() => inner.GetPlanAsync(cancellationToken));
    public Task<IReadOnlyList<PdfTemplateSummaryDto>> GetPdfTemplatesAsync(CancellationToken cancellationToken = default) => Run(() => inner.GetPdfTemplatesAsync(cancellationToken));
    public Task<PdfTemplateDetailDto> UploadPdfTemplateAsync(UploadPdfTemplateRequest request, CancellationToken cancellationToken = default) => Run(() => inner.UploadPdfTemplateAsync(request, cancellationToken));
    public Task<PdfTemplateDetailDto?> GetPdfTemplateAsync(Guid templateId, CancellationToken cancellationToken = default) => Run(() => inner.GetPdfTemplateAsync(templateId, cancellationToken));
    public Task<PdfFileDto?> GetPdfTemplateFileAsync(Guid templateId, CancellationToken cancellationToken = default) => Run(() => inner.GetPdfTemplateFileAsync(templateId, cancellationToken));
    public Task<PdfTemplateDetailDto> UpdatePdfTemplateAsync(Guid templateId, UpdatePdfTemplateRequest request, CancellationToken cancellationToken = default) => Run(() => inner.UpdatePdfTemplateAsync(templateId, request, cancellationToken));
    public Task DeletePdfTemplateAsync(Guid templateId, CancellationToken cancellationToken = default) => Run(() => inner.DeletePdfTemplateAsync(templateId, cancellationToken));
    public Task<PdfFileDto> FillPdfTemplateAsync(Guid templateId, Guid tenancyId, CancellationToken cancellationToken = default) => Run(() => inner.FillPdfTemplateAsync(templateId, tenancyId, cancellationToken));
    public Task<AgentHistoryDto> GetAgentHistoryAsync(CancellationToken cancellationToken = default) => Run(() => inner.GetAgentHistoryAsync(cancellationToken));
    public Task SaveAgentHistoryAsync(AgentHistoryDto history, CancellationToken cancellationToken = default) => Run(() => inner.SaveAgentHistoryAsync(history, cancellationToken));

    private async Task<T> Run<T>(Func<Task<T>> action)
    {
        await gate.WaitAsync();
        try
        {
            return await action();
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task Run(Func<Task> action)
    {
        await gate.WaitAsync();
        try
        {
            await action();
        }
        finally
        {
            gate.Release();
        }
    }
}
