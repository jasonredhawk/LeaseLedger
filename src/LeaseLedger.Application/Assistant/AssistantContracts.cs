namespace LeaseLedger.Application;

public interface IAssistant
{
    Task<AssistantReply> AskAsync(AssistantRequest request, CancellationToken cancellationToken = default);

    Task<AssistantReply> ConfirmAsync(Guid actionId, CancellationToken cancellationToken = default);
}

public sealed record AssistantRequest(string Message, IReadOnlyList<AssistantTurn> History, Guid? TenancyId);

public sealed record AssistantTurn(string Role, string Text);

public sealed record AssistantReply(
    string Text,
    string? Href,
    bool External,
    Guid? TenancyId,
    string? LinkLabel,
    AssistantProposal? Proposal,
    AssistantDownload? Download,
    IReadOnlyList<AssistantLink>? Actions = null,
    bool LedgerChanged = false);

public sealed record AssistantProposal(Guid ActionId, string Summary);

public sealed record AssistantDownload(string FileName, byte[] Content);

public sealed record AssistantLink(string Href, string Label, Guid? TenancyId, string? Group = null, string? Send = null);

public sealed record AssistantCommitResult(
    string Text,
    string? Href,
    string? LinkLabel,
    Guid? TenancyId,
    AssistantDownload? Download);

public sealed record AssistantToolOutcome(string Content, AssistantLink? Link, AssistantProposal? Proposal)
{
    public static AssistantToolOutcome Text(string content) => new(content, null, null);
}

public sealed record AssistantRouteArgs(Guid? TenancyId, Guid? UnitId, int? Year, Guid? TemplateId, string? ReceiptNumber);
