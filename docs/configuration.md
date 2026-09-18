# Configuration reference

Every setting below is an Azure Function app setting (or `local.settings.json` value for
local development). Nothing is hardcoded in source - change any of these without
redeploying code, and (for most) without even restarting the app.

Section names map to `Section:Key` in configuration / `Section__Key` as an app setting or
environment variable name (double underscore = hierarchy separator).

## Dataverse connection (`Dataverse`)

| Setting | Default | Description |
|---|---|---|
| `Dataverse__EnvironmentUrl` | *(required)* | e.g. `https://yourorg.crm.dynamics.com` |
| `Dataverse__AuthMode` | `ManagedIdentity` | `ManagedIdentity` (recommended, no secrets) or `ClientSecret` |
| `Dataverse__TenantId` | | Required when `AuthMode=ClientSecret` |
| `Dataverse__ClientId` | | Required when `AuthMode=ClientSecret` |
| `Dataverse__ClientSecret` | | Required when `AuthMode=ClientSecret`. Store in Key Vault, reference via `@Microsoft.KeyVault(...)` |
| `Dataverse__UserAssignedManagedIdentityClientId` | | Only if the Function App uses a *user-assigned* identity instead of system-assigned |

## Microsoft Graph / Outlook connection (`Graph`)

Same shape as `Dataverse`, configured independently (you may use managed identity for one
and a client secret for the other): `Graph__AuthMode`, `Graph__TenantId`, `Graph__ClientId`,
`Graph__ClientSecret`, `Graph__UserAssignedManagedIdentityClientId`.

## Sync behavior (`Sync`)

| Setting | Default | Description |
|---|---|---|
| `Sync__LookBehindDays` | `1` | Days in the past included in the sync window |
| `Sync__LookAheadDays` | `30` | Days in the future included in the sync window |
| `Sync__PublishedBookingStatusNames` | `Committed,Scheduled` | Comma-separated `bookingstatus` names treated as final/agent-facing. Everything else is skipped, or deleted if previously synced |
| `Sync__ExcludedShiftActivityTypeNames` | *(empty)* | Comma-separated `msdyn_shiftactivitytype` names to never sync (e.g. `Lunch break`) |
| `Sync__EventSubjectTemplate` | `{ShiftActivityType} - {ShiftPlanName}` | Outlook event subject. Tokens: `{ShiftActivityType}`, `{ShiftPlanName}`, `{ResourceName}` |
| `Sync__EventCategory` | `WFM Shift` | Outlook category label applied to every synced event |
| `Sync__ShowAs` | `busy` | `free`, `tentative`, `busy`, `oof`, or `workingElsewhere` |
| `Sync__SetReminder` | `false` | Whether Outlook should show a reminder for these events |
| `Sync__ReminderMinutesBeforeStart` | `15` | Only used when `SetReminder=true` |
| `Sync__DeleteWhenCanceled` | `true` | Remove the Outlook event when a previously-synced shift is canceled/deleted upstream |
| `Sync__DryRun` | `false` | Compute and log the diff, but make no Graph writes |
| `Sync__ExtendedPropertyNamespace` | `WfmOutlookSync` | Name segment of the Outlook extended property used to tag/find this tool's own events |
| `Sync__MaxDegreeOfParallelism` | `4` | Reserved for future parallelization of the upsert loop |

## Schedule

| Setting | Default | Description |
|---|---|---|
| `SYNC_CRON_SCHEDULE` | `0 */5 * * * *` | NCRONTAB expression for the timer trigger. `0 */5 * * * *` = every 5 minutes. Change and restart the app - no redeploy needed |

## Sync state storage (`SyncState`)

| Setting | Default | Description |
|---|---|---|
| `SyncState__TableName` | `wfmSyncState` | Azure Table name for the booking&rarr;event correlation cache |
| `SyncState__StorageAccountUri` | *(empty)* | Table endpoint, e.g. `https://youraccount.table.core.windows.net`. When empty, reuses the storage account configured via `AzureWebJobsStorage` |

## On-demand endpoints

| Endpoint | Auth | Purpose |
|---|---|---|
| `GET /api/status` | Anonymous | Health check; shows non-secret effective config |
| `POST /api/sync` | Function key | Run a sync immediately |
| `POST /api/sync?force=true` | Function key | Re-push every eligible booking even if unchanged - useful after changing `EventSubjectTemplate`, `EventCategory`, etc. so existing events pick up the new format |

## Choosing a sync interval

There is no "correct" interval - it's a tradeoff between freshness and Dataverse/Graph API
load. Every run's cost scales with the number of *changed* bookings (unchanged ones are
skipped via the content hash), so frequent runs are cheap. `0 */5 * * * *` (5 minutes) is a
reasonable default for most contact centers; a smaller operation could go to `0 */1 * * * *`
(every minute) and a very large one might prefer `0 */15 * * * *`.
