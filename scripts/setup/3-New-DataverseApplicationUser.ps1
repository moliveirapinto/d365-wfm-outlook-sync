#Requires -Modules Az.Accounts, Microsoft.Graph.Applications
<#
.SYNOPSIS
    Creates a non-interactive Dataverse Application User for the identity that will run the
    sync (a Function App's managed identity, or a dedicated app registration) and assigns it
    a security role.

.DESCRIPTION
    Nothing about the environment, tenant, or identity is hardcoded - all parameters. Must be
    run by a Dataverse System Administrator (`az login` first).

.PARAMETER DataverseEnvironmentUrl
    e.g. https://yourorg.crm.dynamics.com

.PARAMETER ManagedIdentityPrincipalObjectId
    The Function App's system-assigned managed identity object ID (from the
    FUNCTION_APP_PRINCIPAL_ID deployment output). Use this OR -ApplicationId, not both.

.PARAMETER ApplicationId
    The Application (client) ID of a dedicated app registration, when using ClientSecret
    auth mode instead of a managed identity. Use this OR -ManagedIdentityPrincipalObjectId.

.PARAMETER SecurityRoleId
    The GUID of the security role to assign (see 4-New-DataverseSecurityRole.ps1). If
    omitted, only the application user is created and you must assign a role manually.

.EXAMPLE
    ./3-New-DataverseApplicationUser.ps1 -DataverseEnvironmentUrl https://yourorg.crm.dynamics.com `
        -ManagedIdentityPrincipalObjectId $env:FUNCTION_APP_PRINCIPAL_ID `
        -SecurityRoleId $roleId
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$DataverseEnvironmentUrl,

    [Parameter(Mandatory = $false)]
    [string]$ManagedIdentityPrincipalObjectId,

    [Parameter(Mandatory = $false)]
    [string]$ApplicationId,

    [Parameter(Mandatory = $false)]
    [string]$SecurityRoleId
)

$ErrorActionPreference = "Stop"

if (-not $ManagedIdentityPrincipalObjectId -and -not $ApplicationId) {
    throw "Provide either -ManagedIdentityPrincipalObjectId or -ApplicationId."
}

if ($ManagedIdentityPrincipalObjectId) {
    Connect-MgGraph -Scopes "Application.Read.All" | Out-Null
    $sp = Get-MgServicePrincipal -ServicePrincipalId $ManagedIdentityPrincipalObjectId
    $ApplicationId = $sp.AppId
    Write-Host "Resolved managed identity object '$ManagedIdentityPrincipalObjectId' to Application ID '$ApplicationId'." -ForegroundColor Cyan
}

$token = (az account get-access-token --resource $DataverseEnvironmentUrl --query accessToken -o tsv)
if (-not $token) {
    throw "Could not acquire a token for '$DataverseEnvironmentUrl'. Run 'az login' first."
}

$headers = @{
    Authorization      = "Bearer $token"
    Accept             = "application/json"
    "OData-MaxVersion" = "4.0"
    "OData-Version"    = "4.0"
    "Content-Type"     = "application/json"
}
$base = "$($DataverseEnvironmentUrl.TrimEnd('/'))/api/data/v9.2"

$existing = Invoke-RestMethod -Uri "$base/systemusers?`$filter=applicationid eq $ApplicationId&`$select=systemuserid" -Headers $headers
if ($existing.value.Count -gt 0) {
    $userId = $existing.value[0].systemuserid
    Write-Host "Application user already exists for '$ApplicationId' ($userId)." -ForegroundColor Yellow
}
else {
    $rootBu = Invoke-RestMethod -Uri "$base/businessunits?`$filter=_parentbusinessunitid_value eq null&`$select=businessunitid" -Headers $headers
    $rootBuId = $rootBu.value[0].businessunitid

    $body = @{
        applicationid               = $ApplicationId
        "businessunitid@odata.bind" = "/businessunits($rootBuId)"
    } | ConvertTo-Json

    $response = Invoke-WebRequest -Uri "$base/systemusers" -Headers $headers -Method Post -Body $body
    $userId = [regex]::Match($response.Headers.'OData-EntityId', '\(([0-9a-fA-F-]+)\)').Groups[1].Value

    Write-Host "Created Dataverse application user '$userId' for Application ID '$ApplicationId'." -ForegroundColor Green
}

if ($SecurityRoleId) {
    $assignBody = @{ "@odata.id" = "$base/roles($SecurityRoleId)" } | ConvertTo-Json
    Invoke-RestMethod -Uri "$base/systemusers($userId)/systemuserroles_association/`$ref" -Headers $headers -Method Post -Body $assignBody | Out-Null
    Write-Host "Assigned security role '$SecurityRoleId' to application user '$userId'." -ForegroundColor Green
}
else {
    Write-Host "No -SecurityRoleId supplied - assign a role to user '$userId' before the sync will be able to read any data." -ForegroundColor Yellow
}
