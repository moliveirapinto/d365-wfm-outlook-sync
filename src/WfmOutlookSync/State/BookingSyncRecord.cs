using Azure;
using Azure.Data.Tables;

namespace WfmOutlookSync.State;

/// <summary>
/// Persisted mapping between a Dataverse shift booking and the Outlook event it produced,
/// plus a content hash so unchanged bookings are never re-written to Graph, and the shift's
/// own time range so the sync engine can detect "this booking used to be in the window and
/// is no longer returned" (i.e. it was canceled or deleted upstream) without needing
/// Dataverse change-tracking. Dataverse and Outlook remain the systems of record; this table
/// is purely a disposable cache that can be safely emptied (a subsequent run rebuilds it).
/// </summary>
public sealed class BookingSyncRecord : ITableEntity
{
    public const string PartitionKeyValue = "booking";

    public string PartitionKey { get; set; } = PartitionKeyValue;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string AgentMailbox { get; set; } = string.Empty;
    public string GraphEventId { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
    public DateTimeOffset ShiftStartUtc { get; set; }
    public DateTimeOffset ShiftEndUtc { get; set; }
    public DateTimeOffset LastSyncedUtc { get; set; }

    public static string BuildRowKey(Guid bookingId) => bookingId.ToString("D");
}
