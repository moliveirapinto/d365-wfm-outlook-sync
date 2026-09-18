using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WfmOutlookSync.Configuration;
using WfmOutlookSync.Models;
using WfmOutlookSync.Sync;
using WfmOutlookSync.Tests.Fakes;
using Xunit;

namespace WfmOutlookSync.Tests;

public class ShiftSyncEngineTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);

    private static (ShiftSyncEngine Engine, FakeDataverseShiftRepository Dataverse, FakeOutlookCalendarService Calendar, FakeSyncStateStore State)
        CreateEngine(SyncOptions? options = null)
    {
        var dataverse = new FakeDataverseShiftRepository();
        var calendar = new FakeOutlookCalendarService();
        var state = new FakeSyncStateStore();
        var opts = options ?? new SyncOptions();

        var time = new FakeTimeProvider(Now);

        var engine = new ShiftSyncEngine(
            dataverse, calendar, state, Options.Create(opts), time, NullLogger<ShiftSyncEngine>.Instance);

        return (engine, dataverse, calendar, state);
    }

    private static ShiftBooking MakeBooking(Guid resourceId, string status = "Committed", string activityType = "Voice calls") => new()
    {
        BookingId = Guid.NewGuid(),
        ResourceId = resourceId,
        StartTimeUtc = Now.AddHours(1),
        EndTimeUtc = Now.AddHours(3),
        Name = "Test booking",
        ShiftPlanId = Guid.NewGuid(),
        ShiftPlanName = "August 2026 Schedule",
        ShiftActivityTypeId = Guid.NewGuid(),
        ShiftActivityTypeName = activityType,
        BookingStatusId = Guid.NewGuid(),
        BookingStatusName = status,
        StateCode = 0,
        ModifiedOnUtc = Now
    };

    private static AgentResource MakeAgent(Guid resourceId, string mailbox = "agent@contoso.com", bool disabled = false) => new()
    {
        ResourceId = resourceId,
        UserId = Guid.NewGuid(),
        ResourceName = "Test Agent",
        Mailbox = mailbox,
        IsDisabled = disabled
    };

    [Fact]
    public async Task PublishedBooking_WithMappedAgent_CreatesOutlookEvent()
    {
        var (engine, dataverse, calendar, _) = CreateEngine();
        var resourceId = Guid.NewGuid();
        dataverse.Agents[resourceId] = MakeAgent(resourceId);
        dataverse.Bookings.Add(MakeBooking(resourceId));

        var result = await engine.RunAsync(CancellationToken.None);

        Assert.Equal(1, result.EventsCreated);
        Assert.Equal(0, result.EventsUpdated);
        Assert.Single(calendar.Events);
    }

    [Fact]
    public async Task UnchangedBooking_OnSecondRun_DoesNotCallGraphAgain()
    {
        var (engine, dataverse, calendar, _) = CreateEngine();
        var resourceId = Guid.NewGuid();
        dataverse.Agents[resourceId] = MakeAgent(resourceId);
        dataverse.Bookings.Add(MakeBooking(resourceId));

        await engine.RunAsync(CancellationToken.None);
        Assert.Equal(1, calendar.UpsertCalls);

        var result2 = await engine.RunAsync(CancellationToken.None);

        Assert.Equal(1, calendar.UpsertCalls); // no additional call - content unchanged
        Assert.Equal(0, result2.EventsCreated);
        Assert.Equal(0, result2.EventsUpdated);
    }

    [Fact]
    public async Task BookingWithoutMappedAgent_IsSkipped()
    {
        var (engine, dataverse, calendar, _) = CreateEngine();
        dataverse.Bookings.Add(MakeBooking(Guid.NewGuid()));

        var result = await engine.RunAsync(CancellationToken.None);

        Assert.Equal(1, result.EventsSkippedNoMailbox);
        Assert.Empty(calendar.Events);
    }

    [Fact]
    public async Task UnpublishedBooking_IsNotSynced()
    {
        var (engine, dataverse, calendar, _) = CreateEngine();
        var resourceId = Guid.NewGuid();
        dataverse.Agents[resourceId] = MakeAgent(resourceId);
        dataverse.Bookings.Add(MakeBooking(resourceId, status: "Unpublished"));

        var result = await engine.RunAsync(CancellationToken.None);

        Assert.Equal(1, result.EventsSkippedUnpublished);
        Assert.Empty(calendar.Events);
    }

    [Fact]
    public async Task CanceledBooking_RemovesPreviouslySyncedEvent()
    {
        var (engine, dataverse, calendar, _) = CreateEngine();
        var resourceId = Guid.NewGuid();
        dataverse.Agents[resourceId] = MakeAgent(resourceId);
        var booking = MakeBooking(resourceId);
        dataverse.Bookings.Add(booking);

        await engine.RunAsync(CancellationToken.None);
        Assert.Single(calendar.Events);

        dataverse.Bookings.Clear(); // simulate the shift being deleted/canceled upstream

        var result = await engine.RunAsync(CancellationToken.None);

        Assert.Equal(1, result.EventsDeleted);
        Assert.Empty(calendar.Events);
    }

    [Fact]
    public async Task ExcludedActivityType_IsNeverSynced()
    {
        var options = new SyncOptions { ExcludedShiftActivityTypeNames = "Lunch break" };
        var (engine, dataverse, calendar, _) = CreateEngine(options);
        var resourceId = Guid.NewGuid();
        dataverse.Agents[resourceId] = MakeAgent(resourceId);
        dataverse.Bookings.Add(MakeBooking(resourceId, activityType: "Lunch break"));

        var result = await engine.RunAsync(CancellationToken.None);

        Assert.Equal(0, result.EventsCreated);
        Assert.Empty(calendar.Events);
    }

    [Fact]
    public async Task DryRun_NeverCallsGraphOrPersistsState()
    {
        var options = new SyncOptions { DryRun = true };
        var (engine, dataverse, calendar, state) = CreateEngine(options);
        var resourceId = Guid.NewGuid();
        dataverse.Agents[resourceId] = MakeAgent(resourceId);
        dataverse.Bookings.Add(MakeBooking(resourceId));

        var result = await engine.RunAsync(CancellationToken.None);

        Assert.Equal(1, result.EventsCreated);
        Assert.Empty(calendar.Events);
        Assert.Empty(state.Records);
    }

    [Fact]
    public async Task DisabledAgent_IsSkipped()
    {
        var (engine, dataverse, calendar, _) = CreateEngine();
        var resourceId = Guid.NewGuid();
        dataverse.Agents[resourceId] = MakeAgent(resourceId, disabled: true);
        dataverse.Bookings.Add(MakeBooking(resourceId));

        var result = await engine.RunAsync(CancellationToken.None);

        Assert.Equal(1, result.EventsSkippedNoMailbox);
        Assert.Empty(calendar.Events);
    }

    [Fact]
    public async Task ForceResync_ReUpsertsEvenWhenUnchanged()
    {
        var (engine, dataverse, calendar, _) = CreateEngine();
        var resourceId = Guid.NewGuid();
        dataverse.Agents[resourceId] = MakeAgent(resourceId);
        dataverse.Bookings.Add(MakeBooking(resourceId));

        await engine.RunAsync(CancellationToken.None);
        Assert.Equal(1, calendar.UpsertCalls);

        var result = await engine.RunAsync(forceResync: true, CancellationToken.None);

        Assert.Equal(2, calendar.UpsertCalls);
        Assert.True(result.UsedFullResync);
    }
}
