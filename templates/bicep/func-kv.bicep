param location string = resourceGroup().location
param funcName_g string

resource devOpsKeyVault 'Microsoft.KeyVault/vaults@2022-07-01' existing = {
  name: 'kv-softimply-onprem'
}

module func 'func.bicep' = {
  name: 'func-deployment'
  params: {
    partitionKey: devOpsKeyVault.getSecret('PartitionKey')
    recieverEmail: devOpsKeyVault.getSecret('ToEmail')
    senderEmail: devOpsKeyVault.getSecret('FromEmail')
    sendGridKey: devOpsKeyVault.getSecret('SendGridApiKey')
    serverPingUrl: devOpsKeyVault.getSecret('PingUrl')
    insightsConnectionString:applicationInsights.properties.ConnectionString
    insightsInstrumentationKey:applicationInsights.properties.InstrumentationKey
    location: location
    funcName: funcName_g
  }
}

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: 'insights-func-ping'
  location: location
  kind: 'web'  
  properties: {
    Application_Type: 'web'
    IngestionMode: 'ApplicationInsights'
    Request_Source: 'rest'
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
    RetentionInDays: 30
  }
}
