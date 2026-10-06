using System.Globalization;
using System.Text.Json;

namespace LeaseLedger.Application;

public sealed class AssistantToolRunner(ILeaseLedgerClient client, AssistantActionStore actions)
{
    private const int MaxJson = 12000;
    private static readonly JsonDocument EmptyArguments = JsonDocument.Parse("{}");
    private static readonly CultureInfo Canada = CultureInfo.GetCultureInfo("en-CA");

    public async Task<AssistantToolOutcome> RunAsync(string? name, string? argumentsJson, Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!TryArguments(argumentsJson, out var args, out var argumentError))
                return AssistantToolOutcome.Text(argumentError);

            return name switch
            {
                AssistantTools.ListPages => AssistantToolOutcome.Text(AssistantPages.Describe()),
                AssistantTools.GetDashboard => await Dashboard(cancellationToken),
                AssistantTools.GetTenancy => await Tenancy(args, cancellationToken),
                AssistantTools.GetNotices => await Notices(args, cancellationToken),
                AssistantTools.GetYearEnd => await YearEnd(args, cancellationToken),
                AssistantTools.GetInspections => await Inspections(args, cancellationToken),
                AssistantTools.GetInsurance => await Insurance(args, cancellationToken),
                AssistantTools.GetMaintenance => await Maintenance(cancellationToken),
                AssistantTools.GetProperties => await Properties(cancellationToken),
                AssistantTools.GetLegalContacts => AssistantToolOutcome.Text(Json(await client.GetLegalContactsAsync(cancellationToken))),
                AssistantTools.GetPaymentInstructions => await PaymentInstructions(args, cancellationToken),
                AssistantTools.GetPlan => AssistantToolOutcome.Text(Json(await client.GetPlanAsync(cancellationToken))),
                AssistantTools.ListPdfTemplates => AssistantToolOutcome.Text(Json(await client.GetPdfTemplatesAsync(cancellationToken))),
                AssistantTools.OpenPage => await OpenPage(args, cancellationToken),
                AssistantTools.PreviewN1 => await Preview<N1PreviewRequest, N1PreviewDto>(args, client.PreviewN1Async, cancellationToken),
                AssistantTools.PreviewN2 => await Preview<N2PreviewRequest, N2PreviewDto>(args, client.PreviewN2Async, cancellationToken),
                AssistantTools.PreviewN3 => await Preview<N3PreviewRequest, N3PreviewDto>(args, client.PreviewN3Async, cancellationToken),
                AssistantTools.PreviewN4 => await Preview<N4PreviewRequest, N4PreviewDto>(args, client.PreviewN4Async, cancellationToken),
                AssistantTools.PreviewN5 => await Preview<N5PreviewRequest, TerminationPreviewDto>(args, client.PreviewN5Async, cancellationToken),
                AssistantTools.PreviewN6 => await Preview<N6PreviewRequest, TerminationPreviewDto>(args, client.PreviewN6Async, cancellationToken),
                AssistantTools.PreviewN7 => await Preview<N7PreviewRequest, TerminationPreviewDto>(args, client.PreviewN7Async, cancellationToken),
                AssistantTools.ProposeSaveN1 => await ProposeNotice<N1PreviewRequest>(args, userId, "N1", static (id, request) => AssistantProposals.SaveN1(id, request), N1Summary, cancellationToken),
                AssistantTools.ProposeSaveN2 => await ProposeNotice<N2PreviewRequest>(args, userId, "N2", static (id, request) => AssistantProposals.SaveN2(id, request), N2Summary, cancellationToken),
                AssistantTools.ProposeSaveN3 => await ProposeNotice<N3PreviewRequest>(args, userId, "N3", static (id, request) => AssistantProposals.SaveN3(id, request), N3Summary, cancellationToken),
                AssistantTools.ProposeSaveN4 => await ProposeNotice<N4PreviewRequest>(args, userId, "N4", static (id, request) => AssistantProposals.SaveN4(id, request), N4Summary, cancellationToken),
                AssistantTools.ProposeSaveN5 => await ProposeNotice<N5PreviewRequest>(args, userId, "N5", static (id, request) => AssistantProposals.SaveN5(id, request), N5Summary, cancellationToken),
                AssistantTools.ProposeSaveN6 => await ProposeNotice<N6PreviewRequest>(args, userId, "N6", static (id, request) => AssistantProposals.SaveN6(id, request), N6Summary, cancellationToken),
                AssistantTools.ProposeSaveN7 => await ProposeNotice<N7PreviewRequest>(args, userId, "N7", static (id, request) => AssistantProposals.SaveN7(id, request), N7Summary, cancellationToken),
                AssistantTools.ProposePayment => await ProposePayment(args, userId, cancellationToken),
                AssistantTools.ProposeCharge => await ProposeCharge(args, userId, cancellationToken),
                AssistantTools.ProposeMaintenance => await ProposeMaintenance(args, userId, cancellationToken),
                AssistantTools.ProposeInsurance => await ProposeInsurance(args, userId, cancellationToken),
                AssistantTools.ProposeInspection => await ProposeInspection(args, userId, cancellationToken),
                AssistantTools.ProposeFillPdf => await ProposeFillPdf(args, userId, cancellationToken),
                _ => AssistantToolOutcome.Text("That tool is not available.")
            };
        }
        catch (LeaseLedgerException exception)
        {
            return AssistantToolOutcome.Text(exception.Message);
        }
        catch (JsonException)
        {
            return AssistantToolOutcome.Text("Check the dates, amounts, and choices on that form.");
        }
    }

    private async Task<AssistantToolOutcome> Dashboard(CancellationToken cancellationToken)
    {
        var dashboard = await client.GetDashboardAsync(cancellationToken);
        if (dashboard.Tenancies.Count <= 40)
            return AssistantToolOutcome.Text(Json(dashboard));

        var trimmed = dashboard with { Tenancies = dashboard.Tenancies.Take(40).ToArray() };
        return AssistantToolOutcome.Text("Showing the first 40 tenancies. " + Json(trimmed));
    }

    private async Task<AssistantToolOutcome> Tenancy(JsonElement args, CancellationToken cancellationToken)
    {
        if (!TryTenancy(args, out var tenancyId, out var error))
            return AssistantToolOutcome.Text(error);

        var detail = await client.GetTenancyAsync(tenancyId, cancellationToken);
        if (detail is null)
            return AssistantToolOutcome.Text("That tenancy was not found.");

        var lines = detail.Lines.TakeLast(12).ToArray();
        return AssistantToolOutcome.Text(Json(detail with { Lines = lines }));
    }

    private async Task<AssistantToolOutcome> Notices(JsonElement args, CancellationToken cancellationToken)
    {
        if (!TryTenancy(args, out var tenancyId, out var error))
            return AssistantToolOutcome.Text(error);

        var notices = await client.GetNoticesAsync(tenancyId, cancellationToken);
        return AssistantToolOutcome.Text(Json(notices.TakeLast(20).ToArray()));
    }

    private async Task<AssistantToolOutcome> YearEnd(JsonElement args, CancellationToken cancellationToken)
    {
        if (!TryTenancy(args, out var tenancyId, out var error) || !TryYear(args, out var year, out error))
            return AssistantToolOutcome.Text(error);

        var yearEnd = await client.GetYearEndAsync(tenancyId, year, cancellationToken);
        if (yearEnd is null)
            return AssistantToolOutcome.Text("That year-end was not found.");

        return AssistantToolOutcome.Text(Json(yearEnd with { Lines = yearEnd.Lines.TakeLast(12).ToArray() }));
    }

    private async Task<AssistantToolOutcome> Inspections(JsonElement args, CancellationToken cancellationToken)
    {
        if (await TryUnit(args, cancellationToken) is not UnitLookup unit)
            return AssistantToolOutcome.Text("Give a unitId or a tenancyId.");

        if (unit.Error is not null)
            return AssistantToolOutcome.Text(unit.Error);

        var log = await client.GetInspectionsAsync(unit.UnitId, cancellationToken);
        if (log is null)
            return AssistantToolOutcome.Text("That unit was not found.");

        return AssistantToolOutcome.Text(Json(log with { Inspections = log.Inspections.TakeLast(12).ToArray() }));
    }

    private async Task<AssistantToolOutcome> Insurance(JsonElement args, CancellationToken cancellationToken)
    {
        if (!TryTenancy(args, out var tenancyId, out var error))
            return AssistantToolOutcome.Text(error);

        var insurance = await client.GetInsuranceAsync(tenancyId, cancellationToken);
        return insurance is null
            ? AssistantToolOutcome.Text("No insurance is recorded for that tenancy.")
            : AssistantToolOutcome.Text(Json(insurance));
    }

    private async Task<AssistantToolOutcome> Maintenance(CancellationToken cancellationToken)
    {
        var requests = await client.GetMaintenanceAsync(cancellationToken);
        var trimmed = requests.Take(20).Select(item => item with { Events = item.Events.TakeLast(5).ToArray() }).ToArray();
        var note = requests.Count > 20 ? "Showing the first 20 maintenance requests. " : "";
        return AssistantToolOutcome.Text(note + Json(trimmed));
    }

    private async Task<AssistantToolOutcome> Properties(CancellationToken cancellationToken)
    {
        var properties = await client.GetPropertiesAsync(cancellationToken);
        if (properties.Count <= 40)
            return AssistantToolOutcome.Text(Json(properties));

        return AssistantToolOutcome.Text("Showing the first 40 properties. " + Json(properties.Take(40).ToArray()));
    }

    private async Task<AssistantToolOutcome> PaymentInstructions(JsonElement args, CancellationToken cancellationToken)
    {
        if (!TryTenancy(args, out var tenancyId, out var error))
            return AssistantToolOutcome.Text(error);

        var instructions = await client.GetPaymentInstructionsAsync(tenancyId, cancellationToken);
        return instructions is null
            ? AssistantToolOutcome.Text("Payment instructions were not found.")
            : AssistantToolOutcome.Text(Json(instructions));
    }

    private async Task<AssistantToolOutcome> OpenPage(JsonElement args, CancellationToken cancellationToken)
    {
        var page = OptionalString(args, "page");
        if (page is null)
            return AssistantToolOutcome.Text("Name a page from list_pages.");

        if (!TryOptionalGuid(args, "tenancyId", out var tenancyId, out var error)
            || !TryOptionalGuid(args, "unitId", out var unitId, out error)
            || !TryOptionalInt(args, "year", out var year, out error)
            || !TryOptionalGuid(args, "templateId", out var templateId, out error))
            return AssistantToolOutcome.Text(error!);

        if (page.Equals("inspections", StringComparison.OrdinalIgnoreCase) && unitId is null && tenancyId is Guid tenancy)
        {
            var detail = await client.GetTenancyAsync(tenancy, cancellationToken);
            if (detail is null)
                return AssistantToolOutcome.Text("That tenancy was not found.");
            unitId = detail.UnitId;
        }

        var route = new AssistantRouteArgs(tenancyId, unitId, year, templateId, OptionalString(args, "receiptNumber"));
        if (!AssistantPages.TryOpen(page, route, out var link))
            return AssistantToolOutcome.Text("That page is not available. Use list_pages and supply the required ids.");

        if (tenancyId is not null && link.TenancyId is null)
            link = link with { TenancyId = tenancyId };

        return new AssistantToolOutcome($"Opening {link.Label}.", link, null);
    }

    private async Task<AssistantToolOutcome> Preview<TRequest, TResult>(
        JsonElement args,
        Func<Guid, TRequest, CancellationToken, Task<TResult>> load,
        CancellationToken cancellationToken)
        where TRequest : class
    {
        if (!TryTenancy(args, out var tenancyId, out var error) || !TryBind(args, out TRequest request, out error))
            return AssistantToolOutcome.Text(error);

        var result = await load(tenancyId, request, cancellationToken);
        return AssistantToolOutcome.Text(Json(result!));
    }

    private async Task<AssistantToolOutcome> ProposeNotice<TRequest>(
        JsonElement args,
        Guid userId,
        string kind,
        Func<Guid, TRequest, Func<ILeaseLedgerClient, CancellationToken, Task<AssistantCommitResult>>> execute,
        Func<TenancyDetailDto, TRequest, string> summaryFor,
        CancellationToken cancellationToken)
        where TRequest : class
    {
        if (!TryTenancy(args, out var tenancyId, out var error) || !TryBind(args, out TRequest request, out error))
            return AssistantToolOutcome.Text(error);

        ClipNotice(request);
        if (DateProblem(request) is string dateError)
            return AssistantToolOutcome.Text(dateError);

        var detail = await client.GetTenancyAsync(tenancyId, cancellationToken);
        if (detail is null)
            return AssistantToolOutcome.Text("That tenancy was not found.");

        var summary = summaryFor(detail, request);
        var proposal = actions.Stage(userId, summary, execute(tenancyId, request));
        return Staged(kind, summary, proposal);
    }

    private async Task<AssistantToolOutcome> ProposePayment(JsonElement args, Guid userId, CancellationToken cancellationToken)
    {
        if (!TryTenancy(args, out var tenancyId, out var error) || !TryBind(args, out RecordPaymentRequest request, out error))
            return AssistantToolOutcome.Text(error);
        if (request.Amount <= 0)
            return AssistantToolOutcome.Text("The amount must be greater than zero.");
        if (DateError(request.EffectiveDate, "effectiveDate") is string dateError)
            return AssistantToolOutcome.Text(dateError);

        request.Note = Clip(request.Note, 500);
        var detail = await client.GetTenancyAsync(tenancyId, cancellationToken);
        if (detail is null)
            return AssistantToolOutcome.Text("That tenancy was not found.");

        var summary = $"Record a {Money(request.Amount)} {request.Method} payment on {request.EffectiveDate:yyyy-MM-dd} for {detail.TenantName}, unit {detail.UnitLabel}.";
        var proposal = actions.Stage(userId, summary, AssistantProposals.Payment(tenancyId, request));
        return Staged("payment", summary, proposal);
    }

    private async Task<AssistantToolOutcome> ProposeCharge(JsonElement args, Guid userId, CancellationToken cancellationToken)
    {
        if (!TryTenancy(args, out var tenancyId, out var error) || !TryBind(args, out RecordChargeRequest request, out error))
            return AssistantToolOutcome.Text(error);
        if (request.Amount <= 0)
            return AssistantToolOutcome.Text("The amount must be greater than zero.");
        if (DateError(request.EffectiveDate, "effectiveDate") is string dateError)
            return AssistantToolOutcome.Text(dateError);

        request.Note = Clip(request.Note, 500);
        var detail = await client.GetTenancyAsync(tenancyId, cancellationToken);
        if (detail is null)
            return AssistantToolOutcome.Text("That tenancy was not found.");

        var summary = $"Record a {Money(request.Amount)} {request.Category} charge on {request.EffectiveDate:yyyy-MM-dd} for {detail.TenantName}, unit {detail.UnitLabel}.";
        var proposal = actions.Stage(userId, summary, AssistantProposals.Charge(tenancyId, request));
        return Staged("charge", summary, proposal);
    }

    private async Task<AssistantToolOutcome> ProposeMaintenance(JsonElement args, Guid userId, CancellationToken cancellationToken)
    {
        if (!TryTenancy(args, out var tenancyId, out var error) || !TryBind(args, out CreateMaintenanceRequest request, out error))
            return AssistantToolOutcome.Text(error);

        request.TenancyId = tenancyId;
        request.Title = Clip(request.Title, 200);
        request.Description = Clip(request.Description, 2000);
        if (request.Title.Length == 0)
            return AssistantToolOutcome.Text("A maintenance request needs a title.");

        var detail = await client.GetTenancyAsync(tenancyId, cancellationToken);
        if (detail is null)
            return AssistantToolOutcome.Text("That tenancy was not found.");

        var summary = $"Open a maintenance request “{request.Title}” for {detail.TenantName}, unit {detail.UnitLabel}.";
        var proposal = actions.Stage(userId, summary, AssistantProposals.Maintenance(request));
        return Staged("maintenance", summary, proposal);
    }

    private async Task<AssistantToolOutcome> ProposeInsurance(JsonElement args, Guid userId, CancellationToken cancellationToken)
    {
        if (!TryTenancy(args, out var tenancyId, out var error) || !TryBind(args, out SaveInsuranceRequest request, out error))
            return AssistantToolOutcome.Text(error);

        request.BrokerName = Clip(request.BrokerName, 200);
        request.PolicyNumber = Clip(request.PolicyNumber, 80);
        request.Note = Clip(request.Note, 500);
        var detail = await client.GetTenancyAsync(tenancyId, cancellationToken);
        if (detail is null)
            return AssistantToolOutcome.Text("That tenancy was not found.");

        var summary = $"Save insurance for {detail.TenantName}, unit {detail.UnitLabel}: {request.Status}, broker {request.BrokerName}.";
        var proposal = actions.Stage(userId, summary, AssistantProposals.Insurance(tenancyId, request));
        return Staged("insurance", summary, proposal);
    }

    private async Task<AssistantToolOutcome> ProposeInspection(JsonElement args, Guid userId, CancellationToken cancellationToken)
    {
        if (!TryBind(args, out RecordInspectionRequest request, out var error))
            return AssistantToolOutcome.Text(error);
        if (DateError(request.InspectedOn, "inspectedOn") is string dateError)
            return AssistantToolOutcome.Text(dateError);

        request.Summary = Clip(request.Summary, 200);
        request.Notes = Clip(request.Notes, 2000);
        if (request.Summary.Length == 0)
            return AssistantToolOutcome.Text("An inspection needs a summary.");

        var unit = await TryUnit(args, cancellationToken);
        if (unit is null || unit.Error is not null || unit.UnitId == Guid.Empty)
            return AssistantToolOutcome.Text(unit?.Error ?? "Give a unitId or a tenancyId.");

        var log = await client.GetInspectionsAsync(unit.UnitId, cancellationToken);
        if (log is null)
            return AssistantToolOutcome.Text("That unit was not found.");

        var summary = $"Record an inspection on {request.InspectedOn:yyyy-MM-dd} for {log.Street}, unit {log.UnitLabel}: {request.Summary}.";
        var proposal = actions.Stage(userId, summary, AssistantProposals.Inspection(unit.UnitId, request));
        return Staged("inspection", summary, proposal);
    }

    private async Task<AssistantToolOutcome> ProposeFillPdf(JsonElement args, Guid userId, CancellationToken cancellationToken)
    {
        if (!TryTenancy(args, out var tenancyId, out var error) || !TryRequiredGuid(args, "templateId", out var templateId, out error))
            return AssistantToolOutcome.Text(error);

        var template = await client.GetPdfTemplateAsync(templateId, cancellationToken);
        var detail = await client.GetTenancyAsync(tenancyId, cancellationToken);
        if (template is null || detail is null)
            return AssistantToolOutcome.Text("That form or tenancy was not found.");

        var summary = $"Fill {template.Name} for {detail.TenantName}, unit {detail.UnitLabel}, and download it.";
        var proposal = actions.Stage(userId, summary, AssistantProposals.FillPdf(templateId, tenancyId));
        return Staged("pdf", summary, proposal);
    }

    private static AssistantToolOutcome Staged(string kind, string summary, AssistantProposal proposal) =>
        new($"Staged a {kind}. Ask the user to press Confirm. Do not say this is saved yet. {summary}", null, proposal);

    private async Task<UnitLookup?> TryUnit(JsonElement args, CancellationToken cancellationToken)
    {
        if (!TryOptionalGuid(args, "unitId", out var unitId, out var error))
            return new UnitLookup(Guid.Empty, error);
        if (!TryOptionalGuid(args, "tenancyId", out var tenancyId, out error))
            return new UnitLookup(Guid.Empty, error);
        if (unitId is Guid id)
            return new UnitLookup(id, null);
        if (tenancyId is not Guid tenancy)
            return null;

        var detail = await client.GetTenancyAsync(tenancy, cancellationToken);
        return detail is null
            ? new UnitLookup(Guid.Empty, "That tenancy was not found.")
            : new UnitLookup(detail.UnitId, null);
    }

    private static bool TryArguments(string? argumentsJson, out JsonElement args, out string error)
    {
        error = "";
        if (string.IsNullOrWhiteSpace(argumentsJson))
        {
            args = EmptyArguments.RootElement;
            return true;
        }

        try
        {
            using var document = JsonDocument.Parse(argumentsJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                args = default;
                error = "The arguments were not an object.";
                return false;
            }

            args = document.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            args = default;
            error = "The arguments were not valid JSON.";
            return false;
        }
    }

    private static bool TryTenancy(JsonElement args, out Guid tenancyId, out string error) =>
        TryRequiredGuid(args, "tenancyId", out tenancyId, out error);

    private static bool TryYear(JsonElement args, out int year, out string error)
    {
        year = 0;
        if (!TryOptionalInt(args, "year", out var value, out error))
            return false;
        if (value is not int given || given < 2000 || given > 2100)
        {
            error = "year must be between 2000 and 2100.";
            return false;
        }

        year = given;
        error = "";
        return true;
    }

    private static bool TryRequiredGuid(JsonElement args, string name, out Guid id, out string error)
    {
        id = Guid.Empty;
        if (!TryOptionalGuid(args, name, out var parsed, out error))
            return false;
        if (parsed is not Guid value)
        {
            error = $"{name} is required.";
            return false;
        }

        id = value;
        return true;
    }

    private static bool TryOptionalGuid(JsonElement args, string name, out Guid? id, out string error)
    {
        id = null;
        error = "";
        if (!args.TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return true;
        if (value.ValueKind == JsonValueKind.String && Guid.TryParse(value.GetString(), out var parsed) && parsed != Guid.Empty)
        {
            id = parsed;
            return true;
        }

        error = $"{name} must be an id.";
        return false;
    }

    private static bool TryOptionalInt(JsonElement args, string name, out int? value, out string error)
    {
        value = null;
        error = "";
        if (!args.TryGetProperty(name, out var node) || node.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return true;
        if (node.TryGetInt32(out var number) || (node.ValueKind == JsonValueKind.String && int.TryParse(node.GetString(), out number)))
        {
            value = number;
            return true;
        }

        error = $"{name} must be a number.";
        return false;
    }

    private static bool TryBind<T>(JsonElement args, out T request, out string error) where T : class
    {
        try
        {
            var bound = args.Deserialize<T>(LeaseLedgerJson.Options);
            if (bound is null)
            {
                request = null!;
                error = "The form fields were empty.";
                return false;
            }

            request = bound;
            error = "";
            return true;
        }
        catch (JsonException)
        {
            request = null!;
            error = "Check the dates, amounts, and choices on that form.";
            return false;
        }
    }

    private static string? OptionalString(JsonElement args, string name)
    {
        if (!args.TryGetProperty(name, out var node) || node.ValueKind != JsonValueKind.String)
            return null;

        var text = node.GetString()?.Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    private static void ClipNotice<T>(T request)
    {
        switch (request)
        {
            case N5PreviewRequest n5:
                n5.Details = Clip(n5.Details, 2000);
                break;
            case N6PreviewRequest n6:
                n6.Details = Clip(n6.Details, 2000);
                break;
            case N7PreviewRequest n7:
                n7.Details = Clip(n7.Details, 2000);
                break;
        }
    }

    private static string? DateProblem<T>(T request) => request switch
    {
        N1PreviewRequest n1 => DateError(n1.NoticeDate, "noticeDate") ?? DateError(n1.ProposedEffectiveDate, "proposedEffectiveDate"),
        N2PreviewRequest n2 => DateError(n2.NoticeDate, "noticeDate") ?? DateError(n2.ProposedEffectiveDate, "proposedEffectiveDate"),
        N3PreviewRequest n3 => DateError(n3.NoticeDate, "noticeDate") ?? DateError(n3.EffectiveDate, "effectiveDate"),
        N4PreviewRequest n4 => DateError(n4.NoticeDate, "noticeDate"),
        N5PreviewRequest n5 => DateError(n5.NoticeDate, "noticeDate") ?? OptionalDate(n5.FirstNoticeDate, "firstNoticeDate") ?? SecondNoticeDate(n5),
        N6PreviewRequest n6 => DateError(n6.NoticeDate, "noticeDate"),
        N7PreviewRequest n7 => DateError(n7.NoticeDate, "noticeDate"),
        _ => null
    };

    private static string? SecondNoticeDate(N5PreviewRequest request)
    {
        if (!request.SecondNotice)
            return null;
        if (request.FirstNoticeDate is null || request.FirstNoticeDate.Value.Year < 2000)
            return "firstNoticeDate is required when secondNotice is true.";
        return null;
    }

    private static string? OptionalDate(DateOnly? date, string name) =>
        date is DateOnly value ? DateError(value, name) : null;

    private static string? DateError(DateOnly date, string name) =>
        date.Year is < 2000 or > 2100 ? $"{name} must be a real date." : null;

    private static string Clip(string? value, int max)
    {
        var text = value?.Trim() ?? "";
        return text.Length <= max ? text : text[..max];
    }

    private static string Money(decimal amount) => amount.ToString("C", Canada);

    private static string Json(object value)
    {
        var json = JsonSerializer.Serialize(value, LeaseLedgerJson.Options);
        return json.Length <= MaxJson ? json : json[..MaxJson];
    }

    private static string N1Summary(TenancyDetailDto detail, N1PreviewRequest request) =>
        $"Save the N1 for {detail.TenantName}, unit {detail.UnitLabel}, notice {request.NoticeDate:yyyy-MM-dd}, effective {request.ProposedEffectiveDate:yyyy-MM-dd}.";

    private static string N2Summary(TenancyDetailDto detail, N2PreviewRequest request) =>
        $"Save the N2 for {detail.TenantName}, unit {detail.UnitLabel}, notice {request.NoticeDate:yyyy-MM-dd}, effective {request.ProposedEffectiveDate:yyyy-MM-dd}, proposed rent {Money(request.ProposedRent)}.";

    private static string N3Summary(TenancyDetailDto detail, N3PreviewRequest request) =>
        $"Save the N3 for {detail.TenantName}, unit {detail.UnitLabel}, notice {request.NoticeDate:yyyy-MM-dd}, effective {request.EffectiveDate:yyyy-MM-dd}.";

    private static string N4Summary(TenancyDetailDto detail, N4PreviewRequest request) =>
        $"Save the N4 for {detail.TenantName}, unit {detail.UnitLabel}, notice {request.NoticeDate:yyyy-MM-dd}.";

    private static string N5Summary(TenancyDetailDto detail, N5PreviewRequest request) =>
        $"Save the N5 for {detail.TenantName}, unit {detail.UnitLabel}, notice {request.NoticeDate:yyyy-MM-dd}, reason {request.Reason}.";

    private static string N6Summary(TenancyDetailDto detail, N6PreviewRequest request) =>
        $"Save the N6 for {detail.TenantName}, unit {detail.UnitLabel}, notice {request.NoticeDate:yyyy-MM-dd}, reason {request.Reason}.";

    private static string N7Summary(TenancyDetailDto detail, N7PreviewRequest request) =>
        $"Save the N7 for {detail.TenantName}, unit {detail.UnitLabel}, notice {request.NoticeDate:yyyy-MM-dd}.";

    private sealed record UnitLookup(Guid UnitId, string? Error);
}
