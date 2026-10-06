using System.Net;
using System.Net.Http.Json;
using LeaseLedger.Application;

namespace LeaseLedger.Mobile.Services;

public sealed class HttpLeaseLedgerClient(HttpClient http) : ILeaseLedgerClient
{
    public Task<CurrentUserDto?> GetCurrentUserAsync(CancellationToken cancellationToken = default) =>
        ReadOrNull<CurrentUserDto>(http.GetAsync("api/me", cancellationToken), cancellationToken);

    public Task<DashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default) =>
        Read<DashboardDto>(http.GetAsync("api/dashboard", cancellationToken), cancellationToken);

    public Task<IReadOnlyList<PropertyDto>> GetPropertiesAsync(CancellationToken cancellationToken = default) =>
        Read<IReadOnlyList<PropertyDto>>(http.GetAsync("api/properties", cancellationToken), cancellationToken);

    public Task<PropertyDto> CreatePropertyAsync(CreatePropertyRequest request, CancellationToken cancellationToken = default) =>
        Read<PropertyDto>(http.PostAsJsonAsync("api/properties", request, LeaseLedgerJson.Options, cancellationToken), cancellationToken);

    public Task<UnitDto> AddUnitAsync(Guid propertyId, CreateUnitRequest request, CancellationToken cancellationToken = default) =>
        Read<UnitDto>(http.PostAsJsonAsync($"api/properties/{propertyId}/units", request, LeaseLedgerJson.Options, cancellationToken), cancellationToken);

    public Task<TenancyDto> CreateTenancyAsync(Guid unitId, CreateTenancyRequest request, CancellationToken cancellationToken = default) =>
        Read<TenancyDto>(http.PostAsJsonAsync($"api/units/{unitId}/tenancies", request, LeaseLedgerJson.Options, cancellationToken), cancellationToken);

    public Task<TenancyDetailDto?> GetTenancyAsync(Guid tenancyId, CancellationToken cancellationToken = default) =>
        ReadOrNull<TenancyDetailDto>(http.GetAsync($"api/tenancies/{tenancyId}", cancellationToken), cancellationToken);

    public async Task RecordChargeAsync(Guid tenancyId, RecordChargeRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync($"api/tenancies/{tenancyId}/charges", request, LeaseLedgerJson.Options, cancellationToken);
        await EnsureAsync(response, cancellationToken);
    }

    public Task<ReceiptDto> RecordPaymentAsync(Guid tenancyId, RecordPaymentRequest request, CancellationToken cancellationToken = default) =>
        Read<ReceiptDto>(http.PostAsJsonAsync($"api/tenancies/{tenancyId}/payments", request, LeaseLedgerJson.Options, cancellationToken), cancellationToken);

    public Task<ReceiptDto?> GetReceiptAsync(string receiptNumber, CancellationToken cancellationToken = default) =>
        ReadOrNull<ReceiptDto>(http.GetAsync($"api/receipts/{Uri.EscapeDataString(receiptNumber)}", cancellationToken), cancellationToken);

    public Task<IReadOnlyList<MaintenanceRequestDto>> GetMaintenanceAsync(CancellationToken cancellationToken = default) =>
        Read<IReadOnlyList<MaintenanceRequestDto>>(http.GetAsync("api/maintenance", cancellationToken), cancellationToken);

    public Task<MaintenanceRequestDto> CreateMaintenanceAsync(CreateMaintenanceRequest request, CancellationToken cancellationToken = default) =>
        Read<MaintenanceRequestDto>(http.PostAsJsonAsync("api/maintenance", request, LeaseLedgerJson.Options, cancellationToken), cancellationToken);

    public Task<MaintenanceRequestDto> UpdateMaintenanceStatusAsync(Guid requestId, UpdateMaintenanceStatusRequest request, CancellationToken cancellationToken = default) =>
        Read<MaintenanceRequestDto>(http.PostAsJsonAsync($"api/maintenance/{requestId}/status", request, LeaseLedgerJson.Options, cancellationToken), cancellationToken);

    public Task<N1PreviewDto> PreviewN1Async(Guid tenancyId, N1PreviewRequest request, CancellationToken cancellationToken = default) =>
        Read<N1PreviewDto>(http.PostAsJsonAsync($"api/tenancies/{tenancyId}/n1", request, LeaseLedgerJson.Options, cancellationToken), cancellationToken);

    public Task<N2PreviewDto> PreviewN2Async(Guid tenancyId, N2PreviewRequest request, CancellationToken cancellationToken = default) =>
        Read<N2PreviewDto>(http.PostAsJsonAsync($"api/tenancies/{tenancyId}/n2", request, LeaseLedgerJson.Options, cancellationToken), cancellationToken);

    public Task<N4PreviewDto> PreviewN4Async(Guid tenancyId, N4PreviewRequest request, CancellationToken cancellationToken = default) =>
        Read<N4PreviewDto>(http.PostAsJsonAsync($"api/tenancies/{tenancyId}/n4", request, LeaseLedgerJson.Options, cancellationToken), cancellationToken);

    public Task<NoticeRecordDto> SaveN1Async(Guid tenancyId, N1PreviewRequest request, CancellationToken cancellationToken = default) =>
        Read<NoticeRecordDto>(http.PostAsJsonAsync($"api/tenancies/{tenancyId}/n1/save", request, LeaseLedgerJson.Options, cancellationToken), cancellationToken);

    public Task<NoticeRecordDto> SaveN2Async(Guid tenancyId, N2PreviewRequest request, CancellationToken cancellationToken = default) =>
        Read<NoticeRecordDto>(http.PostAsJsonAsync($"api/tenancies/{tenancyId}/n2/save", request, LeaseLedgerJson.Options, cancellationToken), cancellationToken);

    public Task<NoticeRecordDto> SaveN4Async(Guid tenancyId, N4PreviewRequest request, CancellationToken cancellationToken = default) =>
        Read<NoticeRecordDto>(http.PostAsJsonAsync($"api/tenancies/{tenancyId}/n4/save", request, LeaseLedgerJson.Options, cancellationToken), cancellationToken);

    public Task<IReadOnlyList<NoticeRecordDto>> GetNoticesAsync(Guid tenancyId, CancellationToken cancellationToken = default) =>
        Read<IReadOnlyList<NoticeRecordDto>>(http.GetAsync($"api/tenancies/{tenancyId}/notices", cancellationToken), cancellationToken);

    public Task<YearEndDto?> GetYearEndAsync(Guid tenancyId, int year, CancellationToken cancellationToken = default) =>
        ReadOrNull<YearEndDto>(http.GetAsync($"api/tenancies/{tenancyId}/year/{year}", cancellationToken), cancellationToken);

    public Task<InspectionLogDto?> GetInspectionsAsync(Guid unitId, CancellationToken cancellationToken = default) =>
        ReadOrNull<InspectionLogDto>(http.GetAsync($"api/units/{unitId}/inspections", cancellationToken), cancellationToken);

    public Task<InspectionDto> RecordInspectionAsync(Guid unitId, RecordInspectionRequest request, CancellationToken cancellationToken = default) =>
        Read<InspectionDto>(http.PostAsJsonAsync($"api/units/{unitId}/inspections", request, LeaseLedgerJson.Options, cancellationToken), cancellationToken);

    public Task<TerminationPreviewDto> PreviewN5Async(Guid tenancyId, N5PreviewRequest request, CancellationToken cancellationToken = default) =>
        Read<TerminationPreviewDto>(http.PostAsJsonAsync($"api/tenancies/{tenancyId}/n5", request, LeaseLedgerJson.Options, cancellationToken), cancellationToken);

    public Task<TerminationPreviewDto> PreviewN6Async(Guid tenancyId, N6PreviewRequest request, CancellationToken cancellationToken = default) =>
        Read<TerminationPreviewDto>(http.PostAsJsonAsync($"api/tenancies/{tenancyId}/n6", request, LeaseLedgerJson.Options, cancellationToken), cancellationToken);

    public Task<TerminationPreviewDto> PreviewN7Async(Guid tenancyId, N7PreviewRequest request, CancellationToken cancellationToken = default) =>
        Read<TerminationPreviewDto>(http.PostAsJsonAsync($"api/tenancies/{tenancyId}/n7", request, LeaseLedgerJson.Options, cancellationToken), cancellationToken);

    public Task<N3PreviewDto> PreviewN3Async(Guid tenancyId, N3PreviewRequest request, CancellationToken cancellationToken = default) =>
        Read<N3PreviewDto>(http.PostAsJsonAsync($"api/tenancies/{tenancyId}/n3", request, LeaseLedgerJson.Options, cancellationToken), cancellationToken);

    public Task<NoticeRecordDto> SaveN5Async(Guid tenancyId, N5PreviewRequest request, CancellationToken cancellationToken = default) =>
        Read<NoticeRecordDto>(http.PostAsJsonAsync($"api/tenancies/{tenancyId}/n5/save", request, LeaseLedgerJson.Options, cancellationToken), cancellationToken);

    public Task<NoticeRecordDto> SaveN6Async(Guid tenancyId, N6PreviewRequest request, CancellationToken cancellationToken = default) =>
        Read<NoticeRecordDto>(http.PostAsJsonAsync($"api/tenancies/{tenancyId}/n6/save", request, LeaseLedgerJson.Options, cancellationToken), cancellationToken);

    public Task<NoticeRecordDto> SaveN7Async(Guid tenancyId, N7PreviewRequest request, CancellationToken cancellationToken = default) =>
        Read<NoticeRecordDto>(http.PostAsJsonAsync($"api/tenancies/{tenancyId}/n7/save", request, LeaseLedgerJson.Options, cancellationToken), cancellationToken);

    public Task<NoticeRecordDto> SaveN3Async(Guid tenancyId, N3PreviewRequest request, CancellationToken cancellationToken = default) =>
        Read<NoticeRecordDto>(http.PostAsJsonAsync($"api/tenancies/{tenancyId}/n3/save", request, LeaseLedgerJson.Options, cancellationToken), cancellationToken);

    public async Task RecordNoticeServiceAsync(Guid noticeId, RecordServiceRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync($"api/notices/{noticeId}/service", request, LeaseLedgerJson.Options, cancellationToken);
        await EnsureAsync(response, cancellationToken);
    }

    public async Task AcknowledgeNoticeAsync(Guid noticeId, AcknowledgeNoticeRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync($"api/notices/{noticeId}/acknowledge", request, LeaseLedgerJson.Options, cancellationToken);
        await EnsureAsync(response, cancellationToken);
    }

    public Task<WorkOrderDto> AssignWorkOrderAsync(Guid requestId, AssignWorkOrderRequest request, CancellationToken cancellationToken = default) =>
        Read<WorkOrderDto>(http.PostAsJsonAsync($"api/maintenance/{requestId}/work-order", request, LeaseLedgerJson.Options, cancellationToken), cancellationToken);

    public Task<InsuranceRecordDto?> GetInsuranceAsync(Guid tenancyId, CancellationToken cancellationToken = default) =>
        ReadOrNull<InsuranceRecordDto>(http.GetAsync($"api/tenancies/{tenancyId}/insurance", cancellationToken), cancellationToken);

    public Task<InsuranceRecordDto> SaveInsuranceAsync(Guid tenancyId, SaveInsuranceRequest request, CancellationToken cancellationToken = default) =>
        Read<InsuranceRecordDto>(http.PostAsJsonAsync($"api/tenancies/{tenancyId}/insurance", request, LeaseLedgerJson.Options, cancellationToken), cancellationToken);

    public Task<IReadOnlyList<LegalContactDto>> GetLegalContactsAsync(CancellationToken cancellationToken = default) =>
        Read<IReadOnlyList<LegalContactDto>>(http.GetAsync("api/legal-contacts", cancellationToken), cancellationToken);

    public Task<LegalContactDto> SaveLegalContactAsync(SaveLegalContactRequest request, CancellationToken cancellationToken = default) =>
        Read<LegalContactDto>(http.PostAsJsonAsync("api/legal-contacts", request, LeaseLedgerJson.Options, cancellationToken), cancellationToken);

    public async Task ReferNoticeAsync(Guid noticeId, Guid contactId, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsync($"api/notices/{noticeId}/refer/{contactId}", null, cancellationToken);
        await EnsureAsync(response, cancellationToken);
    }

    public Task<PaymentInstructionsDto?> GetPaymentInstructionsAsync(Guid tenancyId, CancellationToken cancellationToken = default) =>
        ReadOrNull<PaymentInstructionsDto>(http.GetAsync($"api/tenancies/{tenancyId}/payment-instructions", cancellationToken), cancellationToken);

    public async Task AuthorizePadAsync(Guid tenancyId, AcknowledgeNoticeRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync($"api/tenancies/{tenancyId}/pad", request, LeaseLedgerJson.Options, cancellationToken);
        await EnsureAsync(response, cancellationToken);
    }

    public Task<CheckoutStartDto> StartCheckoutAsync(StartCheckoutRequest request, CancellationToken cancellationToken = default) =>
        Read<CheckoutStartDto>(http.PostAsJsonAsync("api/checkout", request, LeaseLedgerJson.Options, cancellationToken), cancellationToken);

    public Task<CheckoutStartDto> ConfirmCheckoutAsync(string sessionId, CancellationToken cancellationToken = default) =>
        Read<CheckoutStartDto>(http.PostAsync($"api/checkout/confirm?sessionId={Uri.EscapeDataString(sessionId)}", new StringContent(""), cancellationToken), cancellationToken);

    public Task<PlanDto> GetPlanAsync(CancellationToken cancellationToken = default) =>
        Read<PlanDto>(http.GetAsync("api/plan", cancellationToken), cancellationToken);

    public Task<IReadOnlyList<PdfTemplateSummaryDto>> GetPdfTemplatesAsync(CancellationToken cancellationToken = default) =>
        Read<IReadOnlyList<PdfTemplateSummaryDto>>(http.GetAsync("api/pdf-templates", cancellationToken), cancellationToken);

    public Task<PdfTemplateDetailDto> UploadPdfTemplateAsync(UploadPdfTemplateRequest request, CancellationToken cancellationToken = default) =>
        Read<PdfTemplateDetailDto>(http.PostAsJsonAsync("api/pdf-templates", request, LeaseLedgerJson.Options, cancellationToken), cancellationToken);

    public Task<PdfTemplateDetailDto?> GetPdfTemplateAsync(Guid templateId, CancellationToken cancellationToken = default) =>
        ReadOrNull<PdfTemplateDetailDto>(http.GetAsync($"api/pdf-templates/{templateId}", cancellationToken), cancellationToken);

    public Task<PdfFileDto?> GetPdfTemplateFileAsync(Guid templateId, CancellationToken cancellationToken = default) =>
        ReadOrNull<PdfFileDto>(http.GetAsync($"api/pdf-templates/{templateId}/file", cancellationToken), cancellationToken);

    public Task<PdfTemplateDetailDto> UpdatePdfTemplateAsync(Guid templateId, UpdatePdfTemplateRequest request, CancellationToken cancellationToken = default) =>
        Read<PdfTemplateDetailDto>(http.PutAsJsonAsync($"api/pdf-templates/{templateId}", request, LeaseLedgerJson.Options, cancellationToken), cancellationToken);

    public async Task DeletePdfTemplateAsync(Guid templateId, CancellationToken cancellationToken = default)
    {
        using var response = await http.DeleteAsync($"api/pdf-templates/{templateId}", cancellationToken);
        await EnsureAsync(response, cancellationToken);
    }

    public Task<PdfFileDto> FillPdfTemplateAsync(Guid templateId, Guid tenancyId, CancellationToken cancellationToken = default) =>
        Read<PdfFileDto>(http.PostAsync($"api/pdf-templates/{templateId}/fill/{tenancyId}", new StringContent(""), cancellationToken), cancellationToken);

    public Task<AgentHistoryDto> GetAgentHistoryAsync(CancellationToken cancellationToken = default) =>
        Read<AgentHistoryDto>(http.GetAsync("api/agent-history", cancellationToken), cancellationToken);

    public async Task SaveAgentHistoryAsync(AgentHistoryDto history, CancellationToken cancellationToken = default)
    {
        using var response = await http.PutAsJsonAsync("api/agent-history", history, LeaseLedgerJson.Options, cancellationToken);
        await EnsureAsync(response, cancellationToken);
    }

    private static async Task<T> Read<T>(Task<HttpResponseMessage> request, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(request, cancellationToken); await EnsureAsync(response, cancellationToken);
        await EnsureAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<T>(LeaseLedgerJson.Options, cancellationToken))!;
    }

    private static async Task<T?> ReadOrNull<T>(Task<HttpResponseMessage> request, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(request, cancellationToken);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound)
            return default;

        await EnsureAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<T>(LeaseLedgerJson.Options, cancellationToken);
    }

    private static async Task<HttpResponseMessage> SendAsync(Task<HttpResponseMessage> request, CancellationToken cancellationToken)
    {
        try
        {
            return await request;
        }
        catch (HttpRequestException)
        {
            throw new LeaseLedgerException(ApiSettings.UnreachableMessage);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new LeaseLedgerException(ApiSettings.UnreachableMessage);
        }
    }

    private static async Task EnsureAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        var error = await response.Content.ReadFromJsonAsync<ApiError>(LeaseLedgerJson.Options, cancellationToken);
        throw new LeaseLedgerException(error?.Error ?? "The request failed.");
    }
}
