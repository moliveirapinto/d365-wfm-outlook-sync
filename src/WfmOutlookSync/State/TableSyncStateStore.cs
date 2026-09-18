using Azure;
using Azure.Data.Tables;

namespace WfmOutlookSync.State;

/// <summary>Azure Table Storage implementation of <see cref="ISyncStateStore"/>.</summary>
public sealed class TableSyncStateStore : ISyncStateStore
{
    private readonly TableClient _table;

    public TableSyncStateStore(TableClient table)
    {
        _table = table;
    }

    public async Task<BookingSyncRecord?> GetBookingRecordAsync(Guid bookingId, CancellationToken ct)
    {
        try
        {
            var response = await _table.GetEntityAsync<BookingSyncRecord>(
                BookingSyncRecord.PartitionKeyValue, BookingSyncRecord.BuildRowKey(bookingId), cancellationToken: ct);
            return response.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task UpsertBookingRecordAsync(BookingSyncRecord record, CancellationToken ct)
    {
        await _table.UpsertEntityAsync(record, TableUpdateMode.Replace, ct);
    }

    public async Task DeleteBookingRecordAsync(Guid bookingId, CancellationToken ct)
    {
        try
        {
            await _table.DeleteEntityAsync(BookingSyncRecord.PartitionKeyValue, BookingSyncRecord.BuildRowKey(bookingId), cancellationToken: ct);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // already absent
        }
    }

    public async Task<IReadOnlyList<BookingSyncRecord>> GetRecordsOverlappingWindowAsync(
        DateTimeOffset windowStartUtc, DateTimeOffset windowEndUtc, CancellationToken ct)
    {
        // Table Storage has no server-side range index beyond PartitionKey/RowKey, but the
        // partition (one per deployment) stays small for a typical WFM roster, so a filtered
        // partition scan is fast and avoids needing a second index table.
        var results = new List<BookingSyncRecord>();
        var query = _table.QueryAsync<BookingSyncRecord>(
            r => r.PartitionKey == BookingSyncRecord.PartitionKeyValue
                 && r.ShiftStartUtc <= windowEndUtc
                 && r.ShiftEndUtc >= windowStartUtc,
            cancellationToken: ct);

        await foreach (var entity in query)
        {
            results.Add(entity);
        }

        return results;
    }
}

