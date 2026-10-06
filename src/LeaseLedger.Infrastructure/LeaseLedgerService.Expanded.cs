using LeaseLedger.Application;
using LeaseLedger.Domain;
using Microsoft.EntityFrameworkCore;

namespace LeaseLedger.Infrastructure;

public sealed partial class LeaseLedgerService
{
    public async Task<TerminationPreviewDto> PreviewN5Async(Guid tenancyId, N5PreviewRequest request, CancellationToken cancellationToken = default)
    {
        await RequireOntarioTenancyAsync(tenancyId, cancellationToken);
        return ToTermination(OntarioN5Calculator.Calculate(request.NoticeDate, request.SecondNotice, request.FirstNoticeDate));
    }

    public async Task<TerminationPreviewDto> PreviewN6Async(Guid tenancyId, N6PreviewRequest request, CancellationToken cancellationToken = default)
    {
        await RequireOntarioTenancyAsync(tenancyId, cancellationToken);
        if (!Enum.IsDefined(request.Reason))
            throw new LeaseLedgerException("Choose an N6 reason.");
        return ToTermination(OntarioN6Calculator.Calculate(request.NoticeDate, request.Reason, request.AfterVoidableN5));
    }

    public async Task<TerminationPreviewDto> PreviewN7Async(Guid tenancyId, N7PreviewRequest request, CancellationToken cancellationToken = default)
    {
        await RequireOntarioTenancyAsync(tenancyId, cancellationToken);
        return ToTermination(OntarioN7Calculator.Calculate(request.NoticeDate));
    }

    public async Task<N3PreviewDto> PreviewN3Async(Guid tenancyId, N3PreviewRequest request, CancellationToken cancellationToken = default)
    {
        var tenancy = await RequireOntarioTenancyAsync(tenancyId, cancellationToken);
        var guidelines = await db.RentGuidelines.AsNoTracking().ToDictionaryAsync(row => row.Year, row => row.Percent, cancellationToken);
        var result = OntarioN3Calculator.Calculate(ToN3Input(tenancy, request), guidelines);
        return new N3PreviewDto(result.IsValid, result.Errors, result.NewRent, result.NewCareAndMeals, result.EarliestDate, result.Explanation, NoticeCopy.Disclaimer);
    }

    public async Task<NoticeRecordDto> SaveN5Async(Guid tenancyId, N5PreviewRequest request, CancellationToken cancellationToken = default)
    {
        var preview = await PreviewN5Async(tenancyId, request, cancellationToken);
        if (!preview.IsValid || preview.TerminationDate is null)
            throw new LeaseLedgerException(preview.Errors.FirstOrDefault() ?? "That N5 worksheet is not valid.");
        var summary = $"N5 worksheet ({request.Reason}, {(request.SecondNotice ? "second" : "first")} notice). Termination {preview.TerminationDate:yyyy-MM-dd}. {Clean(request.Details)}";
        return await StoreNoticeAsync(tenancyId, NoticeKind.N5, request.NoticeDate, preview.TerminationDate, null, summary, cancellationToken);
    }

    public async Task<NoticeRecordDto> SaveN6Async(Guid tenancyId, N6PreviewRequest request, CancellationToken cancellationToken = default)
    {
        var preview = await PreviewN6Async(tenancyId, request, cancellationToken);
        var summary = $"N6 worksheet ({request.Reason}). Termination {preview.TerminationDate:yyyy-MM-dd}. {Clean(request.Details)}";
        return await StoreNoticeAsync(tenancyId, NoticeKind.N6, request.NoticeDate, preview.TerminationDate, null, summary, cancellationToken);
    }

    public async Task<NoticeRecordDto> SaveN7Async(Guid tenancyId, N7PreviewRequest request, CancellationToken cancellationToken = default)
    {
        var details = Required(request.Details, "Describe the serious problem.");
        var preview = await PreviewN7Async(tenancyId, request, cancellationToken);
        var summary = $"N7 worksheet. Termination {preview.TerminationDate:yyyy-MM-dd}. {details}";
        return await StoreNoticeAsync(tenancyId, NoticeKind.N7, request.NoticeDate, preview.TerminationDate, null, summary, cancellationToken);
    }

    public async Task<NoticeRecordDto> SaveN3Async(Guid tenancyId, N3PreviewRequest request, CancellationToken cancellationToken = default)
    {
        var preview = await PreviewN3Async(tenancyId, request, cancellationToken);
        if (!preview.IsValid)
            throw new LeaseLedgerException(preview.Errors.FirstOrDefault() ?? "That N3 worksheet is not valid.");
        var summary = $"N3 worksheet. New rent {preview.NewRent:0.00}. Care and meals {preview.NewCareAndMeals:0.00}. Effective {request.EffectiveDate:yyyy-MM-dd}.";
        return await StoreNoticeAsync(tenancyId, NoticeKind.N3, request.NoticeDate, request.EffectiveDate, preview.NewRent ?? preview.NewCareAndMeals, summary, cancellationToken);
    }

    public async Task RecordNoticeServiceAsync(Guid noticeId, RecordServiceRequest request, CancellationToken cancellationToken = default)
    {
        var user = await RequireRoleAsync(AppRoles.Landlord, cancellationToken);
        var notice = await RequireNoticeAsync(user, noticeId, cancellationToken);
        if (!Enum.IsDefined(request.Method))
            throw new LeaseLedgerException("Choose how the notice was given.");
        db.NoticeServices.Add(new NoticeServiceRecord
        {
            Id = Guid.NewGuid(),
            NoticeRecordId = notice.Id,
            Method = request.Method,
            ServedOn = request.ServedOn,
            Note = Clean(request.Note),
            RecordedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task AcknowledgeNoticeAsync(Guid noticeId, AcknowledgeNoticeRequest request, CancellationToken cancellationToken = default)
    {
        var user = await RequireRoleAsync(AppRoles.Tenant, cancellationToken);
        var notice = await RequireNoticeAsync(user, noticeId, cancellationToken);
        var name = Required(request.SignedName, "Type your name to acknowledge the worksheet.");
        db.NoticeAcknowledgements.Add(new NoticeAcknowledgement
        {
            Id = Guid.NewGuid(),
            NoticeRecordId = notice.Id,
            SignedName = name,
            SignedAt = DateTimeOffset.UtcNow,
            UserId = user.UserId!.Value
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<WorkOrderDto> AssignWorkOrderAsync(Guid requestId, AssignWorkOrderRequest request, CancellationToken cancellationToken = default)
    {
        var user = await RequireRoleAsync(AppRoles.Landlord, cancellationToken);
        var maintenance = await VisibleRequests(user).FirstOrDefaultAsync(item => item.Id == requestId, cancellationToken)
            ?? throw new LeaseLedgerException("That maintenance request was not found.");
        var order = new WorkOrder
        {
            Id = Guid.NewGuid(),
            MaintenanceRequestId = maintenance.Id,
            TradeName = Required(request.TradeName, "Enter the trade's name."),
            Phone = Required(request.Phone, "Enter a phone number."),
            Trade = Required(request.Trade, "Enter the trade, such as plumber."),
            ScheduledFor = request.ScheduledFor,
            Notes = Clean(request.Notes) ?? "",
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.WorkOrders.Add(order);
        await db.SaveChangesAsync(cancellationToken);
        return new WorkOrderDto(order.TradeName, order.Phone, order.Trade, order.ScheduledFor, order.Notes);
    }

    public async Task<InsuranceRecordDto?> GetInsuranceAsync(Guid tenancyId, CancellationToken cancellationToken = default)
    {
        var user = await RequireUserAsync(cancellationToken);
        var tenancy = await RequireVisibleTenancyAsync(user, tenancyId, cancellationToken);
        var record = await db.InsuranceRecords.AsNoTracking()
            .Where(item => item.TenancyId == tenancy.Id)
            .OrderByDescending(item => item.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        return record is null ? null : ToInsurance(record);
    }

    public async Task<InsuranceRecordDto> SaveInsuranceAsync(Guid tenancyId, SaveInsuranceRequest request, CancellationToken cancellationToken = default)
    {
        var user = await RequireUserAsync(cancellationToken);
        var tenancy = await RequireVisibleTenancyAsync(user, tenancyId, cancellationToken);
        if (user.Role == AppRoles.Landlord && request.Status == InsuranceStatus.Requested)
            throw new LeaseLedgerException("The tenant requests coverage. A landlord records an active policy or a cancellation.");
        if (user.Role == AppRoles.Tenant && request.Status == InsuranceStatus.Active)
            throw new LeaseLedgerException("Ask your landlord to confirm the policy is active.");
        if (!Enum.IsDefined(request.Status))
            throw new LeaseLedgerException("Choose a status.");

        var record = new InsuranceRecord
        {
            Id = Guid.NewGuid(),
            TenancyId = tenancy.Id,
            Status = request.Status,
            BrokerName = Required(request.BrokerName, "Enter the broker or insurer name."),
            PolicyNumber = Clean(request.PolicyNumber),
            LandlordListed = request.LandlordListed,
            Note = Clean(request.Note),
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.InsuranceRecords.Add(record);
        await db.SaveChangesAsync(cancellationToken);
        return ToInsurance(record);
    }

    public async Task<IReadOnlyList<LegalContactDto>> GetLegalContactsAsync(CancellationToken cancellationToken = default)
    {
        var user = await RequireRoleAsync(AppRoles.Landlord, cancellationToken);
        var contacts = await db.LegalContacts.AsNoTracking()
            .Where(item => item.LandlordUserId == user.UserId)
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);
        return contacts.Select(item => new LegalContactDto(item.Id, item.Name, item.Email, item.Firm)).ToList();
    }

    public async Task<LegalContactDto> SaveLegalContactAsync(SaveLegalContactRequest request, CancellationToken cancellationToken = default)
    {
        var user = await RequireRoleAsync(AppRoles.Landlord, cancellationToken);
        var contact = new LegalContact
        {
            Id = Guid.NewGuid(),
            LandlordUserId = user.UserId!.Value,
            Name = Required(request.Name, "Enter the contact's name."),
            Email = Required(request.Email, "Enter an email address."),
            Firm = Required(request.Firm, "Enter the firm.")
        };
        db.LegalContacts.Add(contact);
        await db.SaveChangesAsync(cancellationToken);
        return new LegalContactDto(contact.Id, contact.Name, contact.Email, contact.Firm);
    }

    public async Task ReferNoticeAsync(Guid noticeId, Guid contactId, CancellationToken cancellationToken = default)
    {
        var user = await RequireRoleAsync(AppRoles.Landlord, cancellationToken);
        var notice = await RequireNoticeAsync(user, noticeId, cancellationToken);
        var contact = await db.LegalContacts.FirstOrDefaultAsync(item => item.Id == contactId && item.LandlordUserId == user.UserId, cancellationToken)
            ?? throw new LeaseLedgerException("That legal contact was not found.");
        db.NoticeReferrals.Add(new NoticeReferral
        {
            Id = Guid.NewGuid(),
            NoticeRecordId = notice.Id,
            ContactName = contact.Name,
            ContactEmail = contact.Email,
            SentAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<PaymentInstructionsDto?> GetPaymentInstructionsAsync(Guid tenancyId, CancellationToken cancellationToken = default)
    {
        var user = await RequireUserAsync(cancellationToken);
        var tenancy = await VisibleTenancies(user)
            .Include(item => item.LedgerEntries)
            .FirstOrDefaultAsync(item => item.Id == tenancyId, cancellationToken);
        if (tenancy is null)
            return null;

        var landlord = await users.FindByIdAsync(tenancy.Unit.Property.LandlordUserId.ToString());
        var authorized = await db.PadAuthorizations.AsNoTracking().AnyAsync(item => item.TenancyId == tenancy.Id, cancellationToken);
        var due = LedgerMath.Balance(tenancy.LedgerEntries);
        return new PaymentInstructionsDto(
            due > 0 ? due : 0,
            landlord?.Email ?? "",
            $"LL-{tenancy.Id.ToString()[..8].ToUpperInvariant()}",
            authorized,
            "This app does not pull a bank debit. A signed authorization is only a record that the tenant agreed the landlord may set up a pre-authorized debit with their bank.");
    }

    public async Task AuthorizePadAsync(Guid tenancyId, AcknowledgeNoticeRequest request, CancellationToken cancellationToken = default)
    {
        var user = await RequireRoleAsync(AppRoles.Tenant, cancellationToken);
        var tenancy = await RequireVisibleTenancyAsync(user, tenancyId, cancellationToken);
        if (await db.PadAuthorizations.AnyAsync(item => item.TenancyId == tenancy.Id, cancellationToken))
            return;

        db.PadAuthorizations.Add(new PadAuthorization
        {
            Id = Guid.NewGuid(),
            TenancyId = tenancy.Id,
            SignedName = Required(request.SignedName, "Type your name."),
            SignedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<CheckoutStartDto> StartCheckoutAsync(StartCheckoutRequest request, CancellationToken cancellationToken = default)
    {
        if (!payments.IsConfigured)
            return new CheckoutStartDto(false, null, "Card checkout needs a Stripe secret key on the server. E-transfer instructions still work.");

        var user = await RequireUserAsync(cancellationToken);
        var kind = request.Kind.Trim().ToLowerInvariant();
        Guid landlordId;
        Guid? tenancyId = null;
        long cents;
        var description = kind switch
        {
            "plan" => "LeaseLedger notices",
            "form" => "Notice worksheet fee",
            _ => "Rent"
        };
        var returnUrl = string.IsNullOrWhiteSpace(request.ReturnUrl) ? "https://localhost:7208/billing" : request.ReturnUrl;

        if (kind == "portal")
        {
            if (user.Role != AppRoles.Landlord)
                throw new LeaseLedgerException("Only a landlord can open the billing portal.");
            var plan = await db.LandlordPlans.AsNoTracking().FirstOrDefaultAsync(item => item.LandlordUserId == user.UserId, cancellationToken);
            if (string.IsNullOrWhiteSpace(plan?.StripeCustomerId))
                return new CheckoutStartDto(true, null, "Start the notices plan before opening the billing portal.");

            var portal = await payments.CreatePortalAsync(plan.StripeCustomerId, returnUrl, cancellationToken);
            return portal is null
                ? new CheckoutStartDto(true, null, "Stripe did not return a billing portal.")
                : new CheckoutStartDto(true, portal, "Continue to Stripe to manage the notices plan.");
        }

        if (kind == "plan")
        {
            if (user.Role != AppRoles.Landlord)
                throw new LeaseLedgerException("Only a landlord can start the notices plan.");
            landlordId = user.UserId!.Value;
            cents = 2900;
        }
        else if (kind == "form")
        {
            if (user.Role != AppRoles.Landlord)
                throw new LeaseLedgerException("Only a landlord can pay a worksheet fee.");
            landlordId = user.UserId!.Value;
            cents = 6000;
        }
        else
        {
            if (request.TenancyId is null)
                throw new LeaseLedgerException("Choose a tenancy to pay.");
            var tenancy = await VisibleTenancies(user).Include(item => item.LedgerEntries)
                .FirstOrDefaultAsync(item => item.Id == request.TenancyId, cancellationToken)
                ?? throw new LeaseLedgerException("That tenancy was not found.");
            var due = LedgerMath.Balance(tenancy.LedgerEntries);
            if (due <= 0)
                throw new LeaseLedgerException("This ledger is not in arrears.");
            landlordId = tenancy.Unit.Property.LandlordUserId;
            tenancyId = tenancy.Id;
            cents = (long)decimal.Round(due * 100m, 0, MidpointRounding.AwayFromZero);
            kind = "rent";
        }

        var separator = returnUrl.Contains('?') ? "&" : "?";
        var url = await payments.CreateCheckoutAsync(new GatewayCheckout(
            kind,
            tenancyId,
            landlordId,
            cents,
            returnUrl + separator + "session_id={CHECKOUT_SESSION_ID}",
            returnUrl,
            description), cancellationToken);

        return url is null
            ? new CheckoutStartDto(false, null, "Stripe did not return a checkout page.")
            : new CheckoutStartDto(true, url, "Continue to Stripe to pay.");
    }

    public async Task<CheckoutStartDto> ConfirmCheckoutAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        await RequireUserAsync(cancellationToken);
        var status = await payments.GetCheckoutAsync(sessionId, cancellationToken);
        if (status is null || !status.Paid)
            return new CheckoutStartDto(payments.IsConfigured, null, "That checkout is not paid yet.");

        await ApplyPaidCheckoutAsync(sessionId, status, cancellationToken);
        return new CheckoutStartDto(true, null, "Payment recorded.");
    }

    public async Task FulfillCheckoutAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return;

        var status = await payments.GetCheckoutAsync(sessionId, cancellationToken);
        if (status is null || !status.Paid)
            return;

        await ApplyPaidCheckoutAsync(sessionId, status, cancellationToken);
    }

    public async Task<PlanDto> GetPlanAsync(CancellationToken cancellationToken = default)
    {
        var user = await RequireRoleAsync(AppRoles.Landlord, cancellationToken);
        var plan = await db.LandlordPlans.AsNoTracking().FirstOrDefaultAsync(item => item.LandlordUserId == user.UserId, cancellationToken);
        return new PlanDto(plan?.Status ?? "None", payments.IsConfigured);
    }

    private async Task ApplyPaidCheckoutAsync(string sessionId, GatewayCheckoutStatus status, CancellationToken cancellationToken)
    {
        var existing = await db.CardCheckouts.FirstOrDefaultAsync(item => item.StripeSessionId == sessionId, cancellationToken);
        if (existing?.Status == "Paid")
            return;

        var kind = status.Kind ?? "rent";
        Guid? tenancyId = Guid.TryParse(status.TenancyId, out var parsedTenancy) ? parsedTenancy : null;
        var landlordId = Guid.TryParse(status.LandlordUserId, out var parsedLandlord) ? parsedLandlord : Guid.Empty;
        var checkout = existing ?? new CardCheckout
        {
            Id = Guid.NewGuid(),
            StripeSessionId = sessionId,
            Kind = kind,
            Amount = status.AmountCents / 100m,
            TenancyId = tenancyId,
            LandlordUserId = landlordId
        };
        checkout.Status = "Paid";
        checkout.Kind = kind;
        checkout.Amount = status.AmountCents / 100m;
        checkout.TenancyId ??= tenancyId;
        if (landlordId != Guid.Empty)
            checkout.LandlordUserId = landlordId;

        if (kind == "rent" && checkout.TenancyId is Guid rentTenancyId && checkout.ReceiptNumber is null)
        {
            var tenancy = await db.Tenancies.Include(item => item.Unit).ThenInclude(unit => unit.Property)
                .FirstOrDefaultAsync(item => item.Id == rentTenancyId, cancellationToken);
            if (tenancy is not null)
            {
                var paidOn = DateOnly.FromDateTime(DateTime.Today);
                var entry = new LedgerEntry
                {
                    Id = Guid.NewGuid(),
                    TenancyId = tenancy.Id,
                    Kind = LedgerEntryKind.Payment,
                    PaymentMethod = PaymentMethod.Card,
                    Amount = checkout.Amount,
                    EffectiveDate = paidOn,
                    Description = "Payment (card)",
                    Note = "Stripe Checkout",
                    ReceiptNumber = NewReceiptNumber(paidOn)
                };
                db.LedgerEntries.Add(entry);
                checkout.LandlordUserId = tenancy.Unit.Property.LandlordUserId;
                checkout.ReceiptNumber = entry.ReceiptNumber;
            }
        }

        if (kind == "plan" && checkout.LandlordUserId != Guid.Empty)
        {
            var plan = await db.LandlordPlans.FirstOrDefaultAsync(item => item.LandlordUserId == checkout.LandlordUserId, cancellationToken);
            if (plan is null)
            {
                plan = new LandlordPlan { LandlordUserId = checkout.LandlordUserId };
                db.LandlordPlans.Add(plan);
            }

            plan.Status = "Active";
            plan.StripeCustomerId = status.CustomerId;
            plan.StripeSubscriptionId = status.SubscriptionId;
        }

        if (existing is null)
            db.CardCheckouts.Add(checkout);

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Tenancy> RequireOntarioTenancyAsync(Guid tenancyId, CancellationToken cancellationToken)
    {
        var user = await RequireRoleAsync(AppRoles.Landlord, cancellationToken);
        var tenancy = await RequireVisibleTenancyAsync(user, tenancyId, cancellationToken);
        RequireOntario(tenancy);
        return tenancy;
    }

    private async Task<NoticeRecord> RequireNoticeAsync(CurrentUser user, Guid noticeId, CancellationToken cancellationToken)
    {
        var notice = await db.NoticeRecords.Include(item => item.Tenancy).ThenInclude(tenancy => tenancy.Unit).ThenInclude(unit => unit.Property)
            .FirstOrDefaultAsync(item => item.Id == noticeId, cancellationToken)
            ?? throw new LeaseLedgerException("That worksheet was not found.");
        if (!CanSee(user, notice.Tenancy))
            throw new LeaseLedgerException("That worksheet was not found.");
        return notice;
    }

    private static TerminationPreviewDto ToTermination(TerminationResult result) =>
        new(result.IsValid, result.Errors, result.Warnings, result.NoticeDays, result.TerminationDate, result.Explanation, NoticeCopy.Disclaimer);

    private static N3Input ToN3Input(Tenancy tenancy, N3PreviewRequest request) => new(
        tenancy.Unit.IsCareHome,
        request.IncreasingRent,
        tenancy.RentAmount,
        request.ProposedRent,
        request.CurrentCareAndMeals,
        request.ProposedCareAndMeals,
        tenancy.StartDate,
        tenancy.LastRentIncreaseDate,
        request.NoticeDate,
        request.EffectiveDate,
        tenancy.Frequency,
        tenancy.RentDueDay,
        tenancy.Unit.FirstOccupiedAfterNovember152018);

    private static InsuranceRecordDto ToInsurance(InsuranceRecord record) =>
        new(record.Status, record.BrokerName, record.PolicyNumber, record.LandlordListed, record.Note, record.UpdatedAt);
}
