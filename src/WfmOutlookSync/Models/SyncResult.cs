namespace WfmOutlookSync.Models;

/// <summary>Outcome of a single sync pass, returned by the timer function / HTTP endpoints and logged.</summary>
public sealed class SyncResult
{
    public int BookingsEvaluated { get; set; }
    public int EventsCreated { get; set; }
    public int EventsUpdated { get; set; }
    public int EventsDeleted { get; set; }
    public int EventsSkippedNoMailbox { get; set; }
    public int EventsSkippedUnpublished { get; set; }
    public int Failures { get; set; }
    public bool DryRun { get; set; }
    public bool UsedFullResync { get; set; }
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset CompletedAtUtc { get; set; }
    public List<string> Errors { get; } = new();

    public TimeSpan Duration => CompletedAtUtc - StartedAtUtc;
}
