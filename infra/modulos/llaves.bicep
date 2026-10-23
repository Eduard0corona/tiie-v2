// Key Vault con RBAC: secretos referenciados desde App Service y la llave que cifra las llaves de Data Protection.
param ubicacion string
param etiquetas object
param nombre string
param identidadPrincipalId string
param logAnalyticsId string
param bloquearBorrado bool

var roles = {
  secretosUsuario: '4633458b-17de-408a-b874-0445c86b69e6' // Key Vault Secrets User
  criptoUsuario: '12338af0-0e69-4776-bea7-57ae8d297424' // Key Vault Crypto User
}

resource keyVault 'Microsoft.KeyVault/vaults@2024-11-01' = {
  name: nombre
  location: ubicacion
  tags: etiquetas
  properties: {
    tenantId: subscription().tenantId
    sku: { family: 'A', name: 'standard' }
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    enablePurgeProtection: true
    publicNetworkAccess: 'Enabled'
  }
}

// Perderla invalida todas las sesiones y los enlaces de los correos ya enviados (docs/07#data-protection).
resource llaveDataProtection 'Microsoft.KeyVault/vaults/keys@2024-11-01' = {
  parent: keyVault
  name: 'dataprotection'
  properties: {
    kty: 'RSA'
    keySize: 2048
    keyOps: ['wrapKey', 'unwrapKey']
  }
}

resource rolSecretos 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: keyVault
  name: guid(keyVault.id, identidadPrincipalId, roles.secretosUsuario)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.secretosUsuario)
    principalId: identidadPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource rolCripto 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: keyVault
  name: guid(keyVault.id, identidadPrincipalId, roles.criptoUsuario)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.criptoUsuario)
    principalId: identidadPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource diagnostico 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  scope: keyVault
  name: 'log-analytics'
  properties: {
    workspaceId: logAnalyticsId
    logs: [{ categoryGroup: 'audit', enabled: true }]
  }
}

resource bloqueo 'Microsoft.Authorization/locks@2020-05-01' = if (bloquearBorrado) {
  scope: keyVault
  name: 'no-borrar'
  properties: {
    level: 'CanNotDelete'
    notes: 'Contiene la llave de Data Protection y los secretos del sitio.'
  }
}

// Sin versión: Data Protection cifra con la versión vigente y guarda cuál usó para descifrar.
output llaveDataProtectionUri string = llaveDataProtection.properties.keyUri
output keyVaultUri string = keyVault.properties.vaultUri
