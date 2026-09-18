# Setup scripts

Run once per environment, in this order. Every script is fully parameterized - nothing
about your tenant, environment, or app names is hardcoded.

| # | Script | Purpose | When needed |
|---|--------|---------|--------------|
| 1 | `1-Register-EntraApps.ps1` | Creates app registration(s) with a client secret | Only if using `AuthMode=ClientSecret`. Skip for the default, recommended `ManagedIdentity` mode. |
| 2 | `2-Grant-GraphCalendarPermission.ps1` | Grants the Graph `Calendars.ReadWrite` application permission | Always. Run by a Global/Privileged Role Administrator. |
| 3 | `3-New-DataverseApplicationUser.ps1` | Creates the non-interactive Dataverse Application User | Always. Run by a Dataverse System Administrator. |
| 4 | `4-New-DataverseSecurityRole.ps1` | Creates a least-privilege, read-only security role | Recommended - run before step 3 and pass its output as `-SecurityRoleId`. |
| 5 | `5-New-ApplicationAccessPolicy.ps1` | Scopes the Graph permission to only WFM agent mailboxes | Recommended hardening, not required to function. |

See [`docs/setup.md`](../../docs/setup.md) for the full walkthrough, including which
identifiers to pass between scripts.
