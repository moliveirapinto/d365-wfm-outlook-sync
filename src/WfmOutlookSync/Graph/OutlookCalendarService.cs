using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using WfmOutlookSync.Configuration;
using WfmOutlookSync.Models;

namespace WfmOutlookSync.Graph;

/// <summary>
/// Microsoft Graph implementation of <see cref="IOutlookCalendarService"/>. Every event this
/// tool writes carries a single-value extended property containing the originating Dataverse
/// booking ID, so events can always be found and reconciled even if the local state cache
/// (Azure Table) is lost or reset.
/// </summary>
public sealed class OutlookCalendarService : IOutlookCalendarService
{
    // Fixed internal identifier for this tool's Outlook extended-property "property set".
    // Not an environment-specific value - every deployment of this tool uses the same tag
    // format so events remain self-describing; the *name* segment is still configurable.
    private const string PropertySetGuid = "c1a19c0a-0a3e-4f0a-9f0e-7c3b2a5f9a11";

    private readonly GraphServiceClient _graph;
    private readonly SyncOptions _options;

    public OutlookCalendarService(GraphServiceClient graph, Microsoft.Extensions.Options.IOptions<SyncOptions> options)
    {
        _graph = graph;
        _options = options.Value;
    }

    private string PropertyId => $"String {{{PropertySetGuid}}} Name {_options.ExtendedPropertyNamespace}";

    public async Task<string?> FindExistingEventIdAsync(string mailbox, Guid bookingId, CancellationToken ct)
    {
        var filter = $"singleValueExtendedProperties/any(ep: ep/id eq '{PropertyId}' and ep/value eq '{bookingId:D}')";

        var page = await _graph.Users[mailbox].Events.GetAsync(rc =>
        {
            rc.QueryParameters.Filter = filter;
            rc.QueryParameters.Top = 1;
            rc.QueryParameters.Select = new[] { "id" };
        }, ct);

        return page?.Value?.FirstOrDefault()?.Id;
    }

    public async Task<string> UpsertEventAsync(string mailbox, ShiftBooking booking, string? existingEventId, CancellationToken ct)
    {
        var subject = Sync.SubjectTemplateFormatter.Format(
            _options.EventSubjectTemplate, booking.ShiftActivityTypeName, booking.ShiftPlanName, booking.Name);

        var @event = new Event
        {
            Subject = subject,
            Start = new DateTimeTimeZone { DateTime = booking.StartTimeUtc.UtcDateTime.ToString("o"), TimeZone = "UTC" },
            End = new DateTimeTimeZone { DateTime = booking.EndTimeUtc.UtcDateTime.ToString("o"), TimeZone = "UTC" },
            Categories = new List<string> { _options.EventCategory },
            ShowAs = ParseShowAs(_options.ShowAs),
            IsReminderOn = _options.SetReminder,
            ReminderMinutesBeforeStart = _options.ReminderMinutesBeforeStart,
            Body = new ItemBody
            {
                ContentType = BodyType.Text,
                Content = BuildBody(booking)
            },
            SingleValueExtendedProperties = new List<SingleValueLegacyExtendedProperty>
            {
                new() { Id = PropertyId, Value = booking.BookingId.ToString("D") }
            }
        };

        if (existingEventId is not null)
        {
            var updated = await _graph.Users[mailbox].Events[existingEventId].PatchAsync(@event, cancellationToken: ct);
            return updated?.Id ?? existingEventId;
        }

        var created = await _graph.Users[mailbox].Events.PostAsync(@event, cancellationToken: ct);
        return created?.Id ?? throw new InvalidOperationException("Graph did not return an event id after creation.");
    }

    public async Task DeleteEventAsync(string mailbox, string eventId, CancellationToken ct)
    {
        try
        {
            await _graph.Users[mailbox].Events[eventId].DeleteAsync(cancellationToken: ct);
        }
        catch (ODataError ex) when (ex.ResponseStatusCode == 404)
        {
            // already gone - nothing to do
        }
    }

    private static string BuildBody(ShiftBooking booking) =>
        $"Synced from Dynamics 365 Workforce Engagement Management.\n" +
        $"Shift plan: {booking.ShiftPlanName}\n" +
        $"Activity: {booking.ShiftActivityTypeName}\n" +
        $"Status: {booking.BookingStatusName}\n" +
        $"This event is kept in sync automatically - manual edits will be overwritten.";

    private static FreeBusyStatus ParseShowAs(string value) => value.Trim().ToLowerInvariant() switch
    {
        "free" => FreeBusyStatus.Free,
        "tentative" => FreeBusyStatus.Tentative,
        "oof" => FreeBusyStatus.Oof,
        "workingelsewhere" => FreeBusyStatus.WorkingElsewhere,
        _ => FreeBusyStatus.Busy
    };
}
