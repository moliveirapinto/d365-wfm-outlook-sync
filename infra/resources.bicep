@description('Azure region for all resources.')
param location string

@description('Short, unique token used to build globally-unique resource names.')
param resourceToken string

param tags object

param dataverseEnvironmentUrl string
param dataverseAuthMode string
param graphAuthMode string
param tenantId string
param dataverseClientId string
@secure()
param dataverseClientSecret string
param graphClientId string
@secure()
param graphClientSecret string
param syncCronSchedule string
param lookBehindDays int
param lookAheadDays int
param excludedShiftActivityTypeNames string
param publishedBookingStatusNames string
param eventSubjectTemplate string
param eventCategory string
param dryRun bool

var storageAccountName = 'st${resourceToken}'
var planName = 'plan-${resourceToken}'
var functionAppName = 'func-${resourceToken}'
var appInsightsName = 'appi-${resourceToken}'
var logAnalyticsName = 'log-${resourceToken}'
var syncStateTableName = 'wfmSyncState'

// Built-in role IDs required for identity-based (secret-free) AzureWebJobsStorage connections.
var storageBlobDataOwnerRoleId = 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b'
var storageQueueDataContributorRoleId = '974c5e8b-45b9-4653-ba55-5f855dd0fb88'
var storageTableDataContributorRoleId = '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3'

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  tags: tags
  sku: { name: 'Standard_LRS' }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    allowSharedKeyAccess: true
    networkAcls: {
      defaultAction: 'Allow'
    }
  }
}

resource tableService 'Microsoft.Storage/storageAccounts/tableServices@2023-05-01' = {
  parent: storage
  name: 'default'
}

resource syncStateTable 'Microsoft.Storage/storageAccounts/tableServices/tables@2023-05-01' = {
  parent: tableService
  name: syncStateTableName
}

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsName
  location: location
  tags: tags
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
    IngestionMode: 'LogAnalytics'
  }
}

resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: planName
  location: location
  tags: tags
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
  }
  kind: 'functionapp,linux'
  properties: {
    reserved: true
  }
}

resource functionApp 'Microsoft.Web/sites@2023-12-01' = {
  name: functionAppName
  location: location
  tags: union(tags, { 'azd-service-name': 'sync' })
  kind: 'functionapp,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNET-ISOLATED|8.0'
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      appSettings: [
        { name: 'FUNCTIONS_EXTENSION_VERSION', value: '~4' }
        { name: 'FUNCTIONS_WORKER_RUNTIME', value: 'dotnet-isolated' }
        { name: 'AzureWebJobsStorage__accountName', value: storage.name }
        { name: 'AzureWebJobsStorage__credential', value: 'managedidentity' }
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsights.properties.ConnectionString }

        { name: 'SYNC_CRON_SCHEDULE', value: syncCronSchedule }

        { name: 'Dataverse__EnvironmentUrl', value: dataverseEnvironmentUrl }
        { name: 'Dataverse__AuthMode', value: dataverseAuthMode }
        { name: 'Dataverse__TenantId', value: tenantId }
        { name: 'Dataverse__ClientId', value: dataverseClientId }
        { name: 'Dataverse__ClientSecret', value: dataverseClientSecret }

        { name: 'Graph__AuthMode', value: graphAuthMode }
        { name: 'Graph__TenantId', value: tenantId }
        { name: 'Graph__ClientId', value: graphClientId }
        { name: 'Graph__ClientSecret', value: graphClientSecret }

        { name: 'Sync__LookBehindDays', value: string(lookBehindDays) }
        { name: 'Sync__LookAheadDays', value: string(lookAheadDays) }
        { name: 'Sync__ExcludedShiftActivityTypeNames', value: excludedShiftActivityTypeNames }
        { name: 'Sync__PublishedBookingStatusNames', value: publishedBookingStatusNames }
        { name: 'Sync__EventSubjectTemplate', value: eventSubjectTemplate }
        { name: 'Sync__EventCategory', value: eventCategory }
        { name: 'Sync__DryRun', value: string(dryRun) }

        { name: 'SyncState__TableName', value: syncStateTableName }
      ]
    }
  }
}

resource blobRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storage.id, functionApp.id, storageBlobDataOwnerRoleId)
  scope: storage
  properties: {
    principalId: functionApp.identity.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataOwnerRoleId)
  }
}

resource queueRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storage.id, functionApp.id, storageQueueDataContributorRoleId)
  scope: storage
  properties: {
    principalId: functionApp.identity.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageQueueDataContributorRoleId)
  }
}

resource tableRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storage.id, functionApp.id, storageTableDataContributorRoleId)
  scope: storage
  properties: {
    principalId: functionApp.identity.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageTableDataContributorRoleId)
  }
}

output functionAppName string = functionApp.name
output functionAppHostName string = functionApp.properties.defaultHostName
output functionAppPrincipalId string = functionApp.identity.principalId
output storageAccountName string = storage.name
output appInsightsName string = appInsights.name
