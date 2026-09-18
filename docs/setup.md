# Setup walkthrough

This walks through a full deployment using the recommended, secret-free
`ManagedIdentity` auth mode. See [`security.md`](security.md) for the `ClientSecret`
alternative.

## Prerequisites

- [Azure Developer CLI (`azd`)](https://aka.ms/azd)
- An Azure subscription you can deploy to
- A Dataverse environment with the WFM (Workforce Engagement Management) feature enabled
  and at least one published shift plan
- Permissions:
  - **Azure**: Contributor (or Owner) on the target subscription/resource group
  - **Microsoft Entra ID**: Global Administrator or Privileged Role Administrator (one-time,
    to grant the Graph application permission)
  - **Dataverse**: System Administrator security role in the target environment

## 1. Deploy the Azure Function

```powershell
git clone https://github.com/moliveirapinto/d365-wfm-outlook-sync.git
cd d365-wfm-outlook-sync

azd auth login
azd env new my-wfm-sync
azd env set DATAVERSE_ENVIRONMENT_URL https://yourorg.crm.dynamics.com

# Optional: tune before first deploy (all have sensible defaults)
azd env set SYNC_CRON_SCHEDULE "0 */5 * * * *"
azd env set SYNC_LOOK_AHEAD_DAYS 30
azd env set SYNC_DRY_RUN true   # recommended for your very first run

azd up
```

`azd up` provisions (see [`infra/main.bicep`](../infra/main.bicep)):

- A Linux Consumption Function App (`.NET 8`, isolated worker) with a **system-assigned
  managed identity**
- A Storage Account, connected to the Function App via an **identity-based connection**
  (no storage connection string secret)
- Application Insights + Log Analytics

Note the `FUNCTION_APP_PRINCIPAL_ID` output - you'll need it in the next two steps:

```powershell
azd env get-values | Select-String FUNCTION_APP_PRINCIPAL_ID
```

## 2. Grant Dataverse access

```powershell
cd scripts/setup

# Create a least-privilege, read-only security role
./4-New-DataverseSecurityRole.ps1 -DataverseEnvironmentUrl https://yourorg.crm.dynamics.com
# note the Role ID it prints

# Create the Application User for the Function App's managed identity, and assign the role
./3-New-DataverseApplicationUser.ps1 `
    -DataverseEnvironmentUrl https://yourorg.crm.dynamics.com `
    -ManagedIdentityPrincipalObjectId <FUNCTION_APP_PRINCIPAL_ID> `
    -SecurityRoleId <roleId-from-previous-step>
```

## 3. Grant Microsoft Graph access

Requires a Global Administrator or Privileged Role Administrator:

```powershell
Install-Module Microsoft.Graph -Scope CurrentUser # if not already installed

./2-Grant-GraphCalendarPermission.ps1 -PrincipalObjectId <FUNCTION_APP_PRINCIPAL_ID>
```

## 4. (Recommended) Scope the Graph permission to WFM agents only

```powershell
Install-Module ExchangeOnlineManagement -Scope CurrentUser # if not already installed

./5-New-ApplicationAccessPolicy.ps1 `
    -ApplicationId <the AppId Graph resolved for your managed identity - printed by step 2/3> `
    -MailEnabledSecurityGroup wfm-agents@yourorg.com
```

## 5. Verify

```powershell
# Health check - no auth required
curl https://<function-app>.azurewebsites.net/api/status

# Get a function key from the portal or:
az functionapp keys list -g <resource-group> -n <function-app>

# Run a sync (still in dry-run if you set that above - check the logs, nothing writes yet)
curl -X POST "https://<function-app>.azurewebsites.net/api/sync?code=<function-key>"
```

Check the response body (a `SyncResult`) and Application Insights logs for
`EventsCreated`/`EventsUpdated`/`EventsSkipped*`/`Failures`. Once you're happy with a dry
run:

```powershell
azd env set SYNC_DRY_RUN false
azd deploy
```

From here, the timer trigger runs automatically on `SYNC_CRON_SCHEDULE` - nothing else to
do. Adjust any setting in [`configuration.md`](configuration.md) at any time via
`azd env set <NAME> <value>` followed by `azd deploy` (or directly in the Azure Portal's
Configuration blade for a faster iteration loop).

## Running locally

```powershell
cd src/WfmOutlookSync
Copy-Item local.settings.json.sample local.settings.json
# edit local.settings.json with your values (ClientSecret mode is easiest for local dev)
func start
```
