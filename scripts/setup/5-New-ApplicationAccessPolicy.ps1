#Requires -Modules ExchangeOnlineManagement
<#
.SYNOPSIS
    OPTIONAL, RECOMMENDED HARDENING: scopes the Graph "Calendars.ReadWrite" application
    permission so it can only touch mailboxes belonging to WFM agents, instead of every
    mailbox in the tenant. Defense-in-depth: even if the Function App's identity were ever
    compromised, it could still only read/write calendars in this one group.

.DESCRIPTION
    Creates (or reuses) a mail-enabled security group and an Exchange Online Application
    Access Policy scoping the given app ID to only that group. Nothing is hardcoded - you
    choose the group and app.

.PARAMETER ApplicationId
    The Application (client) ID used for Graph auth (the app registration's AppId, or the
    managed identity's AppId as resolved in 3-New-DataverseApplicationUser.ps1).

.PARAMETER MailEnabledSecurityGroup
    Email address of a mail-enabled security group containing every WFM agent mailbox.
    Create/maintain this group's membership however your organization already manages
    distribution lists (e.g. sync it from the same Dataverse "User" bookable resources).

.EXAMPLE
    ./5-New-ApplicationAccessPolicy.ps1 -ApplicationId <graph-app-id> -MailEnabledSecurityGroup wfm-agents@yourorg.com
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$ApplicationId,

    [Parameter(Mandatory = $true)]
    [string]$MailEnabledSecurityGroup
)

$ErrorActionPreference = "Stop"

Connect-ExchangeOnline

New-ApplicationAccessPolicy `
    -AppId $ApplicationId `
    -PolicyScopeGroupId $MailEnabledSecurityGroup `
    -AccessRight RestrictAccess `
    -Description "d365-wfm-outlook-sync: restrict Calendars.ReadWrite to WFM agent mailboxes only"

Write-Host "Application access policy created. Verify with:" -ForegroundColor Green
Write-Host "  Test-ApplicationAccessPolicy -AppId $ApplicationId -Identity <someone-in-the-group@yourorg.com>"
Write-Host "  Test-ApplicationAccessPolicy -AppId $ApplicationId -Identity <someone-NOT-in-the-group@yourorg.com>"
