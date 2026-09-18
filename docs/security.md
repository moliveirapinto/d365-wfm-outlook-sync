# Security

## Auth modes

Both the Dataverse and Graph connections support two independent auth modes:

### `ManagedIdentity` (default, recommended)

The Function App's system-assigned managed identity is used directly - there is **no
client secret anywhere**: not in app settings, not in Key Vault, not in source control.
The identity is granted access explicitly and narrowly:

- **Dataverse**: registered as a non-interactive Application User, assigned a **custom,
  read-only security role** scoped to exactly six tables (see
  [`data-model.md`](data-model.md) and `scripts/setup/4-New-DataverseSecurityRole.ps1`).
  It cannot write to Dataverse, and cannot read any table outside that role.
- **Microsoft Graph**: granted the `Calendars.ReadWrite` **application permission** via an
  app role assignment on the identity's service principal
  (`scripts/setup/2-Grant-GraphCalendarPermission.ps1`). This is intentionally the
  *narrowest* Graph permission that can create/update/delete events in another user's
  calendar - there is no per-user delegated alternative for an unattended background job
  touching many mailboxes.

### `ClientSecret` (opt-in, for constrained environments)

Some organizations require dedicated app registrations for change-management/audit
reasons. `scripts/setup/1-Register-EntraApps.ps1` creates them; store the resulting secret
in Key Vault and reference it from the Function App setting
(`@Microsoft.KeyVault(SecretUri=...)`) rather than as plain text - never commit a secret to
source control or a `local.settings.json` that gets checked in (the `.gitignore` in this
repo already excludes `local.settings.json`).

## Defense in depth: scoping `Calendars.ReadWrite`

`Calendars.ReadWrite` as an application permission is tenant-wide by default - the
identity *could* read/write any mailbox, not just WFM agents'. Two independent mitigations
are provided; use at least the first:

1. **Exchange Online Application Access Policy**
   (`scripts/setup/5-New-ApplicationAccessPolicy.ps1`) restricts the permission to only a
   mail-enabled security group you control (e.g. synced from the same `bookableresource`
   roster this tool already reads). Calls against any mailbox outside that group are
   rejected by Exchange Online itself, independent of anything this code does.
2. **Application code never accepts an arbitrary mailbox as input** - the only mailboxes
   ever touched are the ones resolved from Dataverse's own `bookableresource` &rarr;
   `systemuser` roster (see `Dataverse/DataverseShiftRepository.GetAgentResourcesAsync`),
   which itself only includes active, non-disabled users. There is no HTTP endpoint that
   takes a mailbox parameter.

## Least privilege on the Dataverse side

The custom security role created by `4-New-DataverseSecurityRole.ps1` grants **read-only,
organization-scoped** access to exactly:

- `bookableresourcebooking`, `bookableresource`, `systemuser`, `msdyn_shiftplan`,
  `msdyn_shiftactivitytype`, `bookingstatus`

No write privilege is ever requested on any table - Dataverse remains fully read-only from
this tool's perspective. If your change-management process requires creating the role
through a solution instead of live in the environment, use the script's output privilege
list as your spec.

## Data handled

The tool moves shift **metadata** (start/end time, shift plan name, activity type name,
status) into an Outlook event's subject/body/category. It does not read or write case data,
customer PII, call recordings, or anything outside the WFM shift tables listed above.

## Secrets hygiene checklist

- [ ] Default to `ManagedIdentity` for both `Dataverse:AuthMode` and `Graph:AuthMode`.
- [ ] If `ClientSecret` mode is unavoidable, store the secret in **Key Vault**, reference it
      via a Key Vault reference app setting, and set an expiry + rotation reminder.
- [ ] Never commit `local.settings.json` (already gitignored) or paste secrets into issues,
      PRs, or logs.
- [ ] Apply the Exchange Online Application Access Policy in any production tenant.
- [ ] Review the Dataverse security role periodically - it should never need write access.

## Reporting a vulnerability

Please open a private security advisory on the repository rather than a public issue.
