namespace WfmOutlookSync.State;

/// <summary>Persists the booking-to-event correlation cache used to make sync idempotent and to detect deletions.</summary>
public interface ISyncStateStore
{
    Task<BookingSyncRecord?> GetBookingRecordAsync(Guid bookingId, CancellationToken ct);

    Task UpsertBookingRecordAsync(BookingSyncRecord record, CancellationToken ct);

    Task DeleteBookingRecordAsync(Guid bookingId, CancellationToken ct);

    /// <summary>
    /// Returns every previously-synced booking whose shift overlaps the given window,
    /// used to detect bookings that were canceled/deleted upstream since the last run.
    /// </summary>
    Task<IReadOnlyList<BookingSyncRecord>> GetRecordsOverlappingWindowAsync(
        DateTimeOffset windowStartUtc, DateTimeOffset windowEndUtc, CancellationToken ct);
}
