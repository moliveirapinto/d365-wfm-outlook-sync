using WfmOutlookSync.Graph;
using WfmOutlookSync.Models;

namespace WfmOutlookSync.Tests.Fakes;

public sealed class FakeOutlookCalendarService : IOutlookCalendarService
{
    private int _nextEventId = 1;

    public Dictionary<string, (string Mailbox, Guid BookingId)> Events { get; } = new();
    public int UpsertCalls { get; private set; }
    public int DeleteCalls { get; private set; }

    public Task<string?> FindExistingEventIdAsync(string mailbox, Guid bookingId, CancellationToken ct)
    {
        var found = Events.FirstOrDefault(e => e.Value.Mailbox == mailbox && e.Value.BookingId == bookingId);
        return Task.FromResult<string?>(found.Key);
    }

    public Task<string> UpsertEventAsync(string mailbox, ShiftBooking booking, string? existingEventId, CancellationToken ct)
    {
        UpsertCalls++;
        var id = existingEventId ?? $"event-{_nextEventId++}";
        Events[id] = (mailbox, booking.BookingId);
        return Task.FromResult(id);
    }

    public Task DeleteEventAsync(string mailbox, string eventId, CancellationToken ct)
    {
        DeleteCalls++;
        Events.Remove(eventId);
        return Task.CompletedTask;
    }
}
