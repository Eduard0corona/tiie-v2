// Communication Services con su servicio de correo. El sitio envía con su identidad (sin cadena de conexión).
param etiquetas object
param nombreServicioCorreo string
param nombreComunicacion string
@description('Dominio propio. Vacío: dominio administrado por Azure, válido solo para QA.')
param dominio string
param ubicacionDatos string
param identidadPrincipalId string

var dominioPropio = !empty(dominio)

resource servicioCorreo 'Microsoft.Communication/emailServices@2025-05-01' = {
  name: nombreServicioCorreo
  location: 'global'
  tags: etiquetas
  properties: {
    dataLocation: ubicacionDatos
  }
}

// Con dominio propio, los registros DNS de verificación, SPF y DKIM se publican antes de poder enviar
// (salida registrosDns). El dominio administrado por Azure ya está verificado.
resource dominioCorreo 'Microsoft.Communication/emailServices/domains@2025-05-01' = {
  parent: servicioCorreo
  name: dominioPropio ? dominio : 'AzureManagedDomain'
  location: 'global'
  tags: etiquetas
  properties: {
    domainManagement: dominioPropio ? 'CustomerManaged' : 'AzureManaged'
    userEngagementTracking: 'Disabled'
  }
}

// El dominio administrado por Azure solo admite su remitente DoNotReply.
resource remitente 'Microsoft.Communication/emailServices/domains/senderUsernames@2025-05-01' = if (dominioPropio) {
  parent: dominioCorreo
  name: 'no-responder'
  properties: {
    username: 'no-responder'
    displayName: 'TIIE Sinaloa'
  }
}

resource comunicacion 'Microsoft.Communication/communicationServices@2025-05-01' = {
  name: nombreComunicacion
  location: 'global'
  tags: etiquetas
  properties: {
    dataLocation: ubicacionDatos
    linkedDomains: [dominioCorreo.id]
    // Sin claves de acceso: solo Microsoft Entra.
    disableLocalAuth: true
  }
}

// Mínimo para enviar correo con Microsoft Entra, según la documentación de Communication Services
// («Use a custom role to limit permissions»). Más acotado que Communication and Email Service Owner.
resource rolEnvio 'Microsoft.Authorization/roleDefinitions@2022-04-01' = {
  name: guid(resourceGroup().id, 'tiie-envio-correo')
  properties: {
    roleName: 'TIIE envío de correo (${resourceGroup().name})'
    description: 'Envía correo con Azure Communication Services.'
    type: 'CustomRole'
    assignableScopes: [resourceGroup().id]
    permissions: [
      {
        actions: [
          'Microsoft.Communication/CommunicationServices/Read'
          'Microsoft.Communication/CommunicationServices/Write'
          'Microsoft.Communication/EmailServices/write'
        ]
      }
    ]
  }
}

resource asignacionEnvio 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: comunicacion
  name: guid(comunicacion.id, identidadPrincipalId, rolEnvio.id)
  properties: {
    roleDefinitionId: rolEnvio.id
    principalId: identidadPrincipalId
    principalType: 'ServicePrincipal'
  }
}

output endpoint string = 'https://${comunicacion.properties.hostName}'
output remitente string = dominioPropio
  ? 'no-responder@${dominio}'
  : 'DoNotReply@${dominioCorreo.properties.mailFromSenderDomain}'
output registrosDns object = dominioPropio ? dominioCorreo.properties.verificationRecords : {}
