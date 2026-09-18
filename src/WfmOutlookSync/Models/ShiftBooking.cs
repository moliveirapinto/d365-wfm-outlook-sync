namespace WfmOutlookSync.Models;

/// <summary>
/// A single WFM shift activity instance, projected from a Dataverse
/// bookableresourcebooking row that belongs to a msdyn_shiftplan.
/// </summary>
public sealed class ShiftBooking
{
    public required Guid BookingId { get; init; }
    public required Guid ResourceId { get; init; }
    public required DateTimeOffset StartTimeUtc { get; init; }
    public required DateTimeOffset EndTimeUtc { get; init; }
    public string? Name { get; init; }
    public string? ShiftPlanName { get; init; }
    public Guid? ShiftPlanId { get; init; }
    public string? ShiftActivityTypeName { get; init; }
    public Guid? ShiftActivityTypeId { get; init; }
    public string? BookingStatusName { get; init; }
    public Guid? BookingStatusId { get; init; }

    /// <summary>Dataverse statecode: 0 = Active, 1 = Inactive.</summary>
    public int StateCode { get; init; }

    public DateTimeOffset ModifiedOnUtc { get; init; }

    /// <summary>True when this row currently represents a real, agent-facing shift that should be on a calendar.</summary>
    public bool IsPublished(IReadOnlyCollection<string> publishedStatusNames) =>
        StateCode == 0 &&
        BookingStatusName is not null &&
        publishedStatusNames.Contains(BookingStatusName, StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// A Dataverse bookableresource of type "User" joined to its systemuser mailbox
/// information - the agent whose Outlook calendar we sync into.
/// </summary>
public sealed class AgentResource
{
    public required Guid ResourceId { get; init; }
    public required Guid UserId { get; init; }
    public string? ResourceName { get; init; }
    public required string Mailbox { get; init; }
    public bool IsDisabled { get; init; }
}
