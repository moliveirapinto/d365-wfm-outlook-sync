using WfmOutlookSync.Models;

namespace WfmOutlookSync.Dataverse;

/// <summary>Read-only access to the WFM shift data needed to drive the Outlook sync.</summary>
public interface IDataverseShiftRepository
{
    /// <summary>
    /// All active "User" type bookable resources (WFM agents) mapped to their mailbox.
    /// Cached in memory by the caller for the lifetime of one sync run.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, AgentResource>> GetAgentResourcesAsync(CancellationToken ct);

    /// <summary>
    /// Every shift-plan-linked booking (any status, active or inactive) whose time range
    /// overlaps the given UTC window. Callers apply their own "is this published" filter.
    /// </summary>
    Task<IReadOnlyList<ShiftBooking>> GetShiftBookingsInWindowAsync(
        DateTimeOffset windowStartUtc, DateTimeOffset windowEndUtc, CancellationToken ct);
}
