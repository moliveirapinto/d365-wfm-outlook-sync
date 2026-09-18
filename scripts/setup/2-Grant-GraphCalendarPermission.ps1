#Requires -Modules Microsoft.Graph.Applications, Microsoft.Graph.Authentication
<#
.SYNOPSIS
    Grants the Microsoft Graph application permission "Calendars.ReadWrite" to the
    service principal that will run the sync (a Function App's managed identity, or a
    dedicated app registration).

.DESCRIPTION
    Nothing about the target principal or tenant is hardcoded - pass whichever
    -PrincipalObjectId your deployment uses. Must be run by a Global Administrator or
    Privileged Role Administrator, because granting an application permission is an
    admin-consent operation that cannot be delegated to the app being granted the
    permission.

.PARAMETER PrincipalObjectId
    The Entra ID object ID of the service principal to grant the permission to. For a
    Function App using a system-assigned managed identity, this is the identity's
    principal ID (see the "FUNCTION_APP_PRINCIPAL_ID" azd/deployment output).

.PARAMETER AppRole
    The Graph application permission to grant. Defaults to Calendars.ReadWrite, the
    minimum permission this tool needs. Only widen this if your organization requires it.

.EXAMPLE
    ./2-Grant-GraphCalendarPermission.ps1 -PrincipalObjectId $env:FUNCTION_APP_PRINCIPAL_ID
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$PrincipalObjectId,

    [Parameter(Mandatory = $false)]
    [string]$AppRole = "Calendars.ReadWrite"
)

$ErrorActionPreference = "Stop"

Connect-MgGraph -Scopes "Application.Read.All", "AppRoleAssignment.ReadWrite.All" | Out-Null

$graphServicePrincipal = Get-MgServicePrincipal -Filter "appId eq '00000003-0000-0000-c000-000000000000'"
if (-not $graphServicePrincipal) {
    throw "Could not find the Microsoft Graph service principal in this tenant."
}

$role = $graphServicePrincipal.AppRoles | Where-Object { $_.Value -eq $AppRole -and $_.AllowedMemberTypes -contains "Application" }
if (-not $role) {
    throw "App role '$AppRole' was not found on the Microsoft Graph service principal."
}

$existing = Get-MgServicePrincipalAppRoleAssignment -ServicePrincipalId $PrincipalObjectId -All |
    Where-Object { $_.AppRoleId -eq $role.Id -and $_.ResourceId -eq $graphServicePrincipal.Id }

if ($existing) {
    Write-Host "Principal '$PrincipalObjectId' already has '$AppRole'. Nothing to do." -ForegroundColor Yellow
    return
}

New-MgServicePrincipalAppRoleAssignment `
    -ServicePrincipalId $PrincipalObjectId `
    -PrincipalId $PrincipalObjectId `
    -ResourceId $graphServicePrincipal.Id `
    -AppRoleId $role.Id | Out-Null

Write-Host "Granted Graph application permission '$AppRole' to principal '$PrincipalObjectId'." -ForegroundColor Green
Write-Host "Tip: run scripts/setup/5-New-ApplicationAccessPolicy.ps1 afterwards to scope this permission to only your WFM agents' mailboxes." -ForegroundColor Cyan
