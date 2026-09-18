namespace WfmOutlookSync.Configuration;

/// <summary>
/// How a component authenticates to Azure AD / Microsoft Entra ID.
/// ManagedIdentity requires no secrets and is the recommended mode for production.
/// </summary>
public enum AuthMode
{
    ManagedIdentity,
    ClientSecret
}

/// <summary>
/// Connection settings for the Dataverse environment that hosts the WFM (Workforce
/// Engagement Management) shift data. Bound from the "Dataverse" configuration section.
/// </summary>
public class DataverseOptions
{
    public const string SectionName = "Dataverse";

    /// <summary>Base URL of the Dataverse environment, e.g. https://yourorg.crm.dynamics.com</summary>
    public string EnvironmentUrl { get; set; } = string.Empty;

    public AuthMode AuthMode { get; set; } = AuthMode.ManagedIdentity;

    /// <summary>Required when AuthMode is ClientSecret.</summary>
    public string? TenantId { get; set; }

    /// <summary>Required when AuthMode is ClientSecret.</summary>
    public string? ClientId { get; set; }

    /// <summary>Required when AuthMode is ClientSecret. Should come from Key Vault, never plain text.</summary>
    public string? ClientSecret { get; set; }

    /// <summary>Optional: set when the Function App uses a user-assigned managed identity.</summary>
    public string? UserAssignedManagedIdentityClientId { get; set; }

    public string WebApiBaseUrl => EnvironmentUrl.TrimEnd('/') + "/api/data/v9.2";
}

/// <summary>
/// Connection settings for the Microsoft Graph application used to read/write Outlook
/// calendars on behalf of agents. Bound from the "Graph" configuration section.
/// </summary>
public class GraphOptions
{
    public const string SectionName = "Graph";

    public AuthMode AuthMode { get; set; } = AuthMode.ManagedIdentity;

    public string? TenantId { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? UserAssignedManagedIdentityClientId { get; set; }
}

/// <summary>
/// Business rules that govern which shifts are synced and how the resulting Outlook
/// events look. Every value has a sensible default so the app runs out-of-the-box, but
/// nothing here is hardcoded - all of it is overridable via app settings / environment
/// variables without touching code. Bound from the "Sync" configuration section.
/// </summary>
public class SyncOptions
{
    public const string SectionName = "Sync";

    /// <summary>How many days in the past to include when scanning for shifts (catches recently-started/ended shifts).</summary>
    public int LookBehindDays { get; set; } = 1;

    /// <summary>How many days in the future to include when scanning for shifts.</summary>
    public int LookAheadDays { get; set; } = 30;

    /// <summary>
    /// Comma-separated msdyn_shiftactivitytype names to exclude (e.g. skip syncing unpaid
    /// personal time). Empty by default - everything on a published shift is synced. A plain
    /// comma-separated string (rather than an indexed array) so it can be edited as a single
    /// Azure Portal app setting without redeploying.
    /// </summary>
    public string ExcludedShiftActivityTypeNames { get; set; } = string.Empty;

    /// <summary>
    /// Comma-separated bookingstatus names considered "published"/final. Only bookings whose
    /// status name is in this list are pushed to Outlook. Anything else (e.g. Unpublished,
    /// Proposed, Canceled) is treated as not-yet-real and is skipped or removed.
    /// </summary>
    public string PublishedBookingStatusNames { get; set; } = "Committed,Scheduled";

    public IReadOnlyCollection<string> ExcludedShiftActivityTypeNameList => SplitCsv(ExcludedShiftActivityTypeNames);

    public IReadOnlyCollection<string> PublishedBookingStatusNameList => SplitCsv(PublishedBookingStatusNames);

    private static string[] SplitCsv(string csv) =>
        csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// Template for the Outlook event subject. Supported tokens: {ShiftActivityType},
    /// {ShiftPlanName}, {ResourceName}.
    /// </summary>
    public string EventSubjectTemplate { get; set; } = "{ShiftActivityType} \u2014 {ShiftPlanName}";

    /// <summary>Outlook category label applied to every event this tool creates.</summary>
    public string EventCategory { get; set; } = "WFM Shift";

    /// <summary>Outlook "show as" value: free, tentative, busy, oof, workingElsewhere.</summary>
    public string ShowAs { get; set; } = "busy";

    public bool SetReminder { get; set; } = false;

    public int ReminderMinutesBeforeStart { get; set; } = 15;

    /// <summary>When a previously-synced shift is canceled/removed in Dataverse, delete the Outlook event.</summary>
    public bool DeleteWhenCanceled { get; set; } = true;

    /// <summary>When true, computes what would happen but performs no writes to Outlook.</summary>
    public bool DryRun { get; set; } = false;

    /// <summary>
    /// Namespace used for the Outlook single-value extended property that tags events
    /// created by this tool, enabling idempotent upsert and reconciliation.
    /// </summary>
    public string ExtendedPropertyNamespace { get; set; } = "WfmOutlookSync";

    public int MaxDegreeOfParallelism { get; set; } = 4;
}

/// <summary>
/// Where the tool persists its Dataverse change-tracking cursor and the mapping between
/// a Dataverse shift booking and the Outlook event it produced. Bound from "SyncState".
/// </summary>
public class SyncStateOptions
{
    public const string SectionName = "SyncState";

    public string TableName { get; set; } = "wfmSyncState";

    /// <summary>
    /// Table Storage endpoint, e.g. https://youraccount.table.core.windows.net.
    /// When empty, falls back to the storage account referenced by AzureWebJobsStorage
    /// (via connection string) so a separate account is not required.
    /// </summary>
    public string? StorageAccountUri { get; set; }
}
