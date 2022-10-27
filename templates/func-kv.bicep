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
  }
}
