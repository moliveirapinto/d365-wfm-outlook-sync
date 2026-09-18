using WfmOutlookSync.State;

namespace WfmOutlookSync.Tests.Fakes;

public sealed class FakeSyncStateStore : ISyncStateStore
{
    public Dictionary<string, BookingSyncRecord> Records { get; } = new();

    public Task<BookingSyncRecord?> GetBookingRecordAsync(Guid bookingId, CancellationToken ct) =>
        Task.FromResult(Records.TryGetValue(BookingSyncRecord.BuildRowKey(bookingId), out var r) ? Clone(r) : null);

    public Task UpsertBookingRecordAsync(BookingSyncRecord record, CancellationToken ct)
    {
        Records[record.RowKey] = Clone(record);
        return Task.CompletedTask;
    }

    public Task DeleteBookingRecordAsync(Guid bookingId, CancellationToken ct)
    {
        Records.Remove(BookingSyncRecord.BuildRowKey(bookingId));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<BookingSyncRecord>> GetRecordsOverlappingWindowAsync(
        DateTimeOffset windowStartUtc, DateTimeOffset windowEndUtc, CancellationToken ct)
    {
        var results = Records.Values
            .Where(r => r.ShiftStartUtc <= windowEndUtc && r.ShiftEndUtc >= windowStartUtc)
            .Select(Clone)
            .ToList();

        return Task.FromResult<IReadOnlyList<BookingSyncRecord>>(results);
    }

    private static BookingSyncRecord Clone(BookingSyncRecord r) => new()
    {
        PartitionKey = r.PartitionKey,
        RowKey = r.RowKey,
        AgentMailbox = r.AgentMailbox,
        GraphEventId = r.GraphEventId,
        ContentHash = r.ContentHash,
        ShiftStartUtc = r.ShiftStartUtc,
        ShiftEndUtc = r.ShiftEndUtc,
        LastSyncedUtc = r.LastSyncedUtc
    };
}
