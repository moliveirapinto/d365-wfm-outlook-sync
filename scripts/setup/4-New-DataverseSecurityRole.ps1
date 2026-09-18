#Requires -Modules Az.Accounts
<#
.SYNOPSIS
    Creates a least-privilege, read-only Dataverse security role scoped to exactly the
    tables this tool needs, so the Application User it's assigned to (see
    3-New-DataverseApplicationUser.ps1) never has more access than required.

.DESCRIPTION
    Uses the Dataverse Web API "AddPrivileges" action directly - no table/environment
    name is hardcoded, everything is a parameter. Requires the caller to be signed in
    (`az login`) as a Dataverse System Administrator / System Customizer in the target
    environment.

    If your organization's change-management process requires roles to be created via a
    solution instead of live in an environment, use this script's output as a checklist
    and create the equivalent role through Power Platform Admin Center > your environment
    > Security roles > New role instead.

.PARAMETER DataverseEnvironmentUrl
    e.g. https://yourorg.crm.dynamics.com

.PARAMETER RoleName
    Name for the new security role. Defaults to "WFM Outlook Sync (Read-only)".

.EXAMPLE
    ./4-New-DataverseSecurityRole.ps1 -DataverseEnvironmentUrl https://yourorg.crm.dynamics.com
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$DataverseEnvironmentUrl,

    [Parameter(Mandatory = $false)]
    [string]$RoleName = "WFM Outlook Sync (Read-only)"
)

$ErrorActionPreference = "Stop"

# The only tables this tool ever reads. Nothing else is granted.
$tablesNeeded = @(
    "bookableresourcebooking",
    "bookableresource",
    "systemuser",
    "msdyn_shiftplan",
    "msdyn_shiftactivitytype",
    "bookingstatus"
)

$token = (az account get-access-token --resource $DataverseEnvironmentUrl --query accessToken -o tsv)
if (-not $token) {
    throw "Could not acquire a token for '$DataverseEnvironmentUrl'. Run 'az login' first."
}

$headers = @{
    Authorization    = "Bearer $token"
    Accept           = "application/json"
    "OData-MaxVersion" = "4.0"
    "OData-Version"    = "4.0"
    "Content-Type"     = "application/json"
}
$base = "$($DataverseEnvironmentUrl.TrimEnd('/'))/api/data/v9.2"

$rootBu = Invoke-RestMethod -Uri "$base/businessunits?`$filter=_parentbusinessunitid_value eq null&`$select=businessunitid" -Headers $headers
$rootBuId = $rootBu.value[0].businessunitid

$roleBody = @{
    name                          = $RoleName
    "businessunitid@odata.bind"   = "/businessunits($rootBuId)"
} | ConvertTo-Json

$roleResponse = Invoke-WebRequest -Uri "$base/roles" -Headers $headers -Method Post -Body $roleBody
$roleId = [regex]::Match($roleResponse.Headers.'OData-EntityId', '\(([0-9a-fA-F-]+)\)').Groups[1].Value

Write-Host "Created role '$RoleName' ($roleId)." -ForegroundColor Green

$privileges = @()
foreach ($table in $tablesNeeded) {
    $privName = "prvRead$table"
    $priv = Invoke-RestMethod -Uri "$base/privileges?`$filter=name eq '$privName'&`$select=privilegeid,name" -Headers $headers
    if ($priv.value.Count -eq 0) {
        Write-Warning "Could not find privilege '$privName' - skipping. You may need to grant read access to '$table' manually."
        continue
    }

    $privileges += @{ PrivilegeId = $priv.value[0].privilegeid; Depth = 4 } # 4 = Global/Organization
}

$addPrivilegesBody = @{ Privileges = $privileges } | ConvertTo-Json -Depth 5

Invoke-RestMethod -Uri "$base/roles($roleId)/Microsoft.Dynamics.CRM.AddPrivileges" -Headers $headers -Method Post -Body $addPrivilegesBody | Out-Null

Write-Host "Granted organization-wide read privileges on: $($tablesNeeded -join ', ')" -ForegroundColor Green
Write-Host ""
Write-Host "Role ID (pass this to 3-New-DataverseApplicationUser.ps1 -SecurityRoleId): $roleId" -ForegroundColor Cyan
