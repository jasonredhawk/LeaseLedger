using System.Text.Json.Nodes;
using LeaseLedger.Application;

namespace LeaseLedger.Web.Services;

public static class AssistantToolSchemas
{
    public static JsonArray All { get; } = Build();

    private static JsonArray Build()
    {
        var pages = new JsonArray();
        foreach (var key in AssistantPages.Keys)
            pages.Add(key);

        JsonObject PageKey() => new()
        {
            ["type"] = "string",
            ["description"] = "Page key from list_pages.",
            ["enum"] = pages.DeepClone()
        };

        return new JsonArray(
            Tool(AssistantTools.ListPages, "List pages, who they are for, and which form fields they use.", Empty()),
            Tool(AssistantTools.GetDashboard, "Balances, arrears, maintenance count, and tenancies for the signed-in user.", Empty()),
            Tool(AssistantTools.GetTenancy, "Rent, balance, and recent ledger lines for one tenancy.", Obj(("tenancyId", GuidProp("Tenancy id."), true))),
            Tool(AssistantTools.GetNotices, "Saved notices for one tenancy.", Obj(("tenancyId", GuidProp("Tenancy id."), true))),
            Tool(AssistantTools.GetYearEnd, "Year totals for one tenancy.", Obj(("tenancyId", GuidProp("Tenancy id."), true), ("year", IntProp("Calendar year."), true))),
            Tool(AssistantTools.GetInspections, "Inspection log. Provide unitId or tenancyId.", Obj(("unitId", GuidProp("Unit id."), false), ("tenancyId", GuidProp("Tenancy id, used when unitId is omitted."), false))),
            Tool(AssistantTools.GetInsurance, "Insurance record for one tenancy.", Obj(("tenancyId", GuidProp("Tenancy id."), true))),
            Tool(AssistantTools.GetMaintenance, "Open and recent maintenance requests.", Empty()),
            Tool(AssistantTools.GetProperties, "Properties, units, and tenancies.", Empty()),
            Tool(AssistantTools.GetLegalContacts, "Legal and paralegal contacts.", Empty()),
            Tool(AssistantTools.GetPaymentInstructions, "Amount due and how to pay for one tenancy.", Obj(("tenancyId", GuidProp("Tenancy id."), true))),
            Tool(AssistantTools.GetPlan, "The landlord subscription plan.", Empty()),
            Tool(AssistantTools.ListPdfTemplates, "Uploaded PDF templates that can be filled.", Empty()),
            Tool(AssistantTools.OpenPage, "Open a known page. Do not invent a URL.", Obj(
                ("page", PageKey(), true),
                ("tenancyId", GuidProp("Required for a tenancy page."), false),
                ("unitId", GuidProp("Required for inspections when tenancyId is omitted."), false),
                ("year", IntProp("Required for year-end."), false),
                ("templateId", GuidProp("Required for one form template."), false),
                ("receiptNumber", Str("Required for a receipt."), false))),
            Notice("preview_n1", "Calculate an N1. This does not save.", N1Fields()),
            Notice("preview_n2", "Calculate an N2. This does not save.", N2Fields()),
            Notice("preview_n3", "Calculate an N3. This does not save.", N3Fields()),
            Notice("preview_n4", "Calculate an N4. This does not save.", N4Fields()),
            Notice("preview_n5", "Calculate an N5. This does not save.", N5Fields()),
            Notice("preview_n6", "Calculate an N6. This does not save.", N6Fields()),
            Notice("preview_n7", "Calculate an N7. This does not save.", N7Fields()),
            Notice(AssistantTools.ProposeSaveN1, "Stage an N1 save. Nothing is saved until the user presses Confirm.", N1Fields()),
            Notice(AssistantTools.ProposeSaveN2, "Stage an N2 save. Nothing is saved until the user presses Confirm.", N2Fields()),
            Notice(AssistantTools.ProposeSaveN3, "Stage an N3 save. Nothing is saved until the user presses Confirm.", N3Fields()),
            Notice(AssistantTools.ProposeSaveN4, "Stage an N4 save. Nothing is saved until the user presses Confirm.", N4Fields()),
            Notice(AssistantTools.ProposeSaveN5, "Stage an N5 save. Nothing is saved until the user presses Confirm.", N5Fields()),
            Notice(AssistantTools.ProposeSaveN6, "Stage an N6 save. Nothing is saved until the user presses Confirm.", N6Fields()),
            Notice(AssistantTools.ProposeSaveN7, "Stage an N7 save. Nothing is saved until the user presses Confirm.", N7Fields()),
            Tool(AssistantTools.ProposePayment, "Stage a payment. Nothing is recorded until the user presses Confirm.", Obj(
                ("tenancyId", GuidProp("Tenancy id."), true),
                ("method", EnumProp("How the tenant paid.", "ETransfer", "Cash", "Cheque", "Card", "Other"), true),
                ("amount", Num("Amount received."), true),
                ("effectiveDate", DateProp("Date the payment counts."), true),
                ("note", Str("Optional note."), false))),
            Tool(AssistantTools.ProposeCharge, "Stage a charge. Nothing is recorded until the user presses Confirm.", Obj(
                ("tenancyId", GuidProp("Tenancy id."), true),
                ("category", EnumProp("Charge category.", "Rent", "Other", "Nsf"), true),
                ("amount", Num("Amount charged."), true),
                ("effectiveDate", DateProp("Date the charge counts."), true),
                ("note", Str("Optional note."), false))),
            Tool(AssistantTools.ProposeMaintenance, "Stage a maintenance request. Nothing is created until the user presses Confirm.", Obj(
                ("tenancyId", GuidProp("Tenancy id."), true),
                ("title", Str("Short title."), true),
                ("description", Str("What needs work."), true))),
            Tool(AssistantTools.ProposeInsurance, "Stage an insurance update. Nothing is saved until the user presses Confirm.", Obj(
                ("tenancyId", GuidProp("Tenancy id."), true),
                ("status", EnumProp("Insurance status.", "Requested", "Active", "Cancelled"), true),
                ("brokerName", Str("Broker name."), true),
                ("policyNumber", Str("Policy number."), false),
                ("landlordListed", Bool("Whether the landlord is listed on the policy."), true),
                ("note", Str("Optional note."), false))),
            Tool(AssistantTools.ProposeInspection, "Stage an inspection. Nothing is recorded until the user presses Confirm.", Obj(
                ("unitId", GuidProp("Unit id."), false),
                ("tenancyId", GuidProp("Tenancy id, used when unitId is omitted."), false),
                ("inspectedOn", DateProp("Inspection date."), true),
                ("summary", Str("Short summary."), true),
                ("notes", Str("Optional notes."), false))),
            Tool(AssistantTools.ProposeFillPdf, "Stage a filled PDF download. The file is not created until the user presses Confirm.", Obj(
                ("templateId", GuidProp("PDF template id."), true),
                ("tenancyId", GuidProp("Tenancy id."), true))));
    }

    private static JsonNode Tool(string name, string description, JsonObject parameters) => new JsonObject
    {
        ["type"] = "function",
        ["function"] = new JsonObject
        {
            ["name"] = name,
            ["description"] = description,
            ["parameters"] = parameters
        }
    };

    private static JsonNode Notice(string name, string description, JsonObject parameters) => Tool(name, description, parameters);

    private static JsonObject Empty() => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject(),
        ["additionalProperties"] = false
    };

    private static JsonObject Obj(params (string Name, JsonNode Schema, bool Required)[] fields)
    {
        var properties = new JsonObject();
        var required = new JsonArray();
        foreach (var (name, schema, isRequired) in fields)
        {
            properties[name] = schema;
            if (isRequired)
                required.Add(name);
        }

        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required,
            ["additionalProperties"] = false
        };
    }

    private static JsonObject N1Fields() => Obj(
        ("tenancyId", GuidProp("Tenancy id."), true),
        ("noticeDate", DateProp("Date on the notice."), true),
        ("proposedEffectiveDate", DateProp("Proposed effective date."), true));

    private static JsonObject N2Fields() => Obj(
        ("tenancyId", GuidProp("Tenancy id."), true),
        ("noticeDate", DateProp("Date on the notice."), true),
        ("proposedEffectiveDate", DateProp("Proposed effective date."), true),
        ("proposedRent", Num("Proposed rent."), true));

    private static JsonObject N3Fields() => Obj(
        ("tenancyId", GuidProp("Tenancy id."), true),
        ("increasingRent", Bool("Whether rent is increasing."), true),
        ("proposedRent", Num("Proposed rent."), true),
        ("currentCareAndMeals", Num("Current care and meals amount."), true),
        ("proposedCareAndMeals", Num("Proposed care and meals amount."), true),
        ("noticeDate", DateProp("Date on the notice."), true),
        ("effectiveDate", DateProp("Proposed effective date."), true));

    private static JsonObject N4Fields() => Obj(
        ("tenancyId", GuidProp("Tenancy id."), true),
        ("noticeDate", DateProp("Date on the notice."), true));

    private static JsonObject N5Fields() => Obj(
        ("tenancyId", GuidProp("Tenancy id."), true),
        ("noticeDate", DateProp("Date on the notice."), true),
        ("reason", EnumProp("N5 reason.", "Interference", "Damage", "Overcrowding"), true),
        ("secondNotice", Bool("Whether this follows an earlier N5."), true),
        ("firstNoticeDate", DateProp("Date of the first N5, when secondNotice is true."), false),
        ("details", Str("What happened."), true));

    private static JsonObject N6Fields() => Obj(
        ("tenancyId", GuidProp("Tenancy id."), true),
        ("noticeDate", DateProp("Date on the notice."), true),
        ("reason", EnumProp("N6 reason.", "DrugOffence", "OtherIllegalAct", "IncomeMisrepresentation"), true),
        ("afterVoidableN5", Bool("Whether this follows a voidable N5."), true),
        ("details", Str("What happened."), true));

    private static JsonObject N7Fields() => Obj(
        ("tenancyId", GuidProp("Tenancy id."), true),
        ("noticeDate", DateProp("Date on the notice."), true),
        ("details", Str("The serious problem."), true));

    private static JsonObject Str(string description) => new() { ["type"] = "string", ["description"] = description };

    private static JsonObject GuidProp(string description) => new() { ["type"] = "string", ["format"] = "uuid", ["description"] = description };

    private static JsonObject DateProp(string description) => new() { ["type"] = "string", ["format"] = "date", ["description"] = description };

    private static JsonObject IntProp(string description) => new() { ["type"] = "integer", ["description"] = description };

    private static JsonObject Num(string description) => new() { ["type"] = "number", ["description"] = description };

    private static JsonObject Bool(string description) => new() { ["type"] = "boolean", ["description"] = description };

    private static JsonObject EnumProp(string description, params string[] values)
    {
        var choices = new JsonArray();
        foreach (var value in values)
            choices.Add(value);

        return new JsonObject
        {
            ["type"] = "string",
            ["description"] = description,
            ["enum"] = choices
        };
    }
}
