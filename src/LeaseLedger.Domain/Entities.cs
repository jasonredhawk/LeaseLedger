namespace LeaseLedger.Domain;

public sealed class Property
{
    public Guid Id { get; set; }
    public Guid LandlordUserId { get; set; }
    public string Street { get; set; } = "";
    public string City { get; set; } = "";
    public string Province { get; set; } = "ON";
    public string PostalCode { get; set; } = "";
    public List<Unit> Units { get; set; } = [];
}

public sealed class Unit
{
    public Guid Id { get; set; }
    public Guid PropertyId { get; set; }
    public string Label { get; set; } = "";
    public bool FirstOccupiedAfterNovember152018 { get; set; }
    public bool IsCareHome { get; set; }
    public Property Property { get; set; } = null!;
    public List<Tenancy> Tenancies { get; set; } = [];
    public List<UnitInspection> Inspections { get; set; } = [];
}

public sealed class Tenancy
{
    public Guid Id { get; set; }
    public Guid UnitId { get; set; }
    public Guid? TenantUserId { get; set; }
    public string TenantName { get; set; } = "";
    public string TenantEmail { get; set; } = "";
    public decimal RentAmount { get; set; }
    public int RentDueDay { get; set; }
    public RentFrequency Frequency { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? LastRentIncreaseDate { get; set; }
    public Unit Unit { get; set; } = null!;
    public List<LedgerEntry> LedgerEntries { get; set; } = [];
    public List<MaintenanceRequest> MaintenanceRequests { get; set; } = [];
    public List<NoticeRecord> Notices { get; set; } = [];
}

public sealed class LedgerEntry
{
    public Guid Id { get; set; }
    public Guid TenancyId { get; set; }
    public LedgerEntryKind Kind { get; set; }
    public ChargeCategory? ChargeCategory { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public decimal Amount { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public string Description { get; set; } = "";
    public string? Note { get; set; }
    public string? ReceiptNumber { get; set; }
    public Tenancy Tenancy { get; set; } = null!;
}

public sealed class MaintenanceRequest
{
    public Guid Id { get; set; }
    public Guid TenancyId { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public MaintenanceStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Tenancy Tenancy { get; set; } = null!;
    public List<MaintenanceEvent> Events { get; set; } = [];
    public List<WorkOrder> WorkOrders { get; set; } = [];
}

public sealed class MaintenanceEvent
{
    public Guid Id { get; set; }
    public Guid RequestId { get; set; }
    public MaintenanceStatus Status { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public Guid ActorUserId { get; set; }
    public MaintenanceRequest Request { get; set; } = null!;
}

public sealed class NoticeRecord
{
    public Guid Id { get; set; }
    public Guid TenancyId { get; set; }
    public NoticeKind Kind { get; set; }
    public DateOnly NoticeDate { get; set; }
    public DateOnly? ResultDate { get; set; }
    public decimal? Amount { get; set; }
    public string Summary { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public Guid CreatedByUserId { get; set; }
    public Tenancy Tenancy { get; set; } = null!;
    public List<NoticeServiceRecord> Services { get; set; } = [];
    public List<NoticeAcknowledgement> Acknowledgements { get; set; } = [];
    public List<NoticeReferral> Referrals { get; set; } = [];
}

public sealed class NoticeServiceRecord
{
    public Guid Id { get; set; }
    public Guid NoticeRecordId { get; set; }
    public ServiceMethod Method { get; set; }
    public DateOnly ServedOn { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
    public NoticeRecord Notice { get; set; } = null!;
}

public sealed class NoticeAcknowledgement
{
    public Guid Id { get; set; }
    public Guid NoticeRecordId { get; set; }
    public string SignedName { get; set; } = "";
    public DateTimeOffset SignedAt { get; set; }
    public Guid UserId { get; set; }
    public NoticeRecord Notice { get; set; } = null!;
}

public sealed class NoticeReferral
{
    public Guid Id { get; set; }
    public Guid NoticeRecordId { get; set; }
    public string ContactName { get; set; } = "";
    public string ContactEmail { get; set; } = "";
    public DateTimeOffset SentAt { get; set; }
    public NoticeRecord Notice { get; set; } = null!;
}

public sealed class WorkOrder
{
    public Guid Id { get; set; }
    public Guid MaintenanceRequestId { get; set; }
    public string TradeName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Trade { get; set; } = "";
    public DateOnly? ScheduledFor { get; set; }
    public string Notes { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public MaintenanceRequest Request { get; set; } = null!;
}

public sealed class InsuranceRecord
{
    public Guid Id { get; set; }
    public Guid TenancyId { get; set; }
    public InsuranceStatus Status { get; set; }
    public string BrokerName { get; set; } = "";
    public string? PolicyNumber { get; set; }
    public bool LandlordListed { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Tenancy Tenancy { get; set; } = null!;
}

public sealed class LegalContact
{
    public Guid Id { get; set; }
    public Guid LandlordUserId { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Firm { get; set; } = "";
}

public sealed class PadAuthorization
{
    public Guid Id { get; set; }
    public Guid TenancyId { get; set; }
    public string SignedName { get; set; } = "";
    public DateTimeOffset SignedAt { get; set; }
    public Tenancy Tenancy { get; set; } = null!;
}

public sealed class CardCheckout
{
    public Guid Id { get; set; }
    public Guid? TenancyId { get; set; }
    public Guid LandlordUserId { get; set; }
    public string Kind { get; set; } = "";
    public string? StripeSessionId { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = "Open";
    public string? ReceiptNumber { get; set; }
}

public sealed class LandlordPlan
{
    public Guid LandlordUserId { get; set; }
    public string Status { get; set; } = "None";
    public string? StripeCustomerId { get; set; }
    public string? StripeSubscriptionId { get; set; }
}

public sealed class UnitInspection
{
    public Guid Id { get; set; }
    public Guid UnitId { get; set; }
    public DateOnly InspectedOn { get; set; }
    public string Summary { get; set; } = "";
    public string Notes { get; set; } = "";
    public DateTimeOffset RecordedAt { get; set; }
    public Guid RecordedByUserId { get; set; }
    public Unit Unit { get; set; } = null!;
}

public sealed class RentGuideline
{
    public int Year { get; set; }
    public decimal Percent { get; set; }
}

public sealed class PdfFormTemplate
{
    public Guid Id { get; set; }
    public Guid LandlordUserId { get; set; }
    public string Name { get; set; } = "";
    public string FileName { get; set; } = "";
    public byte[] Content { get; set; } = [];
    public DateTimeOffset UploadedAt { get; set; }
    public NoticeKind? NoticeKind { get; set; }
    public List<PdfFormField> Fields { get; set; } = [];
}

public sealed class AgentHistory
{
    public Guid UserId { get; set; }
    public string Payload { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class PdfFormField
{
    public Guid Id { get; set; }
    public Guid TemplateId { get; set; }
    public string Name { get; set; } = "";
    public string Label { get; set; } = "";
    public PdfFieldKind Kind { get; set; }
    public string? Options { get; set; }
    public string? DataKey { get; set; }
    public string? ConstantValue { get; set; }
    public PdfValueFormat Format { get; set; }
    public int SortOrder { get; set; }
    public PdfFormTemplate Template { get; set; } = null!;
}
