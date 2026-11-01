namespace Ashfall.Sim;

public enum LogKind
{
    Info,
    Good,
    Warning,
    Bad,
}

public sealed record LogEntry(long Tick, LogKind Kind, string Text);

public sealed class EventLog
{
    private readonly List<LogEntry> _entries = new();
    public IReadOnlyList<LogEntry> Entries => _entries;

    public event Action<LogEntry>? Added;

    public void Add(long tick, LogKind kind, string text)
    {
        var e = new LogEntry(tick, kind, text);
        _entries.Add(e);
        if (_entries.Count > 500) _entries.RemoveRange(0, 100);
        Added?.Invoke(e);
    }

    public IEnumerable<LogEntry> Recent(int count) => _entries.Skip(Math.Max(0, _entries.Count - count));
}
