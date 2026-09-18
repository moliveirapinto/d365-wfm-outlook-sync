using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WfmOutlookSync.Configuration;
using WfmOutlookSync.Dataverse;
using WfmOutlookSync.Graph;
using WfmOutlookSync.Models;
using WfmOutlookSync.State;

namespace WfmOutlookSync.Sync;

/// <summary>
/// Core orchestration: reads published WFM shifts for a rolling window from Dataverse,
/// diffs them against what was synced last time, and creates/updates/deletes the
/// corresponding Outlook calendar events. Stateless between calls except for the
/// <see cref="ISyncStateStore"/> cache, so it is safe to run concurrently-avoiding via the
/// timer trigger's own singleton execution guarantee.
/// </summary>
public sealed class ShiftSyncEngine
{
    private readonly IDataverseShiftRepository _dataverse;
    private readonly IOutlookCalendarService _calendar;
    private readonly ISyncStateStore _state;
    private readonly SyncOptions _options;
    private readonly TimeProvider _time;
    private readonly ILogger<ShiftSyncEngine> _logger;

    public ShiftSyncEngine(
        IDataverseShiftRepository dataverse,
        IOutlookCalendarService calendar,
        ISyncStateStore state,
        IOptions<SyncOptions> options,
        TimeProvider time,
        ILogger<ShiftSyncEngine> logger)
    {
        _dataverse = dataverse;
        _calendar = calendar;
        _state = state;
        _options = options.Value;
        _time = time;
        _logger = logger;
    }

    public async Task<SyncResult> RunAsync(CancellationToken ct) => await RunAsync(forceResync: false, ct);

    public async Task<SyncResult> RunAsync(bool forceResync, CancellationToken ct)
    {
        var result = new SyncResult { StartedAtUtc = _time.GetUtcNow(), DryRun = _options.DryRun, UsedFullResync = forceResync };

        var now = _time.GetUtcNow();
        var windowStart = now.AddDays(-Math.Abs(_options.LookBehindDays));
        var windowEnd = now.AddDays(Math.Abs(_options.LookAheadDays));

        var agents = await _dataverse.GetAgentResourcesAsync(ct);
        var bookings = await _dataverse.GetShiftBookingsInWindowAsync(windowStart, windowEnd, ct);
        result.BookingsEvaluated = bookings.Count;

        var excluded = new HashSet<string>(_options.ExcludedShiftActivityTypeNameList, StringComparer.OrdinalIgnoreCase);

        var eligible = bookings
            .Where(b => b.ShiftActivityTypeName is null || !excluded.Contains(b.ShiftActivityTypeName))
            .ToList();

        var published = eligible.Where(b => b.IsPublished(_options.PublishedBookingStatusNameList)).ToList();
        result.EventsSkippedUnpublished = eligible.Count - published.Count;

        var currentIds = new HashSet<Guid>(published.Select(b => b.BookingId));

        if (_options.DeleteWhenCanceled)
        {
            var previousRecords = await _state.GetRecordsOverlappingWindowAsync(windowStart, windowEnd, ct);
            foreach (var previous in previousRecords)
            {
                if (currentIds.Contains(Guid.Parse(previous.RowKey)))
                {
                    continue;
                }

                try
                {
                    if (!_options.DryRun)
                    {
                        await _calendar.DeleteEventAsync(previous.AgentMailbox, previous.GraphEventId, ct);
                        await _state.DeleteBookingRecordAsync(Guid.Parse(previous.RowKey), ct);
                    }

                    result.EventsDeleted++;
                }
                catch (Exception ex)
                {
                    result.Failures++;
                    result.Errors.Add($"Delete failed for booking {previous.RowKey}: {ex.Message}");
                    _logger.LogError(ex, "Failed to delete Outlook event for booking {BookingId}", previous.RowKey);
                }
            }
        }

        foreach (var booking in published)
        {
            ct.ThrowIfCancellationRequested();

            if (!agents.TryGetValue(booking.ResourceId, out var agent) || agent.IsDisabled)
            {
                result.EventsSkippedNoMailbox++;
                continue;
            }

            try
            {
                await SyncOneAsync(booking, agent, result, forceResync, ct);
            }
            catch (Exception ex)
            {
                result.Failures++;
                result.Errors.Add($"Sync failed for booking {booking.BookingId}: {ex.Message}");
                _logger.LogError(ex, "Failed to sync booking {BookingId} for {Mailbox}", booking.BookingId, agent.Mailbox);
            }
        }

        result.CompletedAtUtc = _time.GetUtcNow();
        return result;
    }

    private async Task SyncOneAsync(ShiftBooking booking, AgentResource agent, SyncResult result, bool forceResync, CancellationToken ct)
    {
        var subject = SubjectTemplateFormatter.Format(
            _options.EventSubjectTemplate, booking.ShiftActivityTypeName, booking.ShiftPlanName, booking.Name);

        var contentHash = ComputeHash(agent.Mailbox, subject, booking.StartTimeUtc, booking.EndTimeUtc, _options.EventCategory, _options.ShowAs);

        var existing = await _state.GetBookingRecordAsync(booking.BookingId, ct);

        if (!forceResync && existing is not null && existing.ContentHash == contentHash && existing.AgentMailbox == agent.Mailbox)
        {
            // Nothing changed since the last successful sync - skip the Graph call entirely.
            return;
        }

        var eventId = existing?.GraphEventId;
        if (string.IsNullOrEmpty(eventId))
        {
            // Self-heal: the state cache may have been reset - check Outlook itself before creating a duplicate.
            eventId = await _calendar.FindExistingEventIdAsync(agent.Mailbox, booking.BookingId, ct);
        }

        var isNew = string.IsNullOrEmpty(eventId);

        if (!_options.DryRun)
        {
            eventId = await _calendar.UpsertEventAsync(agent.Mailbox, booking, eventId, ct);

            await _state.UpsertBookingRecordAsync(new BookingSyncRecord
            {
                RowKey = BookingSyncRecord.BuildRowKey(booking.BookingId),
                AgentMailbox = agent.Mailbox,
                GraphEventId = eventId!,
                ContentHash = contentHash,
                ShiftStartUtc = booking.StartTimeUtc,
                ShiftEndUtc = booking.EndTimeUtc,
                LastSyncedUtc = _time.GetUtcNow()
            }, ct);
        }

        if (isNew)
        {
            result.EventsCreated++;
        }
        else
        {
            result.EventsUpdated++;
        }
    }

    private static string ComputeHash(params object[] parts)
    {
        var raw = string.Join('|', parts.Select(p => p.ToString()));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes);
    }
}
