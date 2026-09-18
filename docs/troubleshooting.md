# Troubleshooting

## `GET /api/status` returns 404 / app won't start

Check the Function App's log stream (`az functionapp log-tail` or Application Insights
"Live Metrics"). The most common cause is a missing required setting -
`Dataverse:EnvironmentUrl` has no default and the app will fail fast if it's unset.

## Sync runs but `EventsCreated`/`EventsUpdated` are always 0

1. Confirm there really are bookings in your sync window:
   `Sync:LookBehindDays`/`Sync:LookAheadDays` relative to *today* - a shift plan entirely in
   the past or far future won't be picked up.
2. Confirm `Sync:PublishedBookingStatusNames` matches your environment's actual
   `bookingstatus` names. Query them directly:
   `GET [org]/api/data/v9.2/bookingstatuses?$select=name`. If your organization uses
   different status names (e.g. only `Scheduled`, never `Committed`), update the setting.
3. Check `EventsSkippedUnpublished` and `EventsSkippedNoMailbox` in the `SyncResult` - a
   high count in either tells you exactly which filter is excluding bookings.

## `EventsSkippedNoMailbox` is high

This means bookings resolved to a `bookableresource`, but that resource either isn't of
type "User" (`resourcetype = 3`), has no linked `systemuser`, or that user is disabled.
Verify: `GET [org]/api/data/v9.2/bookableresources?$filter=resourcetype eq 3&$expand=userid($select=internalemailaddress,isdisabled)`.

## `403 Forbidden` from Dataverse

The identity isn't registered as an Application User yet, or its security role doesn't
grant read on one of the six required tables. Re-run
`scripts/setup/3-New-DataverseApplicationUser.ps1` /
`4-New-DataverseSecurityRole.ps1`, or check the role assignment in Power Platform Admin
Center under the environment's Application Users.

## `403 Forbidden` / `ErrorAccessDenied` from Microsoft Graph

- The `Calendars.ReadWrite` application permission hasn't been granted/admin-consented -
  re-run `scripts/setup/2-Grant-GraphCalendarPermission.ps1` as a Global/Privileged Role
  Administrator.
- If you've applied an Exchange Online Application Access Policy
  (`5-New-ApplicationAccessPolicy.ps1`), confirm the target mailbox is actually a member of
  the scoped security group: `Test-ApplicationAccessPolicy -AppId <id> -Identity <mailbox>`.

## Duplicate events appearing for the same shift

This should not happen - every event is tagged with the Dataverse booking ID via an
extended property, and the engine checks for an existing event (via the state cache, then
via Graph directly) before creating a new one. If you do see duplicates:

1. Check whether the sync ran concurrently from two different deployments/identities
   pointed at the same Dataverse environment but different state stores - each maintains
   its own idempotency cache and neither knows about the other's events.
2. Run `POST /api/sync?force=true` after manually deleting the extra event - the
   self-healing lookup will re-associate the remaining one.

## Changed `EventSubjectTemplate`/`EventCategory` but old events still look the old way

Content-hash skipping means unchanged bookings aren't re-pushed to Graph. After changing a
formatting-related setting, run `POST /api/sync?force=true` once to re-push everything.

## I want to reset all state and start over

Deleting the `wfmSyncState` Azure Table (or just its rows) is safe - the next run will
self-heal via the Graph extended-property lookup rather than creating duplicates, at the
cost of one extra Graph call per booking on that run only.

## Verifying what the timer schedule actually is

```powershell
az functionapp config appsettings list -g <rg> -n <function-app> --query "[?name=='SYNC_CRON_SCHEDULE']"
```

NCRONTAB format is `{second} {minute} {hour} {day} {month} {day-of-week}` - note the
leading seconds field, which trips people up coming from standard cron.

## Host reports "Unable to access AzureWebJobsStorage" / timer never fires

If Application Insights shows repeated `The listener for function 'Functions.TimerSyncFunction'
was unable to start` / `AuthorizationFailure` errors, and
`az storage account show --query publicNetworkAccess` returns `Disabled`, your
organization's Azure Policy blocks public network access to Storage accounts entirely
(common in enterprise tenants). A Consumption-plan Function App reaches its storage
account over the public endpoint, so this will always fail regardless of RBAC role
assignments. Options:

- Ask your platform team for a policy exemption on this specific storage account, or
- Move to an Elastic Premium/App Service plan with regional VNet integration plus a
  Private Endpoint (and matching Private DNS Zone) for the storage account's blob, queue,
  and table sub-resources.

This is an environment/policy constraint, not a defect in this tool - `/api/status`
(anonymous, no storage dependency) will still respond normally even while this is broken,
which is a useful first signal to distinguish "app didn't deploy" from "app deployed but
can't reach storage".

## Function key retrieval fails with "InternalServerError from host runtime"

If `az functionapp function keys list` (or the portal's "Get function URL") fails, but the
function otherwise responds to HTTP calls, the Functions host's secret repository (which
normally lives in a blob container named `azure-webjobs-secrets`) may be unable to reach
storage - the same public-network-access restriction above also blocks this. Setting the
app setting `AzureWebJobsSecretStorageType=files` switches key storage to local disk as a
workaround; note this means keys are regenerated if the instance is recycled/scaled out, so
treat it as a diagnostic workaround rather than a production configuration.

