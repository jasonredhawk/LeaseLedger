namespace LeaseLedger.Application;

public sealed class AssistantActionStore
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, Pending> _pending = [];

    public AssistantProposal Stage(Guid userId, string summary, Func<ILeaseLedgerClient, CancellationToken, Task<AssistantCommitResult>> execute)
    {
        var id = Guid.NewGuid();
        lock (_gate)
            _pending[id] = new Pending(userId, execute);

        return new AssistantProposal(id, summary);
    }

    public async Task<AssistantCommitResult> ConfirmAsync(Guid actionId, Guid userId, ILeaseLedgerClient client, CancellationToken cancellationToken = default)
    {
        Pending pending;
        lock (_gate)
        {
            if (!_pending.TryGetValue(actionId, out pending!))
                throw new LeaseLedgerException("That confirmation expired. Ask again.");
            if (pending.UserId != userId)
                throw new LeaseLedgerException("That confirmation belongs to another session.");
            if (pending.Started)
                throw new LeaseLedgerException("That confirmation is already running.");

            pending.Started = true;
        }

        try
        {
            var result = await pending.Execute(client, cancellationToken);
            lock (_gate)
                _pending.Remove(actionId);
            return result;
        }
        catch
        {
            lock (_gate)
                pending.Started = false;
            throw;
        }
    }

    private sealed class Pending(Guid userId, Func<ILeaseLedgerClient, CancellationToken, Task<AssistantCommitResult>> execute)
    {
        public Guid UserId { get; } = userId;
        public Func<ILeaseLedgerClient, CancellationToken, Task<AssistantCommitResult>> Execute { get; } = execute;
        public bool Started { get; set; }
    }
}
