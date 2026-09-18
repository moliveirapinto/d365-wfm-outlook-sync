using System.Text.Json;
using WfmOutlookSync.Models;

namespace WfmOutlookSync.Dataverse;

/// <summary>
/// Reads WFM shift data straight from the standard Dataverse tables used by the
/// Contact Center Workforce Engagement Management (WEM) feature:
///   bookableresourcebooking  - one row per shift activity instance (start/end, resource, status)
///   msdyn_shiftplan          - the published schedule a booking belongs to
///   msdyn_shiftactivitytype  - the kind of activity (Voice calls, Break, Lunch, ...)
///   bookableresource         - the schedulable resource (filtered to resourcetype = User = 3)
///   systemuser               - the agent's mailbox (internalemailaddress)
/// No table or field name here is configuration - these are Microsoft's fixed WFM schema -
/// but every *business rule* applied on top (which statuses count, which activity types are
/// excluded, the sync window) comes from <see cref="Configuration.SyncOptions"/>.
/// </summary>
public sealed class DataverseShiftRepository : IDataverseShiftRepository
{
    private const int UserResourceType = 3;

    private readonly DataverseWebApiClient _client;

    public DataverseShiftRepository(DataverseWebApiClient client)
    {
        _client = client;
    }

    public async Task<IReadOnlyDictionary<Guid, AgentResource>> GetAgentResourcesAsync(CancellationToken ct)
    {
        var resourceUrl =
            "bookableresources" +
            $"?$select=bookableresourceid,name,msdyn_primaryemail" +
            $"&$filter=resourcetype eq {UserResourceType} and statecode eq 0" +
            "&$expand=userid($select=systemuserid,internalemailaddress,domainname,isdisabled)";

        var rows = await _client.GetAllAsync(resourceUrl, ct);

        var result = new Dictionary<Guid, AgentResource>();
        foreach (var row in rows)
        {
            var resourceId = row.GetGuid("bookableresourceid");

            if (!row.TryGetProperty("userid", out var userElement) || userElement.ValueKind != JsonValueKind.Object)
            {
                // A "User" type resource with no linked systemuser can't receive a calendar sync.
                continue;
            }

            var userId = userElement.GetGuid("systemuserid");
            var mailbox = userElement.GetStringOrNull("internalemailaddress")
                          ?? userElement.GetStringOrNull("domainname")
                          ?? row.GetStringOrNull("msdyn_primaryemail");

            if (string.IsNullOrWhiteSpace(mailbox))
            {
                continue;
            }

            result[resourceId] = new AgentResource
            {
                ResourceId = resourceId,
                UserId = userId,
                ResourceName = row.GetStringOrNull("name"),
                Mailbox = mailbox,
                IsDisabled = userElement.GetBoolOrDefault("isdisabled")
            };
        }

        return result;
    }

    public async Task<IReadOnlyList<ShiftBooking>> GetShiftBookingsInWindowAsync(
        DateTimeOffset windowStartUtc, DateTimeOffset windowEndUtc, CancellationToken ct)
    {
        var start = windowStartUtc.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ");
        var end = windowEndUtc.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ");

        var select = string.Join(",", new[]
        {
            "bookableresourcebookingid", "name", "starttime", "endtime", "modifiedon", "statecode",
            "_resource_value", "_msdyn_shiftplan_value", "_msdyn_shiftactivitytype_value", "_bookingstatus_value"
        });

        var filter =
            $"msdyn_shiftplan ne null and starttime le {end} and endtime ge {start}";

        var url = $"bookableresourcebookings?$select={select}&$filter={Uri.EscapeDataString(filter)}";

        var rows = await _client.GetAllAsync(url, ct);

        var result = new List<ShiftBooking>(rows.Count);
        foreach (var row in rows)
        {
            var start_ = row.GetDateTimeOffsetOrNull("starttime");
            var end_ = row.GetDateTimeOffsetOrNull("endtime");
            if (start_ is null || end_ is null)
            {
                continue;
            }

            result.Add(new ShiftBooking
            {
                BookingId = row.GetGuid("bookableresourcebookingid"),
                ResourceId = row.GetGuid("_resource_value"),
                StartTimeUtc = start_.Value,
                EndTimeUtc = end_.Value,
                Name = row.GetStringOrNull("name"),
                ShiftPlanId = row.GetGuidOrNull("_msdyn_shiftplan_value"),
                ShiftPlanName = row.GetFormattedValueOrNull("_msdyn_shiftplan_value"),
                ShiftActivityTypeId = row.GetGuidOrNull("_msdyn_shiftactivitytype_value"),
                ShiftActivityTypeName = row.GetFormattedValueOrNull("_msdyn_shiftactivitytype_value"),
                BookingStatusId = row.GetGuidOrNull("_bookingstatus_value"),
                BookingStatusName = row.GetFormattedValueOrNull("_bookingstatus_value"),
                StateCode = row.GetIntOrDefault("statecode"),
                ModifiedOnUtc = row.GetDateTimeOffsetOrNull("modifiedon") ?? DateTimeOffset.UtcNow
            });
        }

        return result;
    }
}
