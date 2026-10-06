using LeaseLedger.Application;
using LeaseLedger.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LeaseLedger.Infrastructure;

public sealed partial class LeaseLedgerService(LeaseLedgerDbContext db, ICurrentUserAccessor currentUser, UserManager<AppUser> users, IPaymentGateway payments) : ILeaseLedgerClient
{
    public async Task<CurrentUserDto?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        var user = await currentUser.GetAsync(cancellationToken);
        if (user.UserId is null || user.Email is null || user.Role is null)
            return null;

        return new CurrentUserDto(user.UserId.Value, user.Email, user.DisplayName ?? user.Email, user.Role);
    }

    public async Task<DashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var user = await RequireUserAsync(cancellationToken);
        var tenancies = await VisibleTenancies(user).AsNoTracking().ToListAsync(cancellationToken);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var ledgers = await LedgersAsync(tenancies.Select(tenancy => tenancy.Id), today, cancellationToken);
        var openMaintenance = await VisibleRequests(user).CountAsync(request => request.Status != MaintenanceStatus.Completed, cancellationToken);
        DateOnly? nextDue = null;
        foreach (var tenancy in tenancies)
        {
            var due = RentDue.NextOnOrAfter(today, tenancy.StartDate, tenancy.RentDueDay, tenancy.Frequency);
            if (nextDue is null || due < nextDue)
                nextDue = due;
        }

        return new DashboardDto(
            user.DisplayName ?? user.Email!,
            user.Role!,
            ledgers.Values.Where(ledger => ledger.Balance > 0).Sum(ledger => ledger.Balance),
            openMaintenance,
            nextDue,
            tenancies.Select(tenancy => ToSummary(tenancy, ledgers.GetValueOrDefault(tenancy.Id))).ToList());
    }

    public async Task<IReadOnlyList<PropertyDto>> GetPropertiesAsync(CancellationToken cancellationToken = default)
    {
        var user = await RequireRoleAsync(AppRoles.Landlord, cancellationToken);
        var properties = await db.Properties.AsNoTracking()
            .Include(property => property.Units).ThenInclude(unit => unit.Tenancies)
            .Where(property => property.LandlordUserId == user.UserId)
            .OrderBy(property => property.Street)
            .ToListAsync(cancellationToken);

        var tenancyIds = properties.SelectMany(property => property.Units).SelectMany(unit => unit.Tenancies).Select(tenancy => tenancy.Id);
        var ledgers = await LedgersAsync(tenancyIds, DateOnly.FromDateTime(DateTime.Today), cancellationToken);

        return properties.Select(property => new PropertyDto(
            property.Id,
            property.Street,
            property.City,
            property.Province,
            property.PostalCode,
            property.Units.OrderBy(unit => unit.Label).Select(unit => new UnitDto(
                unit.Id,
                unit.Label,
                unit.FirstOccupiedAfterNovember152018,
                unit.IsCareHome,
                unit.Tenancies.Select(tenancy => ToSummary(tenancy, ledgers.GetValueOrDefault(tenancy.Id), property.Street, unit.Label)).ToList()
            )).ToList()
        )).ToList();
    }

    public async Task<PropertyDto> CreatePropertyAsync(CreatePropertyRequest request, CancellationToken cancellationToken = default)
    {
        var user = await RequireRoleAsync(AppRoles.Landlord, cancellationToken);
        var street = Required(request.Street, "Enter the street address.");
        var city = Required(request.City, "Enter the city.");
        var postal = Required(request.PostalCode, "Enter the postal code.");

        var property = new Property
        {
            Id = Guid.NewGuid(),
            LandlordUserId = user.UserId!.Value,
            Street = street,
            City = city,
            Province = NormalizeProvince(request.Province),
            PostalCode = postal.ToUpperInvariant()
        };
        db.Properties.Add(property);
        await db.SaveChangesAsync(cancellationToken);
        return new PropertyDto(property.Id, property.Street, property.City, property.Province, property.PostalCode, []);
    }

    public async Task<UnitDto> AddUnitAsync(Guid propertyId, CreateUnitRequest request, CancellationToken cancellationToken = default)
    {
        var user = await RequireRoleAsync(AppRoles.Landlord, cancellationToken);
        var property = await db.Properties.FirstOrDefaultAsync(item => item.Id == propertyId && item.LandlordUserId == user.UserId, cancellationToken)
            ?? throw new LeaseLedgerException("That property was not found.");
        var label = Required(request.Label, "Enter a unit label.");

        var unit = new Unit
        {
            Id = Guid.NewGuid(),
            PropertyId = property.Id,
            Label = label,
            FirstOccupiedAfterNovember152018 = request.FirstOccupiedAfterNovember152018,
            IsCareHome = request.IsCareHome
        };
        db.Units.Add(unit);
        await db.SaveChangesAsync(cancellationToken);
        return new UnitDto(unit.Id, unit.Label, unit.FirstOccupiedAfterNovember152018, unit.IsCareHome, []);
    }

    public async Task<TenancyDto> CreateTenancyAsync(Guid unitId, CreateTenancyRequest request, CancellationToken cancellationToken = default)
    {
        var user = await RequireRoleAsync(AppRoles.Landlord, cancellationToken);
        var unit = await db.Units.Include(item => item.Property)
            .FirstOrDefaultAsync(item => item.Id == unitId && item.Property.LandlordUserId == user.UserId, cancellationToken)
            ?? throw new LeaseLedgerException("That unit was not found.");

        var tenantName = Required(request.TenantName, "Enter the tenant's name.");
        var email = AccountGateway.NormalizeEmail(request.TenantEmail);
        if (email.Length == 0 || !email.Contains('@'))
            throw new LeaseLedgerException("Enter the tenant's email.");
        if (request.RentAmount <= 0)
            throw new LeaseLedgerException("Rent must be greater than zero.");
        if (request.RentDueDay is < 1 or > 31)
            throw new LeaseLedgerException("Rent due day must be from 1 to 31.");
        if (!Enum.IsDefined(request.Frequency))
            throw new LeaseLedgerException("Choose how often rent is paid.");
        if (request.LastRentIncreaseDate is DateOnly last && last < request.StartDate)
            throw new LeaseLedgerException("The last increase cannot be before the tenancy started.");

        Guid? tenantUserId = null;
        var existing = await users.FindByEmailAsync(email);
        if (existing is not null)
        {
            if (!await users.IsInRoleAsync(existing, AppRoles.Tenant))
                throw new LeaseLedgerException("That email belongs to a landlord account. Invite a tenant email.");
            tenantUserId = existing.Id;
        }

        var tenancy = new Tenancy
        {
            Id = Guid.NewGuid(),
            UnitId = unit.Id,
            TenantUserId = tenantUserId,
            TenantName = tenantName,
            TenantEmail = email,
            RentAmount = request.RentAmount,
            RentDueDay = request.RentDueDay,
            Frequency = request.Frequency,
            StartDate = request.StartDate,
            LastRentIncreaseDate = request.LastRentIncreaseDate
        };
        db.Tenancies.Add(tenancy);
        await db.SaveChangesAsync(cancellationToken);
        return new TenancyDto(tenancy.Id, tenancy.TenantName, tenancy.TenantEmail, tenantUserId is not null);
    }

    public async Task<TenancyDetailDto?> GetTenancyAsync(Guid tenancyId, CancellationToken cancellationToken = default)
    {
        var user = await RequireUserAsync(cancellationToken);
        var tenancy = await VisibleTenancies(user)
            .AsNoTracking()
            .Include(item => item.LedgerEntries)
            .FirstOrDefaultAsync(item => item.Id == tenancyId, cancellationToken);
        if (tenancy is null)
            return null;

        var lines = tenancy.LedgerEntries
            .OrderBy(entry => entry.EffectiveDate)
            .ThenBy(entry => entry.Kind)
            .Select(entry => new LedgerLineDto(
                entry.Id,
                entry.EffectiveDate,
                entry.Kind,
                entry.Description,
                entry.Amount,
                entry.ReceiptNumber,
                entry.PaymentMethod))
            .ToList();

        return new TenancyDetailDto(
            tenancy.Id,
            tenancy.UnitId,
            tenancy.Unit.Property.Street,
            tenancy.Unit.Property.City,
            tenancy.Unit.Label,
            tenancy.TenantName,
            tenancy.TenantEmail,
            tenancy.TenantUserId is not null,
            tenancy.RentAmount,
            tenancy.RentDueDay,
            tenancy.Frequency,
            tenancy.StartDate,
            tenancy.LastRentIncreaseDate,
            tenancy.Unit.FirstOccupiedAfterNovember152018,
            LedgerMath.Balance(tenancy.LedgerEntries),
            user.Role == AppRoles.Landlord,
            lines);
    }

    public async Task RecordChargeAsync(Guid tenancyId, RecordChargeRequest request, CancellationToken cancellationToken = default)
    {
        var user = await RequireRoleAsync(AppRoles.Landlord, cancellationToken);
        var tenancy = await RequireVisibleTenancyAsync(user, tenancyId, cancellationToken);
        if (request.Amount <= 0)
            throw new LeaseLedgerException("The charge amount must be greater than zero.");
        if (!Enum.IsDefined(request.Category))
            throw new LeaseLedgerException("Choose a charge type.");

        db.LedgerEntries.Add(new LedgerEntry
        {
            Id = Guid.NewGuid(),
            TenancyId = tenancy.Id,
            Kind = LedgerEntryKind.Charge,
            ChargeCategory = request.Category,
            Amount = request.Amount,
            EffectiveDate = request.EffectiveDate,
            Description = ChargeDescription(request.Category, request.Note),
            Note = Clean(request.Note)
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ReceiptDto> RecordPaymentAsync(Guid tenancyId, RecordPaymentRequest request, CancellationToken cancellationToken = default)
    {
        var user = await RequireRoleAsync(AppRoles.Landlord, cancellationToken);
        var tenancy = await RequireVisibleTenancyAsync(user, tenancyId, cancellationToken);
        if (request.Amount <= 0)
            throw new LeaseLedgerException("The payment amount must be greater than zero.");
        if (!Enum.IsDefined(request.Method))
            throw new LeaseLedgerException("Choose how the payment was received.");

        var entry = new LedgerEntry
        {
            Id = Guid.NewGuid(),
            TenancyId = tenancy.Id,
            Kind = LedgerEntryKind.Payment,
            PaymentMethod = request.Method,
            Amount = request.Amount,
            EffectiveDate = request.EffectiveDate,
            Description = $"Payment ({MethodLabel(request.Method)})",
            Note = Clean(request.Note),
            ReceiptNumber = NewReceiptNumber(request.EffectiveDate)
        };
        db.LedgerEntries.Add(entry);
        await db.SaveChangesAsync(cancellationToken);
        return ToReceipt(entry, tenancy);
    }

    public async Task<ReceiptDto?> GetReceiptAsync(string receiptNumber, CancellationToken cancellationToken = default)
    {
        var user = await RequireUserAsync(cancellationToken);
        var number = receiptNumber.Trim().ToUpperInvariant();
        var entry = await db.LedgerEntries.AsNoTracking()
            .Include(item => item.Tenancy).ThenInclude(tenancy => tenancy.Unit).ThenInclude(unit => unit.Property)
            .FirstOrDefaultAsync(item => item.ReceiptNumber == number, cancellationToken);
        if (entry is null || !CanSee(user, entry.Tenancy))
            return null;

        return ToReceipt(entry, entry.Tenancy);
    }

    public async Task<IReadOnlyList<MaintenanceRequestDto>> GetMaintenanceAsync(CancellationToken cancellationToken = default)
    {
        var user = await RequireUserAsync(cancellationToken);
        var requests = await VisibleRequests(user)
            .AsNoTracking()
            .Include(request => request.Events)
            .Include(request => request.WorkOrders)
            .OrderByDescending(request => request.CreatedAt)
            .ToListAsync(cancellationToken);

        var canUpdate = user.Role == AppRoles.Landlord;
        return requests.Select(request => ToMaintenance(request, canUpdate)).ToList();
    }

    public async Task<MaintenanceRequestDto> CreateMaintenanceAsync(CreateMaintenanceRequest request, CancellationToken cancellationToken = default)
    {
        var user = await RequireUserAsync(cancellationToken);
        if (user.Role is not (AppRoles.Landlord or AppRoles.Tenant))
            throw new LeaseLedgerException("You do not have access to that action.");

        var tenancy = await RequireVisibleTenancyAsync(user, request.TenancyId, cancellationToken);
        var title = Required(request.Title, "Enter a short title.");
        var description = Required(request.Description, "Describe the problem.");
        var now = DateTimeOffset.UtcNow;

        var maintenance = new MaintenanceRequest
        {
            Id = Guid.NewGuid(),
            TenancyId = tenancy.Id,
            Title = title,
            Description = description,
            Status = MaintenanceStatus.Submitted,
            CreatedAt = now
        };
        maintenance.Events.Add(new MaintenanceEvent
        {
            Id = Guid.NewGuid(),
            Status = MaintenanceStatus.Submitted,
            Note = user.Role == AppRoles.Tenant ? "Submitted by the tenant." : "Logged for the landlord.",
            OccurredAt = now,
            ActorUserId = user.UserId!.Value
        });
        db.MaintenanceRequests.Add(maintenance);
        await db.SaveChangesAsync(cancellationToken);

        maintenance.Tenancy = tenancy;
        return ToMaintenance(maintenance, false);
    }

    public async Task<MaintenanceRequestDto> UpdateMaintenanceStatusAsync(Guid requestId, UpdateMaintenanceStatusRequest request, CancellationToken cancellationToken = default)
    {
        var user = await RequireRoleAsync(AppRoles.Landlord, cancellationToken);
        if (!Enum.IsDefined(request.Status))
            throw new LeaseLedgerException("Choose a status.");

        var maintenance = await VisibleRequests(user)
            .Include(item => item.Events)
            .Include(item => item.WorkOrders)
            .FirstOrDefaultAsync(item => item.Id == requestId, cancellationToken)
            ?? throw new LeaseLedgerException("That maintenance request was not found.");

        maintenance.Status = request.Status;
        db.MaintenanceEvents.Add(new MaintenanceEvent
        {
            Id = Guid.NewGuid(),
            RequestId = maintenance.Id,
            Status = request.Status,
            Note = Clean(request.Note),
            OccurredAt = DateTimeOffset.UtcNow,
            ActorUserId = user.UserId!.Value
        });
        await db.SaveChangesAsync(cancellationToken);
        return ToMaintenance(maintenance, true);
    }

    public async Task<N1PreviewDto> PreviewN1Async(Guid tenancyId, N1PreviewRequest request, CancellationToken cancellationToken = default)
    {
        var user = await RequireRoleAsync(AppRoles.Landlord, cancellationToken);
        var tenancy = await RequireVisibleTenancyAsync(user, tenancyId, cancellationToken);
        RequireOntario(tenancy);
        var guidelines = await db.RentGuidelines.AsNoTracking().ToDictionaryAsync(row => row.Year, row => row.Percent, cancellationToken);
        var result = OntarioN1Calculator.Calculate(new N1Input(
            tenancy.RentAmount,
            tenancy.StartDate,
            tenancy.LastRentIncreaseDate,
            request.NoticeDate,
            request.ProposedEffectiveDate,
            tenancy.Frequency,
            tenancy.RentDueDay,
            tenancy.Unit.FirstOccupiedAfterNovember152018), guidelines);

        return new N1PreviewDto(
            result.IsValid,
            result.Errors,
            tenancy.RentAmount,
            result.GuidelinePercent,
            result.NewRent,
            result.EarliestEffectiveDate,
            result.Explanation,
            NoticeCopy.Disclaimer);
    }

    public async Task<N4PreviewDto> PreviewN4Async(Guid tenancyId, N4PreviewRequest request, CancellationToken cancellationToken = default)
    {
        var user = await RequireRoleAsync(AppRoles.Landlord, cancellationToken);
        var tenancy = await RequireVisibleTenancyAsync(user, tenancyId, cancellationToken);
        RequireOntario(tenancy);
        var entries = await db.LedgerEntries.AsNoTracking().Where(entry => entry.TenancyId == tenancy.Id).ToListAsync(cancellationToken);
        var result = OntarioN4Calculator.Calculate(new N4Input(
            request.NoticeDate,
            tenancy.Frequency,
            LedgerMath.Balance(entries)));

        return new N4PreviewDto(
            result.IsValid,
            result.Errors,
            result.Warnings,
            result.Arrears,
            result.NoticeDays,
            result.TerminationDate,
            NoticeCopy.Disclaimer);
    }

    public async Task<N2PreviewDto> PreviewN2Async(Guid tenancyId, N2PreviewRequest request, CancellationToken cancellationToken = default)
    {
        var user = await RequireRoleAsync(AppRoles.Landlord, cancellationToken);
        var tenancy = await RequireVisibleTenancyAsync(user, tenancyId, cancellationToken);
        RequireOntario(tenancy);
        var result = CalculateN2(tenancy, request);
        return ToN2(tenancy.RentAmount, request.ProposedRent, result);
    }

    public async Task<NoticeRecordDto> SaveN1Async(Guid tenancyId, N1PreviewRequest request, CancellationToken cancellationToken = default)
    {
        var preview = await PreviewN1Async(tenancyId, request, cancellationToken);
        if (!preview.IsValid || preview.NewRent is null)
            throw new LeaseLedgerException(preview.Errors.FirstOrDefault() ?? "That N1 worksheet is not valid.");

        var summary = $"N1 worksheet. Guideline {preview.GuidelinePercent:0.0}%. New rent {preview.NewRent:0.00} effective {preview.EarliestEffectiveDate:yyyy-MM-dd} or the later date you entered, {request.ProposedEffectiveDate:yyyy-MM-dd}.";
        return await StoreNoticeAsync(tenancyId, NoticeKind.N1, request.NoticeDate, request.ProposedEffectiveDate, preview.NewRent, summary, cancellationToken);
    }

    public async Task<NoticeRecordDto> SaveN2Async(Guid tenancyId, N2PreviewRequest request, CancellationToken cancellationToken = default)
    {
        var user = await RequireRoleAsync(AppRoles.Landlord, cancellationToken);
        var tenancy = await RequireVisibleTenancyAsync(user, tenancyId, cancellationToken);
        RequireOntario(tenancy);
        var result = CalculateN2(tenancy, request);
        if (!result.IsValid)
            throw new LeaseLedgerException(result.Errors.FirstOrDefault() ?? "That N2 worksheet is not valid.");

        var proposed = decimal.Round(request.ProposedRent, 2, MidpointRounding.AwayFromZero);
        var summary = $"N2 worksheet. Exempt from the guideline. New rent {proposed:0.00} (an increase of {result.IncreaseAmount:0.00}) effective {request.ProposedEffectiveDate:yyyy-MM-dd}.";
        return await StoreNoticeAsync(tenancyId, NoticeKind.N2, request.NoticeDate, request.ProposedEffectiveDate, proposed, summary, cancellationToken);
    }

    public async Task<NoticeRecordDto> SaveN4Async(Guid tenancyId, N4PreviewRequest request, CancellationToken cancellationToken = default)
    {
        var preview = await PreviewN4Async(tenancyId, request, cancellationToken);
        if (!preview.IsValid || preview.TerminationDate is null)
            throw new LeaseLedgerException(preview.Errors.FirstOrDefault() ?? "That N4 worksheet is not valid.");

        var summary = $"N4 worksheet. Arrears {preview.Arrears:0.00}. Termination {preview.TerminationDate:yyyy-MM-dd}, {preview.NoticeDays} days after the notice date.";
        return await StoreNoticeAsync(tenancyId, NoticeKind.N4, request.NoticeDate, preview.TerminationDate, preview.Arrears, summary, cancellationToken);
    }

    public async Task<IReadOnlyList<NoticeRecordDto>> GetNoticesAsync(Guid tenancyId, CancellationToken cancellationToken = default)
    {
        var user = await RequireUserAsync(cancellationToken);
        var tenancy = await RequireVisibleTenancyAsync(user, tenancyId, cancellationToken);
        var notices = await db.NoticeRecords.AsNoTracking()
            .Include(notice => notice.Services)
            .Include(notice => notice.Acknowledgements)
            .Include(notice => notice.Referrals)
            .Where(notice => notice.TenancyId == tenancy.Id)
            .OrderByDescending(notice => notice.CreatedAt)
            .ToListAsync(cancellationToken);
        return notices.Select(ToNotice).ToList();
    }

    public async Task<YearEndDto?> GetYearEndAsync(Guid tenancyId, int year, CancellationToken cancellationToken = default)
    {
        var user = await RequireUserAsync(cancellationToken);
        var tenancy = await VisibleTenancies(user)
            .AsNoTracking()
            .Include(item => item.LedgerEntries)
            .FirstOrDefaultAsync(item => item.Id == tenancyId, cancellationToken);
        if (tenancy is null)
            return null;

        if (year is < 2000 or > 2100)
            throw new LeaseLedgerException("Choose a calendar year between 2000 and 2100.");

        var totals = YearEndMath.Summarize(year, tenancy.LedgerEntries);
        var lines = tenancy.LedgerEntries
            .Where(entry => entry.EffectiveDate.Year == year)
            .OrderBy(entry => entry.EffectiveDate)
            .ThenBy(entry => entry.Kind)
            .Select(entry => new LedgerLineDto(entry.Id, entry.EffectiveDate, entry.Kind, entry.Description, entry.Amount, entry.ReceiptNumber, entry.PaymentMethod))
            .ToList();

        return new YearEndDto(
            year,
            tenancy.TenantName,
            tenancy.Unit.Property.Street,
            tenancy.Unit.Label,
            tenancy.Unit.Property.City,
            totals.RentCharged,
            totals.OtherCharged,
            totals.NsfCharged,
            totals.Payments,
            lines);
    }

    public async Task<InspectionLogDto?> GetInspectionsAsync(Guid unitId, CancellationToken cancellationToken = default)
    {
        var user = await RequireUserAsync(cancellationToken);
        var unit = await VisibleUnits(user).AsNoTracking().FirstOrDefaultAsync(item => item.Id == unitId, cancellationToken);
        if (unit is null)
            return null;

        var inspections = await db.UnitInspections.AsNoTracking()
            .Where(item => item.UnitId == unit.Id)
            .OrderByDescending(item => item.InspectedOn)
            .ThenByDescending(item => item.RecordedAt)
            .ToListAsync(cancellationToken);

        return new InspectionLogDto(
            user.Role == AppRoles.Landlord,
            unit.Property.Street,
            unit.Label,
            inspections.Select(item => new InspectionDto(item.InspectedOn, item.Summary, item.Notes, item.RecordedAt)).ToList());
    }

    public async Task<InspectionDto> RecordInspectionAsync(Guid unitId, RecordInspectionRequest request, CancellationToken cancellationToken = default)
    {
        var user = await RequireRoleAsync(AppRoles.Landlord, cancellationToken);
        var unit = await VisibleUnits(user).FirstOrDefaultAsync(item => item.Id == unitId, cancellationToken)
            ?? throw new LeaseLedgerException("That unit was not found.");
        var summary = Required(request.Summary, "Enter a short summary of the inspection.");
        var notes = Required(request.Notes, "Describe what you saw.");
        if (request.InspectedOn == default)
            throw new LeaseLedgerException("Choose the inspection date.");

        var inspection = new UnitInspection
        {
            Id = Guid.NewGuid(),
            UnitId = unit.Id,
            InspectedOn = request.InspectedOn,
            Summary = summary,
            Notes = notes,
            RecordedAt = DateTimeOffset.UtcNow,
            RecordedByUserId = user.UserId!.Value
        };
        db.UnitInspections.Add(inspection);
        await db.SaveChangesAsync(cancellationToken);
        return new InspectionDto(inspection.InspectedOn, inspection.Summary, inspection.Notes, inspection.RecordedAt);
    }

    private static N2Result CalculateN2(Tenancy tenancy, N2PreviewRequest request) =>
        OntarioN2Calculator.Calculate(new N2Input(
            tenancy.RentAmount,
            request.ProposedRent,
            tenancy.StartDate,
            tenancy.LastRentIncreaseDate,
            request.NoticeDate,
            request.ProposedEffectiveDate,
            tenancy.Frequency,
            tenancy.RentDueDay,
            tenancy.Unit.FirstOccupiedAfterNovember152018));

    private static N2PreviewDto ToN2(decimal currentRent, decimal proposedRent, N2Result result)
    {
        var proposed = decimal.Round(proposedRent, 2, MidpointRounding.AwayFromZero);
        return new N2PreviewDto(
            result.IsValid,
            result.Errors,
            currentRent,
            result.IncreaseAmount,
            result.IncreasePercent,
            result.IsValid ? proposed : null,
            result.EarliestEffectiveDate,
            result.Explanation,
            NoticeCopy.Disclaimer);
    }

    private async Task<NoticeRecordDto> StoreNoticeAsync(
        Guid tenancyId,
        NoticeKind kind,
        DateOnly noticeDate,
        DateOnly? resultDate,
        decimal? amount,
        string summary,
        CancellationToken cancellationToken)
    {
        var user = await RequireRoleAsync(AppRoles.Landlord, cancellationToken);
        await RequireVisibleTenancyAsync(user, tenancyId, cancellationToken);
        var notice = new NoticeRecord
        {
            Id = Guid.NewGuid(),
            TenancyId = tenancyId,
            Kind = kind,
            NoticeDate = noticeDate,
            ResultDate = resultDate,
            Amount = amount,
            Summary = summary,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByUserId = user.UserId!.Value
        };
        db.NoticeRecords.Add(notice);
        await db.SaveChangesAsync(cancellationToken);
        return ToNotice(notice);
    }

    private static NoticeRecordDto ToNotice(NoticeRecord notice)
    {
        var service = notice.Services.OrderByDescending(item => item.RecordedAt).FirstOrDefault();
        var signature = notice.Acknowledgements.OrderByDescending(item => item.SignedAt).FirstOrDefault();
        var referral = notice.Referrals.OrderByDescending(item => item.SentAt).FirstOrDefault();
        return new(
            notice.Id,
            notice.Kind,
            notice.NoticeDate,
            notice.ResultDate,
            notice.Amount,
            notice.Summary,
            notice.CreatedAt,
            service is null ? null : $"{service.Method} on {service.ServedOn:yyyy-MM-dd}",
            signature?.SignedName,
            referral is null ? null : $"{referral.ContactName} <{referral.ContactEmail}>");
    }

    private static void RequireOntario(Tenancy tenancy)
    {
        if (!ProvincialNotices.Supports(tenancy.Unit.Property.Province))
            throw new LeaseLedgerException(ProvincialNotices.UnsupportedMessage(tenancy.Unit.Property.Province));
    }

    private static string NormalizeProvince(string? province)
    {
        var code = (province ?? "ON").Trim().ToUpperInvariant();
        if (code.Length != 2)
            throw new LeaseLedgerException("Use a two-letter province code. Notice math is loaded for ON.");
        return code;
    }

    private IQueryable<Tenancy> VisibleTenancies(CurrentUser user)
    {
        var query = db.Tenancies
            .Include(tenancy => tenancy.Unit).ThenInclude(unit => unit.Property);
        return user.Role == AppRoles.Landlord
            ? query.Where(tenancy => tenancy.Unit.Property.LandlordUserId == user.UserId)
            : query.Where(tenancy => tenancy.TenantUserId == user.UserId);
    }

    private IQueryable<Unit> VisibleUnits(CurrentUser user)
    {
        var query = db.Units.Include(unit => unit.Property);
        return user.Role == AppRoles.Landlord
            ? query.Where(unit => unit.Property.LandlordUserId == user.UserId)
            : query.Where(unit => unit.Tenancies.Any(tenancy => tenancy.TenantUserId == user.UserId));
    }

    private IQueryable<MaintenanceRequest> VisibleRequests(CurrentUser user)
    {
        var query = db.MaintenanceRequests
            .Include(request => request.Tenancy).ThenInclude(tenancy => tenancy.Unit).ThenInclude(unit => unit.Property);
        return user.Role == AppRoles.Landlord
            ? query.Where(request => request.Tenancy.Unit.Property.LandlordUserId == user.UserId)
            : query.Where(request => request.Tenancy.TenantUserId == user.UserId);
    }

    private async Task<Tenancy> RequireVisibleTenancyAsync(CurrentUser user, Guid tenancyId, CancellationToken cancellationToken)
    {
        return await VisibleTenancies(user).FirstOrDefaultAsync(tenancy => tenancy.Id == tenancyId, cancellationToken)
            ?? throw new LeaseLedgerException("That tenancy was not found.");
    }

    private async Task<Dictionary<Guid, TenancyLedger>> LedgersAsync(IEnumerable<Guid> tenancyIds, DateOnly asOf, CancellationToken cancellationToken)
    {
        var ids = tenancyIds.Distinct().ToArray();
        if (ids.Length == 0)
            return [];

        var entries = await db.LedgerEntries.AsNoTracking()
            .Where(entry => ids.Contains(entry.TenancyId))
            .ToListAsync(cancellationToken);
        return entries.GroupBy(entry => entry.TenancyId).ToDictionary(
            group => group.Key,
            group => new TenancyLedger(LedgerMath.Balance(group), RentHistory.Recent(group, asOf)));
    }

    private async Task<CurrentUser> RequireUserAsync(CancellationToken cancellationToken)
    {
        var user = await currentUser.GetAsync(cancellationToken);
        if (!user.IsAuthenticated || user.Role is null)
            throw new UnauthorizedAccessException();
        return user;
    }

    private async Task<CurrentUser> RequireRoleAsync(string role, CancellationToken cancellationToken)
    {
        var user = await RequireUserAsync(cancellationToken);
        if (user.Role != role)
            throw new LeaseLedgerException("You do not have access to that action.");
        return user;
    }

    private static bool CanSee(CurrentUser user, Tenancy tenancy)
    {
        return user.Role == AppRoles.Landlord
            ? tenancy.Unit.Property.LandlordUserId == user.UserId
            : tenancy.TenantUserId == user.UserId;
    }

    private static TenancySummaryDto ToSummary(Tenancy tenancy, TenancyLedger? ledger, string? street = null, string? unit = null)
    {
        ledger ??= TenancyLedger.Empty;
        var property = tenancy.Unit.Property;
        return new(
            tenancy.Id,
            street ?? property.Street,
            unit ?? tenancy.Unit.Label,
            tenancy.TenantName,
            tenancy.RentAmount,
            ledger.Balance,
            ledger.RecentRent.Select(period => new RentPeriodDto(period.DueOn, period.Timeliness, period.PaidOn)).ToList(),
            property.Id,
            property.City);
    }

    private sealed record TenancyLedger(decimal Balance, IReadOnlyList<RentPeriodRating> RecentRent)
    {
        public static TenancyLedger Empty { get; } = new(0, []);
    }

    private static ReceiptDto ToReceipt(LedgerEntry entry, Tenancy tenancy) => new(
        entry.ReceiptNumber!,
        entry.EffectiveDate,
        entry.Amount,
        entry.PaymentMethod ?? PaymentMethod.Other,
        tenancy.TenantName,
        tenancy.Unit.Property.Street,
        tenancy.Unit.Label,
        tenancy.Unit.Property.City,
        entry.Note);

    private static MaintenanceRequestDto ToMaintenance(MaintenanceRequest request, bool canUpdate) => new(
        request.Id,
        request.TenancyId,
        request.Tenancy.Unit.Property.Street,
        request.Tenancy.Unit.Label,
        request.Title,
        request.Description,
        request.Status,
        canUpdate,
        request.Events.OrderBy(evt => evt.OccurredAt).Select(evt => new MaintenanceEventDto(evt.Status, evt.Note, evt.OccurredAt)).ToList(),
        request.WorkOrders.OrderByDescending(order => order.CreatedAt).Select(order => new WorkOrderDto(order.TradeName, order.Phone, order.Trade, order.ScheduledFor, order.Notes)).FirstOrDefault());

    private static string ChargeDescription(ChargeCategory category, string? note) => category switch
    {
        ChargeCategory.Rent => "Rent",
        ChargeCategory.Nsf => "NSF",
        _ => string.IsNullOrWhiteSpace(note) ? "Other charge" : note.Trim()
    };

    private static string MethodLabel(PaymentMethod method) => method switch
    {
        PaymentMethod.ETransfer => "e-transfer",
        PaymentMethod.Cash => "cash",
        PaymentMethod.Cheque => "cheque",
        PaymentMethod.Card => "card",
        _ => "other"
    };

    private static string Required(string? value, string message)
    {
        var trimmed = value?.Trim() ?? "";
        if (trimmed.Length == 0)
            throw new LeaseLedgerException(message);
        return trimmed;
    }

    private static string? Clean(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static string NewReceiptNumber(DateOnly date) =>
        $"R-{date:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
}
