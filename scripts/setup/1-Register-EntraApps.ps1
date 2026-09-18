#Requires -Modules Microsoft.Graph.Applications, Microsoft.Graph.Authentication
<#
.SYNOPSIS
    OPTIONAL - only needed when you choose AuthMode "ClientSecret" instead of the default,
    recommended, secret-free "ManagedIdentity" mode. Creates one or two app registrations
    (Dataverse and Graph can share the same registration, or use separate ones) and outputs
    the values to feed into `azd env set`.

.DESCRIPTION
    Nothing here is hardcoded - every name and secret lifetime is a parameter. If you are
    using ManagedIdentity mode (the default), you do not need this script at all: the
    Function App's system-assigned identity already IS a service principal, so skip straight
    to 3-New-DataverseApplicationUser.ps1 (with -UseManagedIdentity) and 2-Grant-GraphCalendarPermission.ps1.

.PARAMETER DisplayName
    Display name for the app registration(s) to create.

.PARAMETER SecretExpiryMonths
    How many months until the generated client secret expires. Rotate before then.

.PARAMETER SeparateAppsForDataverseAndGraph
    When set, creates two app registrations instead of one shared registration.

.EXAMPLE
    ./1-Register-EntraApps.ps1 -DisplayName "wfm-outlook-sync"
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$DisplayName,

    [Parameter(Mandatory = $false)]
    [int]$SecretExpiryMonths = 12,

    [switch]$SeparateAppsForDataverseAndGraph
)

$ErrorActionPreference = "Stop"

Connect-MgGraph -Scopes "Application.ReadWrite.All" | Out-Null

function New-AppRegistrationWithSecret {
    param([string]$Name)

    $app = New-MgApplication -DisplayName $Name -SignInAudience "AzureADMyOrg"
    $sp = New-MgServicePrincipal -AppId $app.AppId

    $secret = Add-MgApplicationPassword -ApplicationId $app.Id -PasswordCredential @{
        displayName = "wfm-outlook-sync"
        endDateTime = (Get-Date).AddMonths($SecretExpiryMonths)
    }

    [pscustomobject]@{
        DisplayName        = $Name
        ApplicationId      = $app.AppId
        ObjectId           = $app.Id
        ServicePrincipalId = $sp.Id
        ClientSecret       = $secret.SecretText
    }
}

if ($SeparateAppsForDataverseAndGraph) {
    $dataverseApp = New-AppRegistrationWithSecret -Name "$DisplayName-dataverse"
    $graphApp = New-AppRegistrationWithSecret -Name "$DisplayName-graph"
}
else {
    $shared = New-AppRegistrationWithSecret -Name $DisplayName
    $dataverseApp = $shared
    $graphApp = $shared
}

Write-Host ""
Write-Host "Created app registration(s). Next steps:" -ForegroundColor Green
Write-Host "  1. In the target Dataverse environment, create an Application User for ApplicationId '$($dataverseApp.ApplicationId)'"
Write-Host "     (see 3-New-DataverseApplicationUser.ps1)."
Write-Host "  2. Grant the Graph app registration's service principal Calendars.ReadWrite:"
Write-Host "     ./2-Grant-GraphCalendarPermission.ps1 -PrincipalObjectId $($graphApp.ServicePrincipalId)"
Write-Host "  3. Set these as azd environment variables (or Key Vault secrets referenced by the Function App):"
Write-Host ""
Write-Host "     azd env set DATAVERSE_AUTH_MODE ClientSecret"
Write-Host "     azd env set DATAVERSE_CLIENT_ID $($dataverseApp.ApplicationId)"
Write-Host "     azd env set DATAVERSE_CLIENT_SECRET $($dataverseApp.ClientSecret)"
Write-Host "     azd env set GRAPH_AUTH_MODE ClientSecret"
Write-Host "     azd env set GRAPH_CLIENT_ID $($graphApp.ApplicationId)"
Write-Host "     azd env set GRAPH_CLIENT_SECRET $($graphApp.ClientSecret)"
Write-Host "     azd env set AUTH_TENANT_ID (Get-MgContext).TenantId"
Write-Host ""
Write-Host "Store these secrets in Key Vault for production use - never commit them to source control." -ForegroundColor Yellow
