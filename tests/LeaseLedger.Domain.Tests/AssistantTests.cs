using LeaseLedger.Application;
using LeaseLedger.Domain;

namespace LeaseLedger.Domain.Tests;

public class AssistantTests
{
    private static readonly Guid TenancyId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UnitId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid TemplateId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static TenancySummaryDto Tenancy(Guid id, string name, string unit) =>
        new(id, "245 James Ave", unit, name, 2000m, 0m, [], Guid.Parse("44444444-4444-4444-4444-444444444444"), "Toronto");

    [Fact]
    public void Name_lookup_offers_page_buttons_without_navigating()
    {
        var jason = Tenancy(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "Jason Thoams", "2");
        var demo = Tenancy(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), "Demo Tenant", "4");
        var units = new Dictionary<Guid, Guid> { [jason.Id] = UnitId };
        var reply = new AssistantReply("Opening the tenancy.", $"/tenancies/{jason.Id}", false, jason.Id, "Open tenancy", null, null);

        var enriched = AssistantContext.Enrich(reply, "Jason", [jason, demo], units, 2026);

        Assert.Null(enriched.Href);
        Assert.Contains("Jason Thoams", enriched.Text);
        Assert.Equal(
            ["Ledger", "Payments", "Insurance", "Inspections", "Year-end", "N1", "N4", "N5"],
            enriched.Actions!.Select(action => action.Label).ToArray());
        Assert.Contains(enriched.Actions!, action => action.Href == $"/tenancies/{jason.Id}");
        Assert.Contains(enriched.Actions!, action => action.Href == $"/units/{UnitId}/inspections");
        Assert.DoesNotContain(enriched.Actions!, action => action.Href.Contains(demo.Id.ToString()));
    }

    [Fact]
    public void Shared_name_keeps_a_button_group_for_each_match()
    {
        var first = Tenancy(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "Jason Alvarez", "1");
        var second = Tenancy(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), "Jason Cho", "3");

        var matches = AssistantContext.Find("jason", [first, second]);

        Assert.Equal([first.Id, second.Id], matches.Select(tenancy => tenancy.Id).ToArray());
        var enriched = AssistantContext.Enrich(
            new AssistantReply("Which tenancy?", null, false, null, null, null, null),
            "jason",
            [first, second],
            new Dictionary<Guid, Guid>(),
            2026);
        Assert.Equal("Which tenancy?", enriched.Text);
        Assert.Equal(["Tenants", "Units"], enriched.Actions!.Select(action => action.Group).Distinct().ToArray());
        Assert.Contains(enriched.Actions!, action => action.Label == "Jason Alvarez, unit 1" && action.Href.Length == 0 && action.Send!.StartsWith("Jason Alvarez"));
        Assert.Contains(enriched.Actions!, action => action.Label == "Jason Cho, unit 3");
        Assert.Contains(enriched.Actions!, action => action.Label == "Unit 1" && action.Group == "Units");
        Assert.Contains(enriched.Actions!, action => action.Label == "Unit 3" && action.Group == "Units");
    }

    [Fact]
    public void Unnamed_rent_question_offers_each_tenant_and_each_unit()
    {
        var jason = Tenancy(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "Jason Thoams", "2");
        var demo = Tenancy(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), "Demo Tenant", "2");
        var reply = new AssistantReply("Which tenancy? Jason Thoams (unit 2) or Demo Tenant (unit 2).", null, false, null, null, null, null);

        var enriched = AssistantContext.Enrich(reply, "Tenant is going to be late with rent", [jason, demo], new Dictionary<Guid, Guid>(), 2026);

        Assert.Equal("Which tenancy?", enriched.Text);
        Assert.Null(enriched.Href);
        var jasonButton = Assert.Single(enriched.Actions!, action => action.Label == "Jason Thoams, unit 2");
        Assert.Equal("Tenants", jasonButton.Group);
        Assert.Equal("", jasonButton.Href);
        Assert.StartsWith("Jason Thoams, unit 2.", jasonButton.Send);
        Assert.Contains("late with rent", jasonButton.Send);
        Assert.Contains(enriched.Actions!, action => action.Label == "Demo Tenant, unit 2" && action.Group == "Tenants");
        var unit = Assert.Single(enriched.Actions!, action => action.Group == "Units");
        Assert.Equal("Unit 2", unit.Label);
        Assert.Contains("unit 2", unit.Send);
    }

    [Fact]
    public void Rent_agreement_lists_the_document_the_tenancy_and_related_pages()
    {
        var jason = Tenancy(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "Jason Thoams", "2");
        var demo = Tenancy(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), "Demo Tenant", "4");
        var lease = new PdfTemplateSummaryDto(TemplateId, "Ontario Standard Lease", "lease.pdf", 4, DateTimeOffset.Parse("2026-10-05T12:00:00Z"), null);
        var notice = new PdfTemplateSummaryDto(Guid.Parse("55555555-5555-5555-5555-555555555555"), "N1 form", "n1.pdf", 2, DateTimeOffset.Parse("2026-10-05T12:00:00Z"), NoticeKind.N1);
        var reply = new AssistantReply("Opening payments.", $"/tenancies/{jason.Id}#payments", false, jason.Id, "Open payments", null, null);

        var enriched = AssistantContext.Enrich(
            reply,
            "Jason's rent agreement",
            [jason, demo],
            new Dictionary<Guid, Guid> { [jason.Id] = UnitId },
            2026,
            [lease, notice]);

        Assert.Contains("rent agreement", enriched.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Equal($"/tenancies/{jason.Id}", enriched.Href);
        Assert.Equal("Tenancy details", enriched.LinkLabel);
        Assert.Contains(enriched.Actions!, action => action.Label == "Ontario Standard Lease" && action.Href == $"/forms/{TemplateId}" && action.Group == "Documents");
        Assert.Contains(enriched.Actions!, action => action.Label == "Tenancy details" && action.Href == $"/tenancies/{jason.Id}");
        Assert.Contains(enriched.Actions!, action => action.Label == "Payments");
        Assert.Contains(enriched.Actions!, action => action.Label == "Year-end");
        Assert.Contains(enriched.Actions!, action => action.Label == "Insurance");
        Assert.DoesNotContain(enriched.Actions!, action => action.Href.Contains(notice.Id.ToString()));
        Assert.DoesNotContain(enriched.Actions!, action => action.Href.Contains(demo.Id.ToString()));
    }

    [Fact]
    public void Named_page_still_navigates_and_keeps_the_buttons()
    {
        var jason = Tenancy(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "Jason Thoams", "2");
        var href = $"/notices/n5/{jason.Id}";
        var reply = new AssistantReply("Opening the N5.", href, false, jason.Id, "Open N5", null, null);

        var enriched = AssistantContext.Enrich(reply, "n5 jason", [jason], new Dictionary<Guid, Guid>(), 2026);

        Assert.Equal(href, enriched.Href);
        Assert.Contains(enriched.Actions!, action => action.Label == "Payments");
    }

    [Fact]
    public void Catalog_emits_only_known_routes()
    {
        var args = new AssistantRouteArgs(TenancyId, UnitId, 2026, TemplateId, "R-20261005-ABCDEF");
        var expected = new Dictionary<string, string>
        {
            ["home"] = "/",
            ["properties"] = "/properties",
            ["tenancy"] = $"/tenancies/{TenancyId}",
            ["payments"] = $"/tenancies/{TenancyId}#payments",
            ["n1"] = $"/notices/n1/{TenancyId}",
            ["n2"] = $"/notices/n2/{TenancyId}",
            ["n3"] = $"/notices/n3/{TenancyId}",
            ["n4"] = $"/notices/n4/{TenancyId}",
            ["n5"] = $"/notices/n5/{TenancyId}",
            ["n6"] = $"/notices/n6/{TenancyId}",
            ["n7"] = $"/notices/n7/{TenancyId}",
            ["year-end"] = $"/tenancies/{TenancyId}/year/2026",
            ["inspections"] = $"/units/{UnitId}/inspections",
            ["insurance"] = $"/tenancies/{TenancyId}/insurance",
            ["maintenance"] = "/maintenance",
            ["legal"] = "/legal",
            ["billing"] = "/billing",
            ["forms"] = "/forms",
            ["form"] = $"/forms/{TemplateId}",
            ["receipt"] = "/receipts/R-20261005-ABCDEF"
        };

        Assert.Equal(expected.Keys.OrderBy(key => key), AssistantPages.All.Select(page => page.Key).OrderBy(key => key));
        foreach (var (key, href) in expected)
        {
            Assert.True(AssistantPages.TryOpen(key, args, out var link));
            Assert.Equal(href, link.Href);
            Assert.DoesNotContain("://", link.Href);
        }
    }

    [Theory]
    [InlineData("https://evil.example/steal")]
    [InlineData("/tenancies/11111111-1111-1111-1111-111111111111")]
    [InlineData("tenancy/../admin")]
    [InlineData("")]
    public void Unknown_pages_are_rejected(string page)
    {
        var args = new AssistantRouteArgs(TenancyId, UnitId, 2026, TemplateId, "R-20261005-ABCDEF");
        Assert.False(AssistantPages.TryOpen(page, args, out _));
    }

    [Fact]
    public void Routes_that_need_ids_reject_a_missing_or_unsafe_value()
    {
        Assert.False(AssistantPages.TryOpen("payments", new AssistantRouteArgs(null, UnitId, 2026, TemplateId, "R-1"), out _));
        Assert.False(AssistantPages.TryOpen("tenancy", new AssistantRouteArgs(Guid.Empty, null, null, null, null), out _));
        Assert.False(AssistantPages.TryOpen("inspections", new AssistantRouteArgs(TenancyId, null, 2026, TemplateId, "R-1"), out _));
        Assert.False(AssistantPages.TryOpen("year-end", new AssistantRouteArgs(TenancyId, UnitId, null, TemplateId, "R-1"), out _));
        Assert.False(AssistantPages.TryOpen("year-end", new AssistantRouteArgs(TenancyId, UnitId, 1999, TemplateId, "R-1"), out _));
        Assert.False(AssistantPages.TryOpen("form", new AssistantRouteArgs(TenancyId, UnitId, 2026, null, "R-1"), out _));
        Assert.False(AssistantPages.TryOpen("receipt", new AssistantRouteArgs(TenancyId, UnitId, 2026, TemplateId, "../admin"), out _));
        Assert.False(AssistantPages.TryOpen("receipt", new AssistantRouteArgs(TenancyId, UnitId, 2026, TemplateId, "R 1"), out _));
    }

    [Theory]
    [InlineData("the kitchen sink is broken", true)]
    [InlineData("something is wrong with the heat", true)]
    [InlineData("the toilet won't flush", true)]
    [InlineData("please fix the leaking tap", true)]
    [InlineData("the window will not latch", true)]
    [InlineData("maintenance", false)]
    [InlineData("open maintenance", false)]
    [InlineData("show me payments", false)]
    [InlineData("n5 for damage", false)]
    [InlineData("the tenant hasn't paid rent", false)]
    [InlineData("hello there", false)]
    public void Broken_or_wrong_reports_are_maintenance_and_page_names_are_not(string message, bool report)
    {
        Assert.Equal(report, MaintenanceChat.IsReport(message));
    }

    [Fact]
    public async Task A_broken_report_opens_maintenance_and_a_landlord_task()
    {
        var tenant = Tenancy(TenancyId, "Demo Tenant", "2");
        var client = new RecordingClient();

        var reply = await MaintenanceChat.ReplyAsync(client, "the kitchen sink is broken", [tenant], null);

        Assert.Equal(1, client.MaintenanceCreates);
        Assert.Equal(TenancyId, client.MaintenanceRequest!.TenancyId);
        Assert.Equal("The kitchen sink is broken", client.MaintenanceRequest.Title);
        Assert.Equal("the kitchen sink is broken", client.MaintenanceRequest.Description);
        Assert.True(reply.LedgerChanged);
        Assert.Equal("/maintenance", reply.Href);
        Assert.Contains("landlord", reply.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("to-do", reply.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task An_unnamed_problem_asks_which_tenancy_before_creating_anything()
    {
        var jason = Tenancy(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "Jason Thoams", "2");
        var demo = Tenancy(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), "Demo Tenant", "4");
        var client = new RecordingClient();

        var reply = await MaintenanceChat.ReplyAsync(client, "something is wrong with the heat", [jason, demo], null);

        Assert.Equal(0, client.MaintenanceCreates);
        Assert.False(reply.LedgerChanged);
        Assert.Equal("Which tenancy?", reply.Text);
        var enriched = AssistantContext.Enrich(reply, "something is wrong with the heat", [jason, demo], new Dictionary<Guid, Guid>(), 2026);
        Assert.Equal("Which tenancy?", enriched.Text);
        Assert.Contains(enriched.Actions!, action => action.Label == "Jason Thoams, unit 2" && action.Send!.Contains("wrong"));
        Assert.Contains(enriched.Actions!, action => action.Label == "Demo Tenant, unit 4");
    }

    [Fact]
    public async Task A_named_tenant_problem_is_filed_on_that_tenancy()
    {
        var jason = Tenancy(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "Jason Thoams", "2");
        var demo = Tenancy(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), "Demo Tenant", "4");
        var client = new RecordingClient();

        var reply = await MaintenanceChat.ReplyAsync(client, "Demo Tenant says the sink is broken", [jason, demo], jason.Id);

        Assert.Equal(demo.Id, client.MaintenanceRequest!.TenancyId);
        Assert.Equal(demo.Id, reply.TenancyId);
        Assert.Contains("Demo Tenant", reply.Text);
        Assert.Equal("Demo Tenant says the sink is broken", client.MaintenanceRequest.Title);
    }

    [Fact]
    public async Task A_tenancy_choice_keeps_the_problem_as_the_task_title()
    {
        var jason = Tenancy(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "Jason Thoams", "2");
        var demo = Tenancy(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), "Demo Tenant", "2");
        var client = new RecordingClient();

        await MaintenanceChat.ReplyAsync(client, "Demo Tenant, unit 2. the bedroom heater is broken", [jason, demo], null);

        Assert.Equal(demo.Id, client.MaintenanceRequest!.TenancyId);
        Assert.Equal("The bedroom heater is broken", client.MaintenanceRequest.Title);
        Assert.Equal("the bedroom heater is broken", client.MaintenanceRequest.Description);
    }

    [Fact]
    public async Task The_same_open_problem_is_not_filed_twice()
    {
        var tenant = Tenancy(TenancyId, "Demo Tenant", "2");
        var client = new RecordingClient();
        client.Maintenance.Add(new MaintenanceRequestDto(
            Guid.NewGuid(), TenancyId, "245 James Ave", "2", "The kitchen sink is broken", "Already filed", MaintenanceStatus.Submitted, true, [], null));

        var reply = await MaintenanceChat.ReplyAsync(client, "the kitchen sink is broken", [tenant], null);

        Assert.Equal(0, client.MaintenanceCreates);
        Assert.True(reply.LedgerChanged);
        Assert.Contains("already open", reply.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("landlord", reply.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_saved_maintenance_reply_is_not_replaced_with_page_choices()
    {
        var jason = Tenancy(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "Jason Thoams", "2");
        var reply = new AssistantReply(
            "Opened maintenance “The sink is broken” for Jason Thoams, unit 2. I added it to the landlord's to-do list.",
            "/maintenance",
            false,
            jason.Id,
            "Open maintenance",
            null,
            null) { LedgerChanged = true };

        var enriched = AssistantContext.Enrich(reply, "Jason the sink is broken", [jason], new Dictionary<Guid, Guid>(), 2026);

        Assert.Equal(reply.Text, enriched.Text);
        Assert.Equal("/maintenance", enriched.Href);
    }

    [Fact]
    public async Task Payment_is_not_recorded_until_confirm()
    {
        var store = new AssistantActionStore();
        var client = new RecordingClient();
        var owner = Guid.NewGuid();
        var request = new RecordPaymentRequest
        {
            Amount = 10m,
            EffectiveDate = new DateOnly(2026, 10, 5),
            Method = PaymentMethod.Cash,
            Note = "rent"
        };

        var proposal = store.Stage(owner, "Record payment", AssistantProposals.Payment(TenancyId, request));
        Assert.Equal(0, client.Payments);

        var result = await store.ConfirmAsync(proposal.ActionId, owner, client);
        Assert.Equal(1, client.Payments);
        Assert.Equal(TenancyId, client.TenancyId);
        Assert.Equal(10m, client.Request!.Amount);
        Assert.Equal("/receipts/R-20261005-ABCDEF", result.Href);

        var again = await Assert.ThrowsAsync<LeaseLedgerException>(() => store.ConfirmAsync(proposal.ActionId, owner, client));
        Assert.Contains("expired", again.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, client.Payments);
    }

    [Fact]
    public async Task Confirm_for_another_user_is_rejected()
    {
        var store = new AssistantActionStore();
        var client = new RecordingClient();
        var owner = Guid.NewGuid();
        var proposal = store.Stage(owner, "Record payment", AssistantProposals.Payment(TenancyId, new RecordPaymentRequest
        {
            Amount = 10m,
            EffectiveDate = new DateOnly(2026, 10, 5)
        }));

        var rejected = await Assert.ThrowsAsync<LeaseLedgerException>(() => store.ConfirmAsync(proposal.ActionId, Guid.NewGuid(), client));
        Assert.Contains("another", rejected.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, client.Payments);

        await store.ConfirmAsync(proposal.ActionId, owner, client);
        Assert.Equal(1, client.Payments);
    }

    private sealed class RecordingClient : ILeaseLedgerClient
    {
        public int Payments { get; private set; }
        public Guid TenancyId { get; private set; }
        public RecordPaymentRequest? Request { get; private set; }

        public Task<ReceiptDto> RecordPaymentAsync(Guid tenancyId, RecordPaymentRequest request, CancellationToken cancellationToken = default)
        {
            Payments++;
            TenancyId = tenancyId;
            Request = request;
            return Task.FromResult(new ReceiptDto("R-20261005-ABCDEF", request.EffectiveDate, request.Amount, request.Method, "Ada", "1 Main", "2", "Toronto", request.Note));
        }

        public Task<CurrentUserDto?> GetCurrentUserAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<DashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<PropertyDto>> GetPropertiesAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<PropertyDto> CreatePropertyAsync(CreatePropertyRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<UnitDto> AddUnitAsync(Guid propertyId, CreateUnitRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<TenancyDto> CreateTenancyAsync(Guid unitId, CreateTenancyRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<TenancyDetailDto?> GetTenancyAsync(Guid tenancyId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task RecordChargeAsync(Guid tenancyId, RecordChargeRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ReceiptDto?> GetReceiptAsync(string receiptNumber, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public List<MaintenanceRequestDto> Maintenance { get; } = [];
        public CreateMaintenanceRequest? MaintenanceRequest { get; private set; }
        public int MaintenanceCreates { get; private set; }

        public Task<IReadOnlyList<MaintenanceRequestDto>> GetMaintenanceAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MaintenanceRequestDto>>(Maintenance);

        public Task<MaintenanceRequestDto> CreateMaintenanceAsync(CreateMaintenanceRequest request, CancellationToken cancellationToken = default)
        {
            MaintenanceCreates++;
            MaintenanceRequest = request;
            var created = new MaintenanceRequestDto(Guid.NewGuid(), request.TenancyId, "245 James Ave", "2", request.Title, request.Description, MaintenanceStatus.Submitted, false, [], null);
            Maintenance.Add(created);
            return Task.FromResult(created);
        }
        public Task<MaintenanceRequestDto> UpdateMaintenanceStatusAsync(Guid requestId, UpdateMaintenanceStatusRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<N1PreviewDto> PreviewN1Async(Guid tenancyId, N1PreviewRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<N2PreviewDto> PreviewN2Async(Guid tenancyId, N2PreviewRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<N4PreviewDto> PreviewN4Async(Guid tenancyId, N4PreviewRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<NoticeRecordDto> SaveN1Async(Guid tenancyId, N1PreviewRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<NoticeRecordDto> SaveN2Async(Guid tenancyId, N2PreviewRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<NoticeRecordDto> SaveN4Async(Guid tenancyId, N4PreviewRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<NoticeRecordDto>> GetNoticesAsync(Guid tenancyId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<YearEndDto?> GetYearEndAsync(Guid tenancyId, int year, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<InspectionLogDto?> GetInspectionsAsync(Guid unitId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<InspectionDto> RecordInspectionAsync(Guid unitId, RecordInspectionRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<TerminationPreviewDto> PreviewN5Async(Guid tenancyId, N5PreviewRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<TerminationPreviewDto> PreviewN6Async(Guid tenancyId, N6PreviewRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<TerminationPreviewDto> PreviewN7Async(Guid tenancyId, N7PreviewRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<N3PreviewDto> PreviewN3Async(Guid tenancyId, N3PreviewRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<NoticeRecordDto> SaveN5Async(Guid tenancyId, N5PreviewRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<NoticeRecordDto> SaveN6Async(Guid tenancyId, N6PreviewRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<NoticeRecordDto> SaveN7Async(Guid tenancyId, N7PreviewRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<NoticeRecordDto> SaveN3Async(Guid tenancyId, N3PreviewRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task RecordNoticeServiceAsync(Guid noticeId, RecordServiceRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task AcknowledgeNoticeAsync(Guid noticeId, AcknowledgeNoticeRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<WorkOrderDto> AssignWorkOrderAsync(Guid requestId, AssignWorkOrderRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<InsuranceRecordDto?> GetInsuranceAsync(Guid tenancyId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<InsuranceRecordDto> SaveInsuranceAsync(Guid tenancyId, SaveInsuranceRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<LegalContactDto>> GetLegalContactsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<LegalContactDto> SaveLegalContactAsync(SaveLegalContactRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task ReferNoticeAsync(Guid noticeId, Guid contactId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<PaymentInstructionsDto?> GetPaymentInstructionsAsync(Guid tenancyId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task AuthorizePadAsync(Guid tenancyId, AcknowledgeNoticeRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<CheckoutStartDto> StartCheckoutAsync(StartCheckoutRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<CheckoutStartDto> ConfirmCheckoutAsync(string sessionId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<PlanDto> GetPlanAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<PdfTemplateSummaryDto>> GetPdfTemplatesAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<PdfTemplateDetailDto> UploadPdfTemplateAsync(UploadPdfTemplateRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<PdfTemplateDetailDto?> GetPdfTemplateAsync(Guid templateId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<PdfFileDto?> GetPdfTemplateFileAsync(Guid templateId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<PdfTemplateDetailDto> UpdatePdfTemplateAsync(Guid templateId, UpdatePdfTemplateRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task DeletePdfTemplateAsync(Guid templateId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<PdfFileDto> FillPdfTemplateAsync(Guid templateId, Guid tenancyId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<AgentHistoryDto> GetAgentHistoryAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task SaveAgentHistoryAsync(AgentHistoryDto history, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }
}
