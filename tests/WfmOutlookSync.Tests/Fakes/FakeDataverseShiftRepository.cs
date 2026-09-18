using WfmOutlookSync.Dataverse;
using WfmOutlookSync.Models;

namespace WfmOutlookSync.Tests.Fakes;

public sealed class FakeDataverseShiftRepository : IDataverseShiftRepository
{
    public Dictionary<Guid, AgentResource> Agents { get; } = new();
    public List<ShiftBooking> Bookings { get; } = new();

    public Task<IReadOnlyDictionary<Guid, AgentResource>> GetAgentResourcesAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyDictionary<Guid, AgentResource>>(Agents);

    public Task<IReadOnlyList<ShiftBooking>> GetShiftBookingsInWindowAsync(
        DateTimeOffset windowStartUtc, DateTimeOffset windowEndUtc, CancellationToken ct)
    {
        var result = Bookings
            .Where(b => b.StartTimeUtc <= windowEndUtc && b.EndTimeUtc >= windowStartUtc)
            .ToList();

        return Task.FromResult<IReadOnlyList<ShiftBooking>>(result);
    }
}
