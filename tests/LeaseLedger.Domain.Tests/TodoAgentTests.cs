using LeaseLedger.Application;
using LeaseLedger.Domain;

namespace LeaseLedger.Domain.Tests;

public class TodoAgentTests
{
    private static readonly Guid TenancyId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid OtherTenancyId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly DateOnly Today = new(2026, 10, 5);

    [Fact]
    public void Ledger_tracks_unpaid_rent_late_tenants_and_open_maintenance()
    {
        var tenancy = Tenancy(
            TenancyId,
            "Demo Tenant",
            2000m,
            new RentPeriodDto(new DateOnly(2026, 8, 1), RentTimeliness.Late, new DateOnly(2026, 8, 2)),
            new RentPeriodDto(new DateOnly(2026, 9, 1), RentTimeliness.Unpaid, null));
        var open = Request(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"), TenancyId, "Leaking kitchen tap", MaintenanceStatus.Submitted);
        var done = Request(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"), TenancyId, "Replace bulb", MaintenanceStatus.Completed);

        var items = TodoAgent.Sync([tenancy], [open, done], [], [], Today, new DateOnly(2026, 11, 1));

        var rent = Assert.Single(items, item => item.Kind == TodoKind.Rent);
        Assert.Equal("Collect unpaid rent from Demo Tenant", rent.Title);
        Assert.Equal($"/tenancies/{TenancyId}#payments", rent.Href);
        Assert.Contains("September 1, 2026", rent.Detail);
        Assert.Contains("$2,000.00 owing", rent.Detail);
        Assert.Equal(new DateOnly(2026, 9, 1), rent.Due);

        var tenant = Assert.Single(items, item => item.Kind == TodoKind.Tenant);
        Assert.Equal("Follow up with Demo Tenant on a late payment", tenant.Title);
        Assert.Equal($"/tenancies/{TenancyId}", tenant.Href);
        Assert.Equal(new DateOnly(2026, 8, 1), tenant.Due);

        var maintenance = Assert.Single(items, item => item.Kind == TodoKind.Maintenance);
        Assert.Equal("Leaking kitchen tap", maintenance.Title);
        Assert.Equal("/maintenance", maintenance.Href);
        Assert.Equal(Today, maintenance.Due);
        Assert.Contains("Submitted", maintenance.Detail);
        Assert.DoesNotContain(items, item => item.Title == "Replace bulb");
    }

    [Fact]
    public void Upcoming_rent_is_only_listed_within_two_days()
    {
        var soon = TodoAgent.Sync([], [], [], [], Today, Today.AddDays(2));
        var later = TodoAgent.Sync([], [], [], [], Today, Today.AddDays(3));
        var past = TodoAgent.Sync([], [], [], [], Today, Today.AddDays(-1));

        Assert.Contains(soon, item => item.Title.Contains("October 7, 2026") && item.Due == Today.AddDays(2));
        Assert.DoesNotContain(later, item => item.Kind == TodoKind.Rent);
        Assert.DoesNotContain(past, item => item.Kind == TodoKind.Rent);
    }

    [Fact]
    public void Chats_become_tasks_and_ledger_duplicates_stay_off_the_list()
    {
        var tenancy = Tenancy(TenancyId, "Demo Tenant", 2000m, new RentPeriodDto(new DateOnly(2026, 9, 1), RentTimeliness.Unpaid, null));
        var maintenance = Request(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"), TenancyId, "Leaking kitchen tap", MaintenanceStatus.Acknowledged);
        var chatId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
        var conversation = new TodoConversation(chatId, "September rent",
        [
            new TodoTurn("you", "open payments", null, null),
            new TodoTurn("agent", "Opening payments for Demo Tenant, unit 2.", null, null),
            new TodoTurn("you", "Demo Tenant", null, null),
            new TodoTurn("you", "What's the balance?", null, null),
            new TodoTurn("you", "Demo Tenant hasn't paid September", null, null),
            new TodoTurn("you", "remind me the leaking kitchen tap is still open", null, null),
            new TodoTurn("you", "remind me to call the plumber", null, null),
            new TodoTurn("agent", "You need to follow up on insurance.", null, null),
            new TodoTurn("agent", "Record the payment for Demo Tenant.", "abc123", "Record a payment for Demo Tenant, unit 2.")
        ]);

        var items = TodoAgent.Sync([tenancy], [maintenance], [conversation], [], Today, null);

        Assert.DoesNotContain(items, item => item.Title.Contains("Opening", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(items, item => item.Title.Contains("hasn't paid", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(items, item => !item.FromLedger && item.Title.Contains("tap", StringComparison.OrdinalIgnoreCase));
        Assert.Single(items, item => item.Kind == TodoKind.Rent && item.FromLedger);
        Assert.Single(items, item => item.FromLedger && item.Title == "Leaking kitchen tap");

        var broken = new TodoConversation(Guid.NewGuid(), "Sink",
        [
            new TodoTurn("you", "the kitchen sink is broken", null, null)
        ]);
        var filed = Request(Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"), TenancyId, "The kitchen sink is broken", MaintenanceStatus.Submitted);
        var landlordTasks = TodoAgent.Sync([tenancy], [maintenance, filed], [conversation, broken], [], Today, null);
        var sink = Assert.Single(landlordTasks, item => item.Title == "The kitchen sink is broken");
        Assert.Equal(TodoKind.Maintenance, sink.Kind);
        Assert.True(sink.FromLedger);
        Assert.Equal("/maintenance", sink.Href);

        var plumber = Assert.Single(items, item => item.Title == "Call the plumber");
        Assert.Equal(TodoKind.Maintenance, plumber.Kind);
        Assert.Equal("/maintenance", plumber.Href);
        Assert.Equal("From September rent", plumber.Detail);
        Assert.Equal(chatId, plumber.ConversationId);

        var insurance = Assert.Single(items, item => item.Title == "Follow up on insurance");
        Assert.Equal(TodoKind.Tenant, insurance.Kind);

        var proposal = Assert.Single(items, item => item.Key == "proposal:abc123");
        Assert.Contains("Needs confirmation", proposal.Detail);
        Assert.Equal(TodoKind.Rent, proposal.Kind);
        Assert.Equal($"/tenancies/{TenancyId}#payments", proposal.Href);
    }

    [Fact]
    public void Confirmed_proposals_drop_off_and_done_ledger_items_stay_done_until_the_balance_changes()
    {
        var tenancy = Tenancy(TenancyId, "Demo Tenant", 2000m, new RentPeriodDto(new DateOnly(2026, 9, 1), RentTimeliness.Unpaid, null));
        var conversation = new TodoConversation(Guid.NewGuid(), "Payment",
        [
            new TodoTurn("agent", "Please confirm.", "abc123", "Record a payment for Demo Tenant.")
        ]);
        var open = TodoAgent.Sync([tenancy], [], [conversation], [], Today, null);
        Assert.Contains(open, item => item.Key == "proposal:abc123");

        var checkedOff = TodoAgent.Handle("done collect", open, [tenancy], [], [conversation], Today, null).Items;
        Assert.Contains(checkedOff, item => item.Kind == TodoKind.Rent && item.FromLedger && item.Done);
        Assert.Contains(checkedOff, item => item.Key == "proposal:abc123" && !item.Done);

        var still = TodoAgent.Sync([tenancy], [], [conversation], checkedOff, Today, null);
        Assert.Contains(still, item => item.Kind == TodoKind.Rent && item.FromLedger && item.Done);

        var confirmed = new TodoConversation(conversation.Id, conversation.Title,
        [
            new TodoTurn("agent", "Recorded the payment.", null, null)
        ]);
        var afterConfirm = TodoAgent.Sync([tenancy], [], [confirmed], still, Today, null);
        Assert.DoesNotContain(afterConfirm, item => item.Key == "proposal:abc123");

        var reduced = Tenancy(TenancyId, "Demo Tenant", 500m, new RentPeriodDto(new DateOnly(2026, 9, 1), RentTimeliness.Unpaid, null));
        var reopened = TodoAgent.Sync([reduced], [], [confirmed], afterConfirm, Today, null);
        var rent = Assert.Single(reopened, item => item.Kind == TodoKind.Rent && item.FromLedger);
        Assert.False(rent.Done);
        Assert.Contains("$500.00 owing", rent.Detail);
    }

    [Fact]
    public void Commands_add_review_and_check_off_items()
    {
        var tenancy = Tenancy(TenancyId, "Jason Alvarez", 0m);
        var other = Tenancy(OtherTenancyId, "Jason Cho", 0m);
        var chat = new TodoConversation(Guid.NewGuid(), "Broker",
        [
            new TodoTurn("you", "remind me to email the broker about insurance", null, null)
        ]);

        var added = TodoAgent.Handle("add call Jason about the lease", [], [tenancy], [], [], Today, null);
        Assert.Equal("Added “Call Jason about the lease”.", added.Text);
        var task = Assert.Single(added.Items);
        Assert.Equal(TodoKind.Tenant, task.Kind);
        Assert.Equal($"/tenancies/{TenancyId}", task.Href);

        var ambiguous = TodoAgent.Handle("call Jason tomorrow", [], [tenancy, other], [], [], Today, null);
        Assert.Equal(TodoKind.Other, Assert.Single(ambiguous.Items).Kind);
        Assert.Null(Assert.Single(ambiguous.Items).Href);

        var reviewed = TodoAgent.Handle("review the chats", [], [tenancy], [], [chat], Today, null);
        Assert.StartsWith("Reviewed your chats.", reviewed.Text);
        Assert.Contains(reviewed.Items, item => item.Title == "Email the broker about insurance" && item.Kind == TodoKind.Tenant);

        var duplicate = TodoAgent.Handle("email the broker about insurance", reviewed.Items, [tenancy], [], [chat], Today, null);
        Assert.Contains("already on the list", duplicate.Text);
        Assert.Equal(reviewed.Items.Count, duplicate.Items.Count);

        var calls = TodoAgent.Handle("add call the plumber", reviewed.Items, [tenancy], [], [chat], Today, null).Items;
        calls = TodoAgent.Handle("add call the insurer", calls, [tenancy], [], [chat], Today, null).Items;
        var unclear = TodoAgent.Handle("done call", calls, [tenancy], [], [chat], Today, null);
        Assert.Equal("Which one?", unclear.Text);
        Assert.Equal(2, unclear.Replies!.Count);
        Assert.All(unclear.Replies, reply => Assert.StartsWith("done ", reply.Send));
        Assert.DoesNotContain(";", unclear.Text);

        var done = TodoAgent.Handle("done insurance", reviewed.Items, [tenancy], [], [chat], Today, null);
        Assert.Contains("Checked off", done.Text);
        Assert.All(done.Items.Where(item => item.Title.Contains("insurance")), item => Assert.True(item.Done));

        var listed = TodoAgent.Handle("what's open", done.Items, [tenancy], [], [chat], Today, null);
        Assert.Equal("Nothing is open.", listed.Text);

        var reopened = TodoAgent.Handle("reopen insurance", done.Items, [tenancy], [], [chat], Today, null);
        Assert.Contains("Reopened", reopened.Text);
        Assert.Contains(reopened.Items, item => item.Title.Contains("insurance") && !item.Done);

        var ignored = TodoAgent.Handle("hello there", [], [], [], [], Today, null);
        Assert.Contains("Add a task", ignored.Text);
        Assert.Empty(ignored.Items);
    }

    [Fact]
    public void Tasks_land_on_the_calendar_day_they_name()
    {
        var tenancy = Tenancy(TenancyId, "Demo Tenant", 0m);
        var open = Request(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"), TenancyId, "Leaking kitchen tap", MaintenanceStatus.Submitted);
        var chat = new TodoConversation(Guid.NewGuid(), "Plumber",
        [
            new TodoTurn("you", "remind me to call the plumber tomorrow", null, null)
        ]);

        var dated = TodoAgent.Handle("add call the plumber on October 20", [], [tenancy], [], [], Today, null);
        var plumber = Assert.Single(dated.Items);
        Assert.Equal("Call the plumber", plumber.Title);
        Assert.Equal(new DateOnly(2026, 10, 20), plumber.Due);
        Assert.Contains("October 20, 2026", dated.Text);

        var onTheDay = TodoAgent.Handle("add inspect the alarm on the 15th", [], [tenancy], [], [], Today, null);
        Assert.Equal(new DateOnly(2026, 10, 15), Assert.Single(onTheDay.Items).Due);

        var fromCalendar = TodoAgent.Handle("add email the broker", [], [tenancy], [], [], Today, null, new DateOnly(2026, 10, 22));
        Assert.Equal(new DateOnly(2026, 10, 22), Assert.Single(fromCalendar.Items).Due);
        Assert.Contains("October 22, 2026", fromCalendar.Text);

        var moved = TodoAgent.Handle("move the plumber to Friday", dated.Items, [tenancy], [], [], Today, null);
        Assert.Equal(new DateOnly(2026, 10, 9), Assert.Single(moved.Items).Due);
        Assert.Contains("Moved", moved.Text);

        var synced = TodoAgent.Sync([tenancy], [open], [chat], [], Today, null);
        var reminder = Assert.Single(synced, item => item.Title == "Call the plumber");
        Assert.Equal(Today.AddDays(1), reminder.Due);
        Assert.Equal(Today, Assert.Single(synced, item => item.Title == "Leaking kitchen tap").Due);

        var later = TodoAgent.Sync([tenancy], [open], [chat], synced, Today.AddDays(4), null);
        Assert.Equal(Today, Assert.Single(later, item => item.Title == "Leaking kitchen tap").Due);
        Assert.Equal(Today.AddDays(1), Assert.Single(later, item => item.Title == "Call the plumber").Due);

        var october = TodoAgent.MonthDays(new DateOnly(2026, 10, 1));
        Assert.Equal(new DateOnly(2026, 9, 28), october[0]);
        Assert.Contains(new DateOnly(2026, 10, 20), october);
        Assert.Equal(42, october.Count);
    }

    [Fact]
    public void Tasks_can_be_renamed_moved_and_deleted()
    {
        var tenancy = Tenancy(TenancyId, "Demo Tenant", 0m);
        var added = TodoAgent.Handle("add call the plumber on October 20", [], [tenancy], [], [], Today, null);
        var original = Assert.Single(added.Items);

        var renamed = TodoAgent.Handle("rename the plumber to email the broker", added.Items, [tenancy], [], [], Today, null);
        var updated = Assert.Single(renamed.Items);
        Assert.Equal(original.Key, updated.Key);
        Assert.Equal("Email the broker", updated.Title);
        Assert.Equal(new DateOnly(2026, 10, 20), updated.Due);
        Assert.StartsWith("Updated", renamed.Text);

        var moved = TodoAgent.Handle("update the broker to Friday", renamed.Items, [tenancy], [], [], Today, null);
        Assert.Equal("Email the broker", Assert.Single(moved.Items).Title);
        Assert.Equal(new DateOnly(2026, 10, 9), Assert.Single(moved.Items).Due);

        var deleted = TodoAgent.Handle("delete the broker", moved.Items, [tenancy], [], [], Today, null);
        Assert.Empty(deleted.Items);
        Assert.Equal([original.Key], deleted.RemovedKeys);
    }

    [Fact]
    public void Update_offers_missing_items_and_adds_the_one_you_tap()
    {
        var tenancy = Tenancy(TenancyId, "Demo Tenant", 2000m, new RentPeriodDto(new DateOnly(2026, 9, 1), RentTimeliness.Unpaid, null));
        var open = Request(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"), TenancyId, "Leaking kitchen tap", MaintenanceStatus.Submitted);
        var chat = new TodoConversation(Guid.NewGuid(), "Plumber",
        [
            new TodoTurn("you", "remind me to call the plumber tomorrow", null, null)
        ]);
        var kept = TodoAgent.Handle("add email the broker on October 20", [], [tenancy], [], [], Today, null).Items;
        var broker = Assert.Single(kept);
        var edited = broker with { Title = "Email the broker about the lease" };

        var suggested = TodoAgent.FindNew([edited], [tenancy], [open], [chat], Today, null);

        Assert.Equal("I found 3 items that are not on the calendar yet. Tap one to add it.", suggested.Text);
        var still = Assert.Single(suggested.Items);
        Assert.Equal(edited.Key, still.Key);
        Assert.Equal("Email the broker about the lease", still.Title);
        Assert.Equal(new DateOnly(2026, 10, 20), still.Due);
        Assert.Equal("Maintenance, Rent", string.Join(", ", suggested.Replies!.Select(reply => reply.Group).Distinct()));
        var plumber = Assert.Single(suggested.Replies!, reply => reply.Label.StartsWith("Call the plumber", StringComparison.Ordinal));
        Assert.Contains("Tomorrow", plumber.Label);
        Assert.StartsWith("add suggested ", plumber.Send);

        var added = TodoAgent.Handle(plumber.Send, suggested.Items, [tenancy], [open], [chat], Today, null);
        Assert.StartsWith("Added “Call the plumber”", added.Text);
        Assert.Contains(added.Items, item => item.Title == "Call the plumber" && item.Due == Today.AddDays(1));
        Assert.Contains(added.Items, item => item.Key == edited.Key && item.Title == "Email the broker about the lease");

        var duplicate = TodoAgent.Handle(plumber.Send, added.Items, [tenancy], [open], [chat], Today, null);
        Assert.Contains("already on the calendar", duplicate.Text);
        Assert.Equal(added.Items.Count, duplicate.Items.Count);

        var again = TodoAgent.Handle("find new items", added.Items, [tenancy], [open], [chat], Today, null);
        Assert.DoesNotContain(again.Replies!, reply => reply.Label.StartsWith("Call the plumber", StringComparison.Ordinal));
        Assert.Contains(again.Replies!, reply => reply.Label.StartsWith("Leaking kitchen tap", StringComparison.Ordinal));
        Assert.Contains(again.Replies!, reply => reply.Group == "Rent");

        var renamed = TodoAgent.Handle("update the plumber to Friday", added.Items, [tenancy], [open], [chat], Today, null);
        Assert.StartsWith("Updated", renamed.Text);
        Assert.Equal(new DateOnly(2026, 10, 9), Assert.Single(renamed.Items, item => item.Title == "Call the plumber").Due);
    }

    private static TenancySummaryDto Tenancy(Guid id, string name, decimal balance, params RentPeriodDto[] periods) =>
        new(id, "245 James Ave", "2", name, 2000m, balance, periods, Guid.Parse("44444444-4444-4444-4444-444444444444"), "Toronto");

    private static MaintenanceRequestDto Request(Guid id, Guid tenancyId, string title, MaintenanceStatus status) =>
        new(id, tenancyId, "245 James Ave", "2", title, "Drips", status, true, [], null);
}
