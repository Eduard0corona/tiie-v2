// Plan y sitio de App Service (Linux, .NET 10) y, en producción, la ranura staging para intercambiar.
param ubicacion string
param etiquetas object
param nombrePlan string
param nombreSitio string
param skuPlan string
param instancias int
param conRanura bool
param identidadId string
param identidadClientId string
param logAnalyticsId string
@description('Configuración común al sitio y a la ranura.')
param configuracion object
@description('Configuración que solo existe en QA (semilla y lista de destinatarios).')
param configuracionQa object

var comun = union(configuracion, configuracionQa, {
  // DefaultAzureCredential y las referencias a Key Vault usan la identidad asignada por el usuario.
  AZURE_CLIENT_ID: identidadClientId
  // El intercambio espera a que /salud responda 200 en cada instancia de la ranura.
  WEBSITE_SWAP_WARMUP_PING_PATH: '/salud'
  WEBSITE_SWAP_WARMUP_PING_STATUSES: '200'
})

// Procesos:Habilitados es de ranura: staging no envía correos ni corre procesos con el código nuevo
// mientras se prueba; al intercambiar, la configuración se queda en su ranura.
var configuracionSitio = union(comun, { Procesos__Habilitados: 'true' })
var configuracionRanura = union(comun, { Procesos__Habilitados: 'false' })

var sitioBase = {
  linuxFxVersion: 'DOTNETCORE|10.0'
  appCommandLine: 'dotnet Tiie.Web.dll'
  alwaysOn: true
  http20Enabled: true
  minTlsVersion: '1.2'
  scmMinTlsVersion: '1.2'
  ftpsState: 'Disabled'
  healthCheckPath: '/salud'
  use32BitWorkerProcess: false
}

resource plan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: nombrePlan
  location: ubicacion
  tags: etiquetas
  kind: 'linux'
  sku: {
    name: skuPlan
    capacity: instancias
  }
  properties: {
    reserved: true
  }
}

resource sitio 'Microsoft.Web/sites@2024-04-01' = {
  name: nombreSitio
  location: ubicacion
  tags: etiquetas
  kind: 'app,linux'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: { '${identidadId}': {} }
  }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    clientAffinityEnabled: false
    keyVaultReferenceIdentity: identidadId
    siteConfig: union(sitioBase, {
      appSettings: map(items(configuracionSitio), c => { name: c.key, value: c.value })
    })
  }
}

resource configuracionDeRanura 'Microsoft.Web/sites/config@2024-04-01' = {
  parent: sitio
  name: 'slotConfigNames'
  properties: {
    appSettingNames: ['Procesos__Habilitados']
  }
}

// Despliegue solo con Microsoft Entra (OIDC del pipeline): sin usuario y contraseña de publicación.
resource sinFtp 'Microsoft.Web/sites/basicPublishingCredentialsPolicies@2024-04-01' = {
  parent: sitio
  name: 'ftp'
  properties: { allow: false }
}

resource sinScmBasico 'Microsoft.Web/sites/basicPublishingCredentialsPolicies@2024-04-01' = {
  parent: sitio
  name: 'scm'
  properties: { allow: false }
}

resource ranura 'Microsoft.Web/sites/slots@2024-04-01' = if (conRanura) {
  parent: sitio
  name: 'staging'
  location: ubicacion
  tags: etiquetas
  kind: 'app,linux'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: { '${identidadId}': {} }
  }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    clientAffinityEnabled: false
    keyVaultReferenceIdentity: identidadId
    siteConfig: union(sitioBase, {
      appSettings: map(items(configuracionRanura), c => { name: c.key, value: c.value })
    })
  }
}

resource ranuraSinFtp 'Microsoft.Web/sites/slots/basicPublishingCredentialsPolicies@2024-04-01' = if (conRanura) {
  parent: ranura
  name: 'ftp'
  properties: { allow: false }
}

resource ranuraSinScmBasico 'Microsoft.Web/sites/slots/basicPublishingCredentialsPolicies@2024-04-01' = if (conRanura) {
  parent: ranura
  name: 'scm'
  properties: { allow: false }
}

// Sin AppServiceHTTPLogs: registran la URL completa y /mi-cita/{token} lleva el token de la cita.
resource diagnostico 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  scope: sitio
  name: 'log-analytics'
  properties: {
    workspaceId: logAnalyticsId
    logs: [
      { category: 'AppServiceConsoleLogs', enabled: true }
      { category: 'AppServiceAppLogs', enabled: true }
      { category: 'AppServicePlatformLogs', enabled: true }
    ]
    metrics: [{ category: 'AllMetrics', enabled: true }]
  }
}

output hostPredeterminado string = sitio.properties.defaultHostName
output hostRanura string = conRanura ? ranura!.properties.defaultHostName : ''
