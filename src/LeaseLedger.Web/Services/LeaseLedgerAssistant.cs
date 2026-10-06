using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using LeaseLedger.Application;
using LeaseLedger.Shared;
using Microsoft.Extensions.Options;

namespace LeaseLedger.Web.Services;

public sealed class LeaseLedgerAssistant(
    HttpClient http,
    IOptions<AssistantOptions> options,
    ILeaseLedgerClient client,
    ICurrentUserAccessor users,
    AssistantToolRunner tools,
    AssistantActionStore actions) : IAssistant
{
    private const int MaxMessage = 2000;
    private const int MaxRounds = 6;
    private readonly AssistantOptions _options = options.Value;

    public async Task<AssistantReply> AskAsync(AssistantRequest request, CancellationToken cancellationToken = default)
    {
        var message = request.Message.Trim();
        if (message.Length > MaxMessage)
            message = message[..MaxMessage];
        if (MaintenanceChat.IsReport(message))
        {
            var signedIn = await users.GetAsync(cancellationToken);
            if (signedIn.UserId is null)
                throw new LeaseLedgerException("Sign in to ask LeaseLedger.");

            return await MaintenanceReplyAsync(message, request.TenancyId, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            return await FallbackAsync(request, cancellationToken);

        var user = await users.GetAsync(cancellationToken);
        if (user.UserId is null)
            throw new LeaseLedgerException("Sign in to ask LeaseLedger.");

        if (message.Length == 0)
            return new AssistantReply("Say which page to open.", null, false, request.TenancyId, null, null, null);

        var messages = new List<JsonObject> { SystemMessage() };
        foreach (var turn in request.History.TakeLast(12))
        {
            if (turn.Role == "you")
                messages.Add(TextMessage("user", turn.Text));
            else if (turn.Role == "agent" && !string.IsNullOrWhiteSpace(turn.Text))
                messages.Add(TextMessage("assistant", turn.Text));
        }

        var prefix = request.TenancyId is Guid tenancyId
            ? $"Current tenancy id: {tenancyId}\n"
            : "No tenancy is selected yet.\n";
        messages.Add(TextMessage("user", prefix + message));

        AssistantLink? link = null;
        AssistantProposal? proposal = null;
        try
        {
            for (var round = 0; round < MaxRounds; round++)
            {
                var completion = await CompleteAsync(messages, cancellationToken);
                if (completion.ToolCalls.Count == 0)
                {
                    var text = string.IsNullOrWhiteSpace(completion.Content)
                        ? link is null ? "I could not answer that." : link.Label + "."
                        : completion.Content.Trim();
                    return await FinishAsync(Reply(text, link, proposal, request.TenancyId), message, cancellationToken);
                }

                messages.Add(AssistantToolMessage(completion));
                foreach (var call in completion.ToolCalls)
                {
                    var outcome = await tools.RunAsync(call.Name, call.ArgumentsJson, user.UserId.Value, cancellationToken);
                    if (outcome.Link is not null)
                        link = outcome.Link;
                    if (outcome.Proposal is not null)
                        proposal = outcome.Proposal;
                    messages.Add(ToolResult(call.Id, outcome.Content));
                }
            }
        }
        catch (HttpRequestException)
        {
            throw new LeaseLedgerException("The assistant could not be reached.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new LeaseLedgerException("The assistant took too long to answer.");
        }

        return await FinishAsync(Reply("I need a narrower question to finish that.", link, proposal, request.TenancyId), message, cancellationToken);
    }

    public async Task<AssistantReply> ConfirmAsync(Guid actionId, CancellationToken cancellationToken = default)
    {
        var user = await users.GetAsync(cancellationToken);
        if (user.UserId is null)
            throw new LeaseLedgerException("Sign in to confirm.");

        var result = await actions.ConfirmAsync(actionId, user.UserId.Value, client, cancellationToken);
        return new AssistantReply(result.Text, result.Href, false, result.TenancyId, result.LinkLabel, null, result.Download);
    }

    private async Task<AssistantReply> FallbackAsync(AssistantRequest request, CancellationToken cancellationToken)
    {
        DashboardDto dashboard;
        try
        {
            dashboard = await client.GetDashboardAsync(cancellationToken);
        }
        catch (LeaseLedgerException exception)
        {
            return new AssistantReply(exception.Message, null, false, request.TenancyId, null, null, null);
        }

        var result = AgentNavigator.Interpret(request.Message, dashboard.Tenancies, request.TenancyId);
        AssistantReply reply;
        if (result.Inspections && result.TenancyId is Guid tenancyId)
        {
            try
            {
                var detail = await client.GetTenancyAsync(tenancyId, cancellationToken);
                if (detail is null || !AssistantPages.TryOpen("inspections", new AssistantRouteArgs(null, detail.UnitId, null, null, null), out var opened))
                    reply = new AssistantReply("That tenancy was not found.", null, false, result.TenancyId, null, null, null);
                else
                    reply = new AssistantReply(result.Reply, opened.Href, false, result.TenancyId, opened.Label, null, null);
            }
            catch (LeaseLedgerException exception)
            {
                reply = new AssistantReply(exception.Message, null, false, result.TenancyId, null, null, null);
            }
        }
        else
        {
            reply = new AssistantReply(result.Reply, result.Href, result.External, result.TenancyId, result.LinkLabel, null, null);
        }

        return AssistantContext.Enrich(
            reply,
            request.Message,
            dashboard.Tenancies,
            await UnitIdsAsync(cancellationToken),
            DateTime.Now.Year,
            await TemplatesAsync(cancellationToken));
    }

    private async Task<AssistantReply> MaintenanceReplyAsync(string message, Guid? tenancyId, CancellationToken cancellationToken)
    {
        DashboardDto dashboard;
        try
        {
            dashboard = await client.GetDashboardAsync(cancellationToken);
        }
        catch (LeaseLedgerException exception)
        {
            return new AssistantReply(exception.Message, null, false, tenancyId, null, null, null);
        }

        AssistantReply reply;
        try
        {
            reply = await MaintenanceChat.ReplyAsync(client, message, dashboard.Tenancies, tenancyId, cancellationToken);
        }
        catch (LeaseLedgerException exception)
        {
            return new AssistantReply(exception.Message, null, false, tenancyId, null, null, null);
        }

        if (reply.LedgerChanged)
            return reply;

        return AssistantContext.Enrich(
            reply,
            message,
            dashboard.Tenancies,
            await UnitIdsAsync(cancellationToken),
            DateTime.Now.Year,
            await TemplatesAsync(cancellationToken));
    }

    private async Task<AssistantReply> FinishAsync(AssistantReply reply, string message, CancellationToken cancellationToken)
    {
        DashboardDto dashboard;
        try
        {
            dashboard = await client.GetDashboardAsync(cancellationToken);
        }
        catch (LeaseLedgerException)
        {
            return reply;
        }

        return AssistantContext.Enrich(
            reply,
            message,
            dashboard.Tenancies,
            await UnitIdsAsync(cancellationToken),
            DateTime.Now.Year,
            await TemplatesAsync(cancellationToken));
    }

    private async Task<IReadOnlyList<PdfTemplateSummaryDto>> TemplatesAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await client.GetPdfTemplatesAsync(cancellationToken);
        }
        catch (LeaseLedgerException)
        {
            return [];
        }
    }

    private async Task<IReadOnlyDictionary<Guid, Guid>> UnitIdsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var properties = await client.GetPropertiesAsync(cancellationToken);
            var map = new Dictionary<Guid, Guid>();
            foreach (var property in properties)
            {
                foreach (var unit in property.Units)
                {
                    foreach (var tenancy in unit.Tenancies)
                        map[tenancy.Id] = unit.Id;
                }
            }

            return map;
        }
        catch (LeaseLedgerException)
        {
            return new Dictionary<Guid, Guid>();
        }
    }

    private async Task<Completion> CompleteAsync(List<JsonObject> messages, CancellationToken cancellationToken)
    {
        var payload = new JsonObject
        {
            ["model"] = string.IsNullOrWhiteSpace(_options.Model) ? "gpt-4.1-mini" : _options.Model,
            ["messages"] = Clone(messages),
            ["tools"] = AssistantToolSchemas.All.DeepClone()
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        httpRequest.Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");

        using var response = await http.SendAsync(httpRequest, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new LeaseLedgerException(ReadApiError(body));

        return Parse(body);
    }

    private static JsonObject SystemMessage() => TextMessage("system",
        """
        You are the LeaseLedger assistant inside a property-management app for Ontario residential tenancies. Help the signed-in user open the right page, answer from their records, and prepare forms.

        Rules:
        - Call a tool for any balance, date, notice, or other record. Do not invent figures.
        - Open pages only with open_page. Never invent a URL.
        - Notice helpers calculate from the tenancy. They are not legal advice. Call the matching preview tool before you propose saving a notice, and tell the user the preview result.
        - propose_* tools do not save, charge, or download. Tell the user to press Confirm. Do not say a form, payment, or PDF was saved until they do.
        - If the user only names a tenant, unit, or street, describe who you found and do not call open_page. The app shows page buttons.
        - If several tenancies could match, ask "Which tenancy?" and stop. The app shows a tappable reply for each tenant and each unit. Do not list the names.
        - If they ask for a document such as a rent agreement, say so. The app lists that document, the tenancy, and the related documents as buttons.
        - If the user reports something broken, leaking, or wrong in a unit, call propose_maintenance. Say it will also go on the landlord to-do list after Confirm. Do not answer with a page list.
        - Checkout, deleting templates, uploading templates, and creating properties are not available.

        Pages:
        """ + AssistantPages.Describe());

    private static AssistantReply Reply(string text, AssistantLink? link, AssistantProposal? proposal, Guid? contextTenancyId)
    {
        if (proposal is not null)
            text = text.Trim() + "\n\n" + proposal.Summary;

        return new AssistantReply(
            text,
            link?.Href,
            false,
            link?.TenancyId ?? contextTenancyId,
            link?.Label,
            proposal,
            null);
    }

    private static JsonObject AssistantToolMessage(Completion completion)
    {
        var calls = new JsonArray();
        foreach (var call in completion.ToolCalls)
        {
            calls.Add(new JsonObject
            {
                ["id"] = call.Id,
                ["type"] = "function",
                ["function"] = new JsonObject
                {
                    ["name"] = call.Name,
                    ["arguments"] = call.ArgumentsJson
                }
            });
        }

        return new JsonObject
        {
            ["role"] = "assistant",
            ["content"] = completion.Content ?? "",
            ["tool_calls"] = calls
        };
    }

    private static JsonObject ToolResult(string id, string content) => new()
    {
        ["role"] = "tool",
        ["tool_call_id"] = id,
        ["content"] = content
    };

    private static JsonObject TextMessage(string role, string content) => new()
    {
        ["role"] = role,
        ["content"] = content
    };

    private static JsonArray Clone(List<JsonObject> messages)
    {
        var copy = new JsonArray();
        foreach (var message in messages)
            copy.Add(message.DeepClone());
        return copy;
    }

    private static Completion Parse(string body)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
            throw new LeaseLedgerException("The assistant returned an empty answer.");

        var message = choices[0].GetProperty("message");
        var content = ReadContent(message);
        var calls = new List<ToolCall>();
        if (message.TryGetProperty("tool_calls", out var toolCalls) && toolCalls.ValueKind == JsonValueKind.Array)
        {
            foreach (var call in toolCalls.EnumerateArray())
            {
                var function = call.GetProperty("function");
                var id = call.TryGetProperty("id", out var idNode) ? idNode.GetString() : null;
                calls.Add(new ToolCall(
                    string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id,
                    function.GetProperty("name").GetString() ?? "",
                    function.TryGetProperty("arguments", out var arguments) ? arguments.GetString() ?? "{}" : "{}"));
            }
        }

        return new Completion(content, calls);
    }

    private static string? ReadContent(JsonElement message)
    {
        if (!message.TryGetProperty("content", out var content) || content.ValueKind is JsonValueKind.Null)
            return null;
        if (content.ValueKind == JsonValueKind.String)
            return content.GetString();
        if (content.ValueKind != JsonValueKind.Array)
            return null;

        var text = new StringBuilder();
        foreach (var part in content.EnumerateArray())
        {
            if (part.ValueKind == JsonValueKind.String)
                text.Append(part.GetString());
            else if (part.TryGetProperty("text", out var piece) && piece.ValueKind == JsonValueKind.String)
                text.Append(piece.GetString());
        }

        return text.ToString();
    }

    private static string ReadApiError(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("error", out var error)
                && error.TryGetProperty("message", out var message)
                && message.GetString() is string text
                && !string.IsNullOrWhiteSpace(text))
                return text.Length <= 180 ? text : text[..180];
        }
        catch (JsonException)
        {
        }

        return "The assistant could not answer just now.";
    }

    private sealed record Completion(string? Content, List<ToolCall> ToolCalls);

    private sealed record ToolCall(string Id, string Name, string ArgumentsJson);
}
