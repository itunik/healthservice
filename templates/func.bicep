
param location string = resourceGroup().location
@secure()
param serverPingUrl string
@secure()
param sendGridKey string
@secure()
param senderEmail string
@secure()
param recieverEmail string
@secure()
param partitionKey string
@secure()
param insightsConnectionString string
@secure()
param insightsInstrumentationKey string

resource storageAccount 'Microsoft.Storage/storageAccounts@2021-02-01' = {
  name: 'sapingfunction'
  location: location
  kind:'StorageV2'
  sku:{
    name:'Standard_LRS'
  }    
}

resource table 'Microsoft.Storage/storageAccounts/tableServices/tables@2022-05-01' = {
  name: '${storageAccount.name}/default/StatusData'
}

resource servicePlan 'Microsoft.Web/serverfarms@2020-12-01' = {
  name:'serviceplan'
  location:location
  sku:{
    name:'Y1'
    tier:'Dynamic'
  }
}

resource funcServerPing 'Microsoft.Web/sites@2022-03-01' = {
  name:'func-softimply-serverping'
  location: location
  kind:'functionapp'  
  properties:{
    serverFarmId: servicePlan.id
    clientAffinityEnabled: true
    siteConfig:{
      appSettings:[
        {
          name: 'AzureWebJobsStorage'
          value:'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${listKeys(storageAccount.id, storageAccount.apiVersion).keys[0].value};EndpointSuffix=${environment().suffixes.storage}'          
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet'
        }
        {
          name: 'PingUrl'
          value: serverPingUrl
        }
        { 
          name: 'SendGridApiKey'
          value: sendGridKey
        }
        {
          name: 'FromEmail'
          value: senderEmail
        }
        {
          name: 'ToEmail'
          value: recieverEmail
        }
        {
          name: 'PartitionKey'
          value: partitionKey
        }
        {
          name: 'TimeZoneId'
          value: 'FLE Standard Time'
        }
        {
          name: 'APPINSIGHTS_CONNECTIONSTRING'
          value: insightsConnectionString
        }
        {
          name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
          value: insightsInstrumentationKey
        }
        {
          name: 'ShouldReadTimezones'
          value: 'false'
        }
        {
          name: 'TableName'
          value: 'StatusData'
        }
      ]
    }
  }
}
