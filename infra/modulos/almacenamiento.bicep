// Storage con el contenedor de las llaves de Data Protection. Sin claves compartidas: solo Microsoft Entra.
param ubicacion string
param etiquetas object
param nombre string
param identidadPrincipalId string
param redundancia string
param bloquearBorrado bool

var rolBlobColaborador = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe' // Storage Blob Data Contributor

resource cuenta 'Microsoft.Storage/storageAccounts@2024-01-01' = {
  name: nombre
  location: ubicacion
  tags: etiquetas
  sku: { name: redundancia }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Hot'
    allowBlobPublicAccess: false
    allowSharedKeyAccess: false
    defaultToOAuthAuthentication: true
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    publicNetworkAccess: 'Enabled'
  }
}

resource blobs 'Microsoft.Storage/storageAccounts/blobServices@2024-01-01' = {
  parent: cuenta
  name: 'default'
  properties: {
    deleteRetentionPolicy: { enabled: true, days: 30 }
    containerDeleteRetentionPolicy: { enabled: true, days: 30 }
    isVersioningEnabled: true
  }
}

resource contenedor 'Microsoft.Storage/storageAccounts/blobServices/containers@2024-01-01' = {
  parent: blobs
  name: 'dataprotection'
  properties: {
    publicAccess: 'None'
  }
}

// Solo sobre el contenedor, no sobre la cuenta.
resource rolContenedor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: contenedor
  name: guid(contenedor.id, identidadPrincipalId, rolBlobColaborador)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', rolBlobColaborador)
    principalId: identidadPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource bloqueo 'Microsoft.Authorization/locks@2020-05-01' = if (bloquearBorrado) {
  scope: cuenta
  name: 'no-borrar'
  properties: {
    level: 'CanNotDelete'
    notes: 'Contiene las llaves de Data Protection: borrarlas invalida sesiones y enlaces de los correos.'
  }
}

output blobLlavesUri string = '${cuenta.properties.primaryEndpoints.blob}${contenedor.name}/llaves.xml'
