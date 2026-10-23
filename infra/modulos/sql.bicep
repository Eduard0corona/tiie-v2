// Azure SQL solo con autenticación de Microsoft Entra. Los usuarios de base de datos (sitio y pipeline)
// los crea el grupo administrador con infra/sql/permisos.sql: Bicep no puede ejecutar CREATE USER.
param ubicacion string
param etiquetas object
param nombreServidor string
param nombreBase string
param administrador { nombre: string, objectId: string }
param sku object
param diasRestauracion int
param retencionLargoPlazo bool
param redundanciaRespaldos string
param logAnalyticsId string

resource servidor 'Microsoft.Sql/servers@2023-08-01' = {
  name: nombreServidor
  location: ubicacion
  tags: etiquetas
  properties: {
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    administrators: {
      administratorType: 'ActiveDirectory'
      principalType: 'Group'
      login: administrador.nombre
      sid: administrador.objectId
      tenantId: subscription().tenantId
      azureADOnlyAuthentication: true
    }
  }
}

// Permite conexiones desde servicios de Azure (el sitio no tiene red privada). Sin contraseñas, la regla
// solo abre el puerto; la autenticación sigue siendo de Entra. El pipeline agrega y quita su IP al migrar.
resource reglaAzure 'Microsoft.Sql/servers/firewallRules@2023-08-01' = {
  parent: servidor
  name: 'AllowAllWindowsAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource baseDatos 'Microsoft.Sql/servers/databases@2023-08-01' = {
  parent: servidor
  name: nombreBase
  location: ubicacion
  tags: etiquetas
  sku: sku
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    requestedBackupStorageRedundancy: redundanciaRespaldos
    zoneRedundant: false
    // Serverless (QA): sin pausa automática, porque los procesos del sitio consultan cada minuto.
    autoPauseDelay: startsWith(sku.name, 'GP_S_') ? -1 : null
    minCapacity: startsWith(sku.name, 'GP_S_') ? json('0.5') : null
  }
}

resource restauracion 'Microsoft.Sql/servers/databases/backupShortTermRetentionPolicies@2023-08-01' = {
  parent: baseDatos
  name: 'default'
  properties: {
    retentionDays: diasRestauracion
  }
}

// Propuesta de docs/13#respaldos: semanal 12 semanas y mensual 12 meses.
resource largoPlazo 'Microsoft.Sql/servers/databases/backupLongTermRetentionPolicies@2023-08-01' = if (retencionLargoPlazo) {
  parent: baseDatos
  name: 'default'
  properties: {
    weeklyRetention: 'P12W'
    monthlyRetention: 'P12M'
    weekOfYear: 1
    yearlyRetention: 'PT0S'
  }
}

// Auditoría del servidor hacia Log Analytics.
resource master 'Microsoft.Sql/servers/databases@2023-08-01' existing = {
  parent: servidor
  name: 'master'
}

resource diagnosticoAuditoria 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  scope: master
  name: 'auditoria'
  properties: {
    workspaceId: logAnalyticsId
    logs: [{ category: 'SQLSecurityAuditEvents', enabled: true }]
  }
}

resource auditoria 'Microsoft.Sql/servers/auditingSettings@2023-08-01' = {
  parent: servidor
  name: 'default'
  properties: {
    state: 'Enabled'
    isAzureMonitorTargetEnabled: true
  }
  dependsOn: [diagnosticoAuditoria]
}

output servidorFqdn string = servidor.properties.fullyQualifiedDomainName
output baseDatosId string = baseDatos.id
