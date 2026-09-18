using WfmOutlookSync.Models;

namespace WfmOutlookSync.Graph;

/// <summary>Creates/updates/removes the Outlook calendar events that represent WFM shifts.</summary>
public interface IOutlookCalendarService
{
    /// <summary>
    /// Looks for a previously-created event tagged with this booking's ID via the
    /// extended property (used for self-healing when the local state cache is empty/lost).
    /// </summary>
    Task<string?> FindExistingEventIdAsync(string mailbox, Guid bookingId, CancellationToken ct);

    /// <summary>Creates a new event, or updates it in place when <paramref name="existingEventId"/> is supplied.</summary>
    Task<string> UpsertEventAsync(
        string mailbox, ShiftBooking booking, string? existingEventId, CancellationToken ct);

    Task DeleteEventAsync(string mailbox, string eventId, CancellationToken ct);
}
