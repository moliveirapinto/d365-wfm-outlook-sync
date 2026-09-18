targetScope = 'subscription'

@description('Name of the resource group to create/use for this deployment.')
param resourceGroupName string

@description('Azure region for all resources.')
param location string = deployment().location

@description('Short, unique token used to build globally-unique resource names (e.g. storage account, key vault). Provide your own or let azd generate one.')
@minLength(3)
@maxLength(10)
param resourceToken string

@description('The Dataverse environment URL that hosts the WFM shift data, e.g. https://yourorg.crm.dynamics.com')
param dataverseEnvironmentUrl string

@description('Auth mode used to call Dataverse: ManagedIdentity (recommended, no secrets) or ClientSecret.')
@allowed([
  'ManagedIdentity'
  'ClientSecret'
])
param dataverseAuthMode string = 'ManagedIdentity'

@description('Auth mode used to call Microsoft Graph: ManagedIdentity (recommended, no secrets) or ClientSecret.')
@allowed([
  'ManagedIdentity'
  'ClientSecret'
])
param graphAuthMode string = 'ManagedIdentity'

@description('Tenant ID for ClientSecret auth modes. Leave empty when using ManagedIdentity.')
param tenantId string = ''

@description('Client (application) ID used for Dataverse ClientSecret auth mode. Leave empty when using ManagedIdentity.')
param dataverseClientId string = ''

@description('Client secret used for Dataverse ClientSecret auth mode. Leave empty when using ManagedIdentity.')
@secure()
param dataverseClientSecret string = ''

@description('Client (application) ID used for Graph ClientSecret auth mode. Leave empty when using ManagedIdentity.')
param graphClientId string = ''

@description('Client secret used for Graph ClientSecret auth mode. Leave empty when using ManagedIdentity.')
@secure()
param graphClientSecret string = ''

@description('NCRONTAB expression controlling how often the sync runs, e.g. "0 */5 * * * *" for every 5 minutes.')
param syncCronSchedule string = '0 */5 * * * *'

@description('How many days in the past to include when scanning for shifts.')
param lookBehindDays int = 1

@description('How many days in the future to include when scanning for shifts.')
param lookAheadDays int = 30

@description('Comma-separated list of msdyn_shiftactivitytype names to exclude from sync.')
param excludedShiftActivityTypeNames string = ''

@description('Comma-separated list of bookingstatus names considered "published"/final.')
param publishedBookingStatusNames string = 'Committed,Scheduled'

@description('Outlook event subject template. Supported tokens: {ShiftActivityType} {ShiftPlanName} {ResourceName}')
param eventSubjectTemplate string = '{ShiftActivityType} - {ShiftPlanName}'

@description('Outlook category label applied to every synced event.')
param eventCategory string = 'WFM Shift'

@description('When true, computes what would happen but performs no writes to Outlook.')
param dryRun bool = false

var tags = {
  'azd-env-name': resourceToken
  solution: 'd365-wfm-outlook-sync'
}

resource rg 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: resourceGroupName
  location: location
  tags: tags
}

module resources 'resources.bicep' = {
  name: 'resources'
  scope: rg
  params: {
    location: location
    resourceToken: resourceToken
    tags: tags
    dataverseEnvironmentUrl: dataverseEnvironmentUrl
    dataverseAuthMode: dataverseAuthMode
    graphAuthMode: graphAuthMode
    tenantId: tenantId
    dataverseClientId: dataverseClientId
    dataverseClientSecret: dataverseClientSecret
    graphClientId: graphClientId
    graphClientSecret: graphClientSecret
    syncCronSchedule: syncCronSchedule
    lookBehindDays: lookBehindDays
    lookAheadDays: lookAheadDays
    excludedShiftActivityTypeNames: excludedShiftActivityTypeNames
    publishedBookingStatusNames: publishedBookingStatusNames
    eventSubjectTemplate: eventSubjectTemplate
    eventCategory: eventCategory
    dryRun: dryRun
  }
}

output FUNCTION_APP_NAME string = resources.outputs.functionAppName
output FUNCTION_APP_HOSTNAME string = resources.outputs.functionAppHostName
output FUNCTION_APP_PRINCIPAL_ID string = resources.outputs.functionAppPrincipalId
output STORAGE_ACCOUNT_NAME string = resources.outputs.storageAccountName
output APPLICATION_INSIGHTS_NAME string = resources.outputs.appInsightsName
output RESOURCE_GROUP_NAME string = rg.name
