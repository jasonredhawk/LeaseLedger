using System.Text.Json;
using System.Text.Json.Serialization;
using LeaseLedger.Domain;

namespace LeaseLedger.Application;

public interface ICurrentUserAccessor
{
    Task<CurrentUser> GetAsync(CancellationToken cancellationToken = default);
}

public sealed record CurrentUser(Guid? UserId, string? Email, string? Role, string? DisplayName)
{
    public bool IsAuthenticated => UserId is not null;
}

public interface ILeaseLedgerClient
{
    Task<CurrentUserDto?> GetCurrentUserAsync(CancellationToken cancellationToken = default);
    Task<DashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PropertyDto>> GetPropertiesAsync(CancellationToken cancellationToken = default);
    Task<PropertyDto> CreatePropertyAsync(CreatePropertyRequest request, CancellationToken cancellationToken = default);
    Task<UnitDto> AddUnitAsync(Guid propertyId, CreateUnitRequest request, CancellationToken cancellationToken = default);
    Task<TenancyDto> CreateTenancyAsync(Guid unitId, CreateTenancyRequest request, CancellationToken cancellationToken = default);
    Task<TenancyDetailDto?> GetTenancyAsync(Guid tenancyId, CancellationToken cancellationToken = default);
    Task RecordChargeAsync(Guid tenancyId, RecordChargeRequest request, CancellationToken cancellationToken = default);
    Task<ReceiptDto> RecordPaymentAsync(Guid tenancyId, RecordPaymentRequest request, CancellationToken cancellationToken = default);
    Task<ReceiptDto?> GetReceiptAsync(string receiptNumber, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MaintenanceRequestDto>> GetMaintenanceAsync(CancellationToken cancellationToken = default);
    Task<MaintenanceRequestDto> CreateMaintenanceAsync(CreateMaintenanceRequest request, CancellationToken cancellationToken = default);
    Task<MaintenanceRequestDto> UpdateMaintenanceStatusAsync(Guid requestId, UpdateMaintenanceStatusRequest request, CancellationToken cancellationToken = default);
    Task<N1PreviewDto> PreviewN1Async(Guid tenancyId, N1PreviewRequest request, CancellationToken cancellationToken = default);
    Task<N2PreviewDto> PreviewN2Async(Guid tenancyId, N2PreviewRequest request, CancellationToken cancellationToken = default);
    Task<N4PreviewDto> PreviewN4Async(Guid tenancyId, N4PreviewRequest request, CancellationToken cancellationToken = default);
    Task<NoticeRecordDto> SaveN1Async(Guid tenancyId, N1PreviewRequest request, CancellationToken cancellationToken = default);
    Task<NoticeRecordDto> SaveN2Async(Guid tenancyId, N2PreviewRequest request, CancellationToken cancellationToken = default);
    Task<NoticeRecordDto> SaveN4Async(Guid tenancyId, N4PreviewRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NoticeRecordDto>> GetNoticesAsync(Guid tenancyId, CancellationToken cancellationToken = default);
    Task<YearEndDto?> GetYearEndAsync(Guid tenancyId, int year, CancellationToken cancellationToken = default);
    Task<InspectionLogDto?> GetInspectionsAsync(Guid unitId, CancellationToken cancellationToken = default);
    Task<InspectionDto> RecordInspectionAsync(Guid unitId, RecordInspectionRequest request, CancellationToken cancellationToken = default);
    Task<TerminationPreviewDto> PreviewN5Async(Guid tenancyId, N5PreviewRequest request, CancellationToken cancellationToken = default);
    Task<TerminationPreviewDto> PreviewN6Async(Guid tenancyId, N6PreviewRequest request, CancellationToken cancellationToken = default);
    Task<TerminationPreviewDto> PreviewN7Async(Guid tenancyId, N7PreviewRequest request, CancellationToken cancellationToken = default);
    Task<N3PreviewDto> PreviewN3Async(Guid tenancyId, N3PreviewRequest request, CancellationToken cancellationToken = default);
    Task<NoticeRecordDto> SaveN5Async(Guid tenancyId, N5PreviewRequest request, CancellationToken cancellationToken = default);
    Task<NoticeRecordDto> SaveN6Async(Guid tenancyId, N6PreviewRequest request, CancellationToken cancellationToken = default);
    Task<NoticeRecordDto> SaveN7Async(Guid tenancyId, N7PreviewRequest request, CancellationToken cancellationToken = default);
    Task<NoticeRecordDto> SaveN3Async(Guid tenancyId, N3PreviewRequest request, CancellationToken cancellationToken = default);
    Task RecordNoticeServiceAsync(Guid noticeId, RecordServiceRequest request, CancellationToken cancellationToken = default);
    Task AcknowledgeNoticeAsync(Guid noticeId, AcknowledgeNoticeRequest request, CancellationToken cancellationToken = default);
    Task<WorkOrderDto> AssignWorkOrderAsync(Guid requestId, AssignWorkOrderRequest request, CancellationToken cancellationToken = default);
    Task<InsuranceRecordDto?> GetInsuranceAsync(Guid tenancyId, CancellationToken cancellationToken = default);
    Task<InsuranceRecordDto> SaveInsuranceAsync(Guid tenancyId, SaveInsuranceRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LegalContactDto>> GetLegalContactsAsync(CancellationToken cancellationToken = default);
    Task<LegalContactDto> SaveLegalContactAsync(SaveLegalContactRequest request, CancellationToken cancellationToken = default);
    Task ReferNoticeAsync(Guid noticeId, Guid contactId, CancellationToken cancellationToken = default);
    Task<PaymentInstructionsDto?> GetPaymentInstructionsAsync(Guid tenancyId, CancellationToken cancellationToken = default);
    Task AuthorizePadAsync(Guid tenancyId, AcknowledgeNoticeRequest request, CancellationToken cancellationToken = default);
    Task<CheckoutStartDto> StartCheckoutAsync(StartCheckoutRequest request, CancellationToken cancellationToken = default);
    Task<CheckoutStartDto> ConfirmCheckoutAsync(string sessionId, CancellationToken cancellationToken = default);
    Task<PlanDto> GetPlanAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PdfTemplateSummaryDto>> GetPdfTemplatesAsync(CancellationToken cancellationToken = default);
    Task<PdfTemplateDetailDto> UploadPdfTemplateAsync(UploadPdfTemplateRequest request, CancellationToken cancellationToken = default);
    Task<PdfTemplateDetailDto?> GetPdfTemplateAsync(Guid templateId, CancellationToken cancellationToken = default);
    Task<PdfFileDto?> GetPdfTemplateFileAsync(Guid templateId, CancellationToken cancellationToken = default);
    Task<PdfTemplateDetailDto> UpdatePdfTemplateAsync(Guid templateId, UpdatePdfTemplateRequest request, CancellationToken cancellationToken = default);
    Task DeletePdfTemplateAsync(Guid templateId, CancellationToken cancellationToken = default);
    Task<PdfFileDto> FillPdfTemplateAsync(Guid templateId, Guid tenancyId, CancellationToken cancellationToken = default);
    Task<AgentHistoryDto> GetAgentHistoryAsync(CancellationToken cancellationToken = default);
    Task SaveAgentHistoryAsync(AgentHistoryDto history, CancellationToken cancellationToken = default);
}

public interface IAuthClient
{
    bool ShowDemoHints { get; }
    Task LogoutAsync(CancellationToken cancellationToken = default);
    Task<AuthResultDto> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<AuthResultDto> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
}

public interface IAccountGateway
{
    Task<AccountOutcome> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<AccountOutcome> ValidatePasswordAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task LinkTenantInvitesAsync(Guid userId, CancellationToken cancellationToken = default);
}

public sealed record AccountOutcome(bool Succeeded, string? Error, Guid? UserId, string? Email, string? DisplayName, string? Role)
{
    public static AccountOutcome Fail(string error) => new(false, error, null, null, null, null);

    public static AccountOutcome Ok(Guid userId, string email, string displayName, string role) =>
        new(true, null, userId, email, displayName, role);

    public CurrentUserDto ToDto() => new(UserId!.Value, Email!, DisplayName!, Role!);
}

public sealed record AuthResultDto(bool Succeeded, string? Error, CurrentUserDto? User)
{
    public static AuthResultDto Fail(string? error) => new(false, error, null);

    public static AuthResultDto Ok(CurrentUserDto user) => new(true, null, user);
}

public sealed record CurrentUserDto(Guid UserId, string Email, string DisplayName, string Role);

public sealed record DashboardDto(
    string DisplayName,
    string Role,
    decimal TotalArrears,
    int OpenMaintenanceCount,
    DateOnly? NextDueDate,
    IReadOnlyList<TenancySummaryDto> Tenancies);

public sealed record TenancySummaryDto(
    Guid Id,
    string PropertyStreet,
    string UnitLabel,
    string TenantName,
    decimal RentAmount,
    decimal Balance,
    IReadOnlyList<RentPeriodDto> RecentRent,
    Guid PropertyId,
    string City);

public sealed record RentPeriodDto(DateOnly DueOn, RentTimeliness Timeliness, DateOnly? PaidOn);

public sealed record PropertyDto(
    Guid Id,
    string Street,
    string City,
    string Province,
    string PostalCode,
    IReadOnlyList<UnitDto> Units);

public sealed record UnitDto(
    Guid Id,
    string Label,
    bool FirstOccupiedAfterNovember152018,
    bool IsCareHome,
    IReadOnlyList<TenancySummaryDto> Tenancies);

public sealed record TenancyDto(Guid Id, string TenantName, string TenantEmail, bool TenantLinked);

public sealed record TenancyDetailDto(
    Guid Id,
    Guid UnitId,
    string PropertyStreet,
    string City,
    string UnitLabel,
    string TenantName,
    string TenantEmail,
    bool TenantLinked,
    decimal RentAmount,
    int RentDueDay,
    RentFrequency Frequency,
    DateOnly StartDate,
    DateOnly? LastRentIncreaseDate,
    bool ExemptFromGuideline,
    decimal Balance,
    bool CanManageLedger,
    IReadOnlyList<LedgerLineDto> Lines);

public sealed record LedgerLineDto(
    Guid Id,
    DateOnly EffectiveDate,
    LedgerEntryKind Kind,
    string Description,
    decimal Amount,
    string? ReceiptNumber,
    PaymentMethod? Method);

public sealed record ReceiptDto(
    string ReceiptNumber,
    DateOnly PaidOn,
    decimal Amount,
    PaymentMethod Method,
    string TenantName,
    string Street,
    string UnitLabel,
    string City,
    string? Note);

public sealed record MaintenanceRequestDto(
    Guid Id,
    Guid TenancyId,
    string PropertyStreet,
    string UnitLabel,
    string Title,
    string Description,
    MaintenanceStatus Status,
    bool CanUpdateStatus,
    IReadOnlyList<MaintenanceEventDto> Events,
    WorkOrderDto? WorkOrder);

public sealed record MaintenanceEventDto(MaintenanceStatus Status, string? Note, DateTimeOffset OccurredAt);

public sealed record N1PreviewDto(
    bool IsValid,
    IReadOnlyList<string> Errors,
    decimal CurrentRent,
    decimal? GuidelinePercent,
    decimal? NewRent,
    DateOnly? EarliestEffectiveDate,
    string Explanation,
    string Disclaimer);

public sealed record N2PreviewDto(
    bool IsValid,
    IReadOnlyList<string> Errors,
    decimal CurrentRent,
    decimal? IncreaseAmount,
    decimal? IncreasePercent,
    decimal? NewRent,
    DateOnly? EarliestEffectiveDate,
    string Explanation,
    string Disclaimer);

public sealed record N4PreviewDto(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings,
    decimal Arrears,
    int NoticeDays,
    DateOnly? TerminationDate,
    string Disclaimer);

public sealed record NoticeRecordDto(
    Guid Id,
    NoticeKind Kind,
    DateOnly NoticeDate,
    DateOnly? ResultDate,
    decimal? Amount,
    string Summary,
    DateTimeOffset CreatedAt,
    string? ServiceNote,
    string? AcknowledgedBy,
    string? ReferredTo);

public sealed record YearEndDto(
    int Year,
    string TenantName,
    string Street,
    string UnitLabel,
    string City,
    decimal RentCharged,
    decimal OtherCharged,
    decimal NsfCharged,
    decimal Payments,
    IReadOnlyList<LedgerLineDto> Lines);

public sealed record InspectionLogDto(
    bool CanRecord,
    string Street,
    string UnitLabel,
    IReadOnlyList<InspectionDto> Inspections);

public sealed record InspectionDto(DateOnly InspectedOn, string Summary, string Notes, DateTimeOffset RecordedAt);

public sealed record TerminationPreviewDto(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings,
    int NoticeDays,
    DateOnly? TerminationDate,
    string Explanation,
    string Disclaimer);

public sealed record N3PreviewDto(
    bool IsValid,
    IReadOnlyList<string> Errors,
    decimal? NewRent,
    decimal? NewCareAndMeals,
    DateOnly? EarliestDate,
    string Explanation,
    string Disclaimer);

public sealed record WorkOrderDto(string TradeName, string Phone, string Trade, DateOnly? ScheduledFor, string Notes);

public sealed record InsuranceRecordDto(InsuranceStatus Status, string BrokerName, string? PolicyNumber, bool LandlordListed, string? Note, DateTimeOffset UpdatedAt);

public sealed record LegalContactDto(Guid Id, string Name, string Email, string Firm);

public sealed record PaymentInstructionsDto(decimal AmountDue, string LandlordEmail, string Reference, bool PadAuthorized, string PadNote);

public sealed record CheckoutStartDto(bool Configured, string? Url, string Message);

public sealed record PlanDto(string Status, bool CheckoutConfigured);

public sealed class LoginRequest
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
}

public sealed class RegisterRequest
{
    public string DisplayName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public string Role { get; set; } = AppRoles.Landlord;
}

public sealed class CreatePropertyRequest
{
    public string Street { get; set; } = "";
    public string City { get; set; } = "";
    public string PostalCode { get; set; } = "";
    public string Province { get; set; } = "ON";
}

public sealed class CreateUnitRequest
{
    public string Label { get; set; } = "";
    public bool FirstOccupiedAfterNovember152018 { get; set; }
    public bool IsCareHome { get; set; }
}

public sealed class CreateTenancyRequest
{
    public string TenantName { get; set; } = "";
    public string TenantEmail { get; set; } = "";
    public decimal RentAmount { get; set; }
    public int RentDueDay { get; set; } = 1;
    public RentFrequency Frequency { get; set; } = RentFrequency.Monthly;
    public DateOnly StartDate { get; set; }
    public DateOnly? LastRentIncreaseDate { get; set; }
}

public sealed class RecordChargeRequest
{
    public ChargeCategory Category { get; set; } = ChargeCategory.Rent;
    public decimal Amount { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public string Note { get; set; } = "";
}

public sealed class RecordPaymentRequest
{
    public PaymentMethod Method { get; set; } = PaymentMethod.ETransfer;
    public decimal Amount { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public string Note { get; set; } = "";
}

public sealed class CreateMaintenanceRequest
{
    public Guid TenancyId { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
}

public sealed class UpdateMaintenanceStatusRequest
{
    public MaintenanceStatus Status { get; set; }
    public string Note { get; set; } = "";
}

public sealed class N1PreviewRequest
{
    public DateOnly NoticeDate { get; set; }
    public DateOnly ProposedEffectiveDate { get; set; }
}

public sealed class N2PreviewRequest
{
    public DateOnly NoticeDate { get; set; }
    public DateOnly ProposedEffectiveDate { get; set; }
    public decimal ProposedRent { get; set; }
}

public sealed class N4PreviewRequest
{
    public DateOnly NoticeDate { get; set; }
}

public sealed class RecordInspectionRequest
{
    public DateOnly InspectedOn { get; set; }
    public string Summary { get; set; } = "";
    public string Notes { get; set; } = "";
}

public sealed class N5PreviewRequest
{
    public DateOnly NoticeDate { get; set; }
    public N5Reason Reason { get; set; } = N5Reason.Interference;
    public bool SecondNotice { get; set; }
    public DateOnly? FirstNoticeDate { get; set; }
    public string Details { get; set; } = "";
}

public sealed class N6PreviewRequest
{
    public DateOnly NoticeDate { get; set; }
    public N6Reason Reason { get; set; } = N6Reason.OtherIllegalAct;
    public bool AfterVoidableN5 { get; set; }
    public string Details { get; set; } = "";
}

public sealed class N7PreviewRequest
{
    public DateOnly NoticeDate { get; set; }
    public string Details { get; set; } = "";
}

public sealed class N3PreviewRequest
{
    public bool IncreasingRent { get; set; }
    public decimal ProposedRent { get; set; }
    public decimal CurrentCareAndMeals { get; set; }
    public decimal ProposedCareAndMeals { get; set; }
    public DateOnly NoticeDate { get; set; }
    public DateOnly EffectiveDate { get; set; }
}

public sealed class RecordServiceRequest
{
    public ServiceMethod Method { get; set; } = ServiceMethod.InPerson;
    public DateOnly ServedOn { get; set; }
    public string Note { get; set; } = "";
}

public sealed class AcknowledgeNoticeRequest
{
    public string SignedName { get; set; } = "";
}

public sealed class AssignWorkOrderRequest
{
    public string TradeName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Trade { get; set; } = "";
    public DateOnly? ScheduledFor { get; set; }
    public string Notes { get; set; } = "";
}

public sealed class SaveInsuranceRequest
{
    public InsuranceStatus Status { get; set; } = InsuranceStatus.Requested;
    public string BrokerName { get; set; } = "";
    public string PolicyNumber { get; set; } = "";
    public bool LandlordListed { get; set; } = true;
    public string Note { get; set; } = "";
}

public sealed class SaveLegalContactRequest
{
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Firm { get; set; } = "";
}

public sealed class StartCheckoutRequest
{
    public string Kind { get; set; } = "rent";
    public Guid? TenancyId { get; set; }
    public string ReturnUrl { get; set; } = "";
}

public sealed record PdfTemplateSummaryDto(Guid Id, string Name, string FileName, int FieldCount, DateTimeOffset UploadedAt, NoticeKind? NoticeKind);

public sealed record PdfFieldDto(
    Guid Id,
    string Name,
    string Label,
    PdfFieldKind Kind,
    IReadOnlyList<string> Options,
    string? DataKey,
    string? ConstantValue,
    PdfValueFormat Format);

public sealed record PdfTemplateDetailDto(
    Guid Id,
    string Name,
    string FileName,
    DateTimeOffset UploadedAt,
    IReadOnlyList<PdfFieldDto> Fields);

public sealed record PdfFileDto(string FileName, byte[] Content);

public sealed class UploadPdfTemplateRequest
{
    public string Name { get; set; } = "";
    public string FileName { get; set; } = "";
    public byte[] Content { get; set; } = [];
    public NoticeKind? NoticeKind { get; set; }
}

public sealed class PdfFieldMappingRequest
{
    public Guid FieldId { get; set; }
    public string? DataKey { get; set; }
    public string? ConstantValue { get; set; }
    public PdfValueFormat Format { get; set; }
}

public sealed class UpdatePdfTemplateRequest
{
    public string Name { get; set; } = "";
    public List<PdfFieldMappingRequest> Fields { get; set; } = [];
}

public sealed record AgentHistoryDto(
    IReadOnlyList<AgentConversationDto> Conversations,
    IReadOnlyList<AgentNoteDto> Notes,
    IReadOnlyList<AgentTodoOverrideDto> Overrides,
    IReadOnlyList<TodoItem> Todos)
{
    public static AgentHistoryDto Empty { get; } = new([], [], [], []);
}

public sealed record AgentConversationDto(
    Guid Id,
    string Title,
    string? Preview,
    string? LastHref,
    Guid? TenancyId,
    DateTime UpdatedAt,
    IReadOnlyList<AgentTurnDto> Turns);

public sealed record AgentTurnDto(
    Guid Id,
    string Role,
    string Text,
    bool Confirmed,
    Guid? ActionId,
    string? ProposalSummary,
    IReadOnlyList<AgentActionDto> Actions);

public sealed record AgentActionDto(
    string Href,
    string Label,
    bool External,
    Guid? TenancyId,
    string? Group,
    string? Send);

public sealed record AgentNoteDto(Guid Id, string Role, string Text, IReadOnlyList<TodoReply> Replies);

public sealed record AgentTodoOverrideDto(string Key, string? Title, DateOnly? Due, bool Deleted);

public sealed record ApiError(string Error);

public sealed record AccessTokenResponse(CurrentUserDto User, string AccessToken);

public static class LeaseLedgerJson
{
    public static JsonSerializerOptions Options { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
