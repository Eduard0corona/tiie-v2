// Recursos de un entorno de la TIIE (docs/13-despliegue-y-operacion.md#recursos-de-azure).
// Lo aplica una persona con rol Owner sobre el grupo de recursos, porque crea asignaciones de roles,
// un rol personalizado y bloqueos. El pipeline de despliegue solo publica código y migraciones.
//
//   az deployment group create -g rg-tiie-qa -f infra/main.bicep -p infra/qa.bicepparam
targetScope = 'resourceGroup'

@description('Entorno: qa o prod. Define nombres, SKUs, retenciones y si hay ranura staging.')
@allowed(['qa', 'prod'])
param entorno string

@description('Región de los recursos regionales.')
param ubicacion string = resourceGroup().location

@description('Sufijo opcional para nombres que deben ser únicos en Azure (Key Vault y Storage), por ejemplo "01".')
@maxLength(4)
param sufijoUnico string = ''

@description('Grupo de Microsoft Entra que administra Azure SQL. Es el único que puede crear usuarios de base de datos.')
param administradorSql {
  @description('Nombre visible del grupo.')
  nombre: string
  @description('Object ID del grupo.')
  objectId: string
}

@description('Service principal del pipeline (credencial federada OIDC). Recibe Contributor sobre el grupo de recursos.')
param principalDespliegueObjectId string

@description('URL pública del sitio, sin diagonal final.')
param urlPublica string

@description('Versión vigente del aviso de privacidad (Sitio:VersionAvisoPrivacidad).')
param versionAvisoPrivacidad string

@description('Licencia de QuestPDF: Community, Professional o Enterprise. Ver docs/01#preguntas-abiertas.')
@allowed(['Community', 'Professional', 'Enterprise'])
param licenciaQuestPdf string

@description('Clave pública de Turnstile. La secreta se guarda a mano en Key Vault como captcha-clave-secreta.')
param claveSitioCaptcha string

@description('Dominio propio para el correo. Vacío: dominio administrado por Azure (solo para QA).')
param dominioCorreo string = ''

@description('Destinatarios permitidos en QA (Correo:DestinatariosPermitidos). Vacío en producción.')
param destinatariosPermitidos array = []

@description('Correo del equipo para las alertas.')
param correoAlertas string

@description('Celular de 10 dígitos para las alertas críticas por SMS. Vacío: sin SMS.')
param celularAlertas string = ''

@description('SKU del plan de App Service.')
param skuPlan string = entorno == 'prod' ? 'P0v3' : 'B1'

@description('Instancias del plan; 2 en temporada.')
@minValue(1)
param instancias int = 1

@description('SKU de la base de datos.')
param skuBaseDatos object = entorno == 'prod'
  ? { name: 'GP_Gen5', tier: 'GeneralPurpose', family: 'Gen5', capacity: 2 }
  : { name: 'GP_S_Gen5', tier: 'GeneralPurpose', family: 'Gen5', capacity: 1 }

@description('Ubicación de los datos de Communication Services.')
param ubicacionDatosCorreo string = 'United States'

var esProduccion = entorno == 'prod'
var nombres = {
  identidad: 'id-tiie-${entorno}'
  plan: 'asp-tiie-${entorno}'
  sitio: 'app-tiie-${entorno}'
  sqlServidor: 'sql-tiie-${entorno}'
  sqlBase: 'sqldb-tiie'
  keyVault: 'kv-tiie-${entorno}${sufijoUnico}'
  storage: 'sttiie${entorno}${sufijoUnico}'
  correo: 'ecs-tiie-${entorno}'
  comunicacion: 'acs-tiie-${entorno}'
  logAnalytics: 'log-tiie-${entorno}'
  appInsights: 'appi-tiie-${entorno}'
  grupoAcciones: 'ag-tiie-${entorno}'
}
var etiquetas = {
  sistema: 'TIIE'
  entorno: entorno
}

// Identidad asignada por el usuario: la comparten el sitio y la ranura staging, así el intercambio de ranuras
// no cambia la identidad con la que el código entra a SQL, Key Vault, Storage y Communication Services.
resource identidad 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: nombres.identidad
  location: ubicacion
  tags: etiquetas
}

module monitoreo 'modulos/monitoreo.bicep' = {
  name: 'monitoreo'
  params: {
    ubicacion: ubicacion
    etiquetas: etiquetas
    nombreLogAnalytics: nombres.logAnalytics
    nombreAppInsights: nombres.appInsights
    diasRetencion: esProduccion ? 90 : 30
  }
}

module llaves 'modulos/llaves.bicep' = {
  name: 'llaves'
  params: {
    ubicacion: ubicacion
    etiquetas: etiquetas
    nombre: nombres.keyVault
    identidadPrincipalId: identidad.properties.principalId
    logAnalyticsId: monitoreo.outputs.logAnalyticsId
    bloquearBorrado: esProduccion
  }
}

module almacenamiento 'modulos/almacenamiento.bicep' = {
  name: 'almacenamiento'
  params: {
    ubicacion: ubicacion
    etiquetas: etiquetas
    nombre: nombres.storage
    identidadPrincipalId: identidad.properties.principalId
    redundancia: esProduccion ? 'Standard_ZRS' : 'Standard_LRS'
    bloquearBorrado: esProduccion
  }
}

module sql 'modulos/sql.bicep' = {
  name: 'sql'
  params: {
    ubicacion: ubicacion
    etiquetas: etiquetas
    nombreServidor: nombres.sqlServidor
    nombreBase: nombres.sqlBase
    administrador: administradorSql
    sku: skuBaseDatos
    diasRestauracion: esProduccion ? 35 : 7
    retencionLargoPlazo: esProduccion
    redundanciaRespaldos: esProduccion ? 'Geo' : 'Local'
    logAnalyticsId: monitoreo.outputs.logAnalyticsId
  }
}

module correo 'modulos/correo.bicep' = {
  name: 'correo'
  params: {
    etiquetas: etiquetas
    nombreServicioCorreo: nombres.correo
    nombreComunicacion: nombres.comunicacion
    dominio: dominioCorreo
    ubicacionDatos: ubicacionDatosCorreo
    identidadPrincipalId: identidad.properties.principalId
  }
}

module sitio 'modulos/sitio.bicep' = {
  name: 'sitio'
  params: {
    ubicacion: ubicacion
    etiquetas: etiquetas
    nombrePlan: nombres.plan
    nombreSitio: nombres.sitio
    skuPlan: skuPlan
    instancias: instancias
    conRanura: esProduccion
    identidadId: identidad.id
    identidadClientId: identidad.properties.clientId
    logAnalyticsId: monitoreo.outputs.logAnalyticsId
    configuracion: {
      ASPNETCORE_ENVIRONMENT: esProduccion ? 'Production' : 'QA'
      ConnectionStrings__Tiie: 'Server=tcp:${sql.outputs.servidorFqdn},1433;Database=${nombres.sqlBase};Authentication=Active Directory Default;User Id=${identidad.properties.clientId};Encrypt=True'
      Sitio__UrlPublica: urlPublica
      Sitio__VersionAvisoPrivacidad: versionAvisoPrivacidad
      Correo__Proveedor: 'Acs'
      Correo__AcsEndpoint: correo.outputs.endpoint
      Correo__Remitente: correo.outputs.remitente
      Captcha__ClaveSitio: claveSitioCaptcha
      Captcha__ClaveSecreta: '@Microsoft.KeyVault(VaultName=${nombres.keyVault};SecretName=captcha-clave-secreta)'
      DataProtection__BlobUri: almacenamiento.outputs.blobLlavesUri
      // Usar una salida de llaves hace esperar a sus roles: las referencias a Key Vault se resuelven al arrancar.
      DataProtection__KeyUri: llaves.outputs.llaveDataProtectionUri
      Documentos__LicenciaQuestPdf: licenciaQuestPdf
      APPLICATIONINSIGHTS_CONNECTION_STRING: monitoreo.outputs.cadenaAppInsights
    }
    configuracionQa: esProduccion
      ? {}
      : union(
          {
            // El administrador semilla de QA (docs/07#usuarios-de-desarrollo); producción no siembra.
            Semilla__ContrasenaAdministrador: '@Microsoft.KeyVault(VaultName=${nombres.keyVault};SecretName=semilla-contrasena-administrador)'
          },
          reduce(range(0, length(destinatariosPermitidos)), {}, (acc, i) => union(acc, {
            'Correo__DestinatariosPermitidos__${i}': destinatariosPermitidos[i]
          }))
        )
  }
}

module alertas 'modulos/alertas.bicep' = {
  name: 'alertas'
  params: {
    ubicacion: ubicacion
    etiquetas: etiquetas
    nombreGrupoAcciones: nombres.grupoAcciones
    correo: correoAlertas
    celular: celularAlertas
    appInsightsId: monitoreo.outputs.appInsightsId
    sitioHost: sitio.outputs.hostPredeterminado
    baseDatosId: sql.outputs.baseDatosId
  }
}

// Contributor sobre el grupo: publicar el sitio, intercambiar ranuras y abrir el firewall de SQL al aplicar migraciones.
resource rolDespliegue 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, principalDespliegueObjectId, 'Contributor')
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'b24988ac-6180-42a0-ab88-20f7382dd24c')
    principalId: principalDespliegueObjectId
    principalType: 'ServicePrincipal'
  }
}

output sitio string = nombres.sitio
output hostSitio string = sitio.outputs.hostPredeterminado
output hostRanura string = sitio.outputs.hostRanura
output servidorSql string = sql.outputs.servidorFqdn
output baseDatos string = nombres.sqlBase
output identidadSitio string = nombres.identidad
output keyVault string = nombres.keyVault
output registrosDnsCorreo object = correo.outputs.registrosDns
