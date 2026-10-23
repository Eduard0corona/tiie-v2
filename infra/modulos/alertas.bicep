// Alertas de docs/13#monitoreo-y-alertas. Severidad de Azure Monitor: 0 crítica, 1 alta, 2 media.
param ubicacion string
param etiquetas object
param nombreGrupoAcciones string
param correo string
@description('Celular de 10 dígitos para SMS de alertas críticas. Vacío: sin SMS.')
param celular string
param appInsightsId string
param sitioHost string
param baseDatosId string

resource grupoAcciones 'Microsoft.Insights/actionGroups@2023-01-01' = {
  name: nombreGrupoAcciones
  location: 'global'
  tags: etiquetas
  properties: {
    groupShortName: 'TIIE'
    enabled: true
    emailReceivers: [
      { name: 'equipo', emailAddress: correo, useCommonAlertSchema: true }
    ]
  }
}

// Las críticas también llegan por SMS.
resource grupoCriticas 'Microsoft.Insights/actionGroups@2023-01-01' = {
  name: '${nombreGrupoAcciones}-criticas'
  location: 'global'
  tags: etiquetas
  properties: {
    groupShortName: 'TIIE-CRIT'
    enabled: true
    emailReceivers: [
      { name: 'equipo', emailAddress: correo, useCommonAlertSchema: true }
    ]
    smsReceivers: empty(celular) ? [] : [
      { name: 'guardia', countryCode: '52', phoneNumber: celular }
    ]
  }
}

// Consultas sobre Application Insights: requests y customMetrics (métricas propias de Metricas.cs).
// La ventana limita los datos que ve la consulta, así que cubre el periodo más largo que usa cada una.
// Las alertas son con estado: mientras siguen activas no vuelven a notificar.
var consultas = [
  {
    nombre: 'errores-servidor'
    titulo: 'Más de 1 % de respuestas 5xx en 5 minutos'
    severidad: 1
    ventana: 'PT5M'
    frecuencia: 'PT5M'
    consulta: '''
requests
| summarize total = count(), errores = countif(toint(resultCode) >= 500)
| where total > 0 and 100.0 * errores / total > 1
'''
  }
  {
    nombre: 'reservas-anomalas'
    titulo: 'Más de 5 veces el promedio de reservas por minuto de la última hora'
    severidad: 2
    // La consulta compara con la hora anterior: la ventana debe cubrirla.
    ventana: 'PT2H'
    frecuencia: 'PT5M'
    consulta: '''
let promedio = toscalar(customMetrics
    | where timestamp between (ago(65m) .. ago(5m)) and name == "tiie.reservas"
    | summarize sum(valueSum) / 60.0);
customMetrics
| where timestamp > ago(5m) and name == "tiie.reservas"
| summarize porMinuto = sum(valueSum) / 5.0
| where porMinuto > 5 * max_of(promedio, 1.0)
'''
  }
  {
    nombre: 'bandeja-atrasada'
    titulo: 'Notificaciones vencidas hace más de 15 minutos sin enviar'
    severidad: 2
    ventana: 'PT10M'
    frecuencia: 'PT5M'
    consulta: '''
customMetrics
| where name == "tiie.notificaciones.atrasadas"
| summarize maximo = max(valueMax)
| where maximo > 0
'''
  }
  {
    nombre: 'notificaciones-descartadas'
    titulo: 'Notificación descartada tras agotar los reintentos'
    severidad: 2
    ventana: 'PT15M'
    frecuencia: 'PT5M'
    consulta: '''
customMetrics
| where name == "tiie.notificaciones.descartadas"
| summarize descartadas = sum(valueSum)
| where descartadas > 0
'''
  }
  {
    nombre: 'proceso-fallido'
    titulo: 'Proceso diario con error'
    severidad: 2
    ventana: 'PT15M'
    frecuencia: 'PT5M'
    consulta: '''
customMetrics
| where name == "tiie.procesos.duracion" and tostring(customDimensions["resultado"]) == "Error"
| summarize fallas = sum(valueCount) by proceso = tostring(customDimensions["proceso"])
'''
  }
  {
    nombre: 'proceso-sin-correr'
    titulo: 'Proceso diario sin correr en 26 horas'
    severidad: 2
    ventana: 'P2D'
    frecuencia: 'PT1H'
    consulta: '''
datatable(proceso: string)["GeneracionCupos", "CierreDiario", "Recordatorios"]
| join kind = leftanti (
    customMetrics
    | where timestamp > ago(26h) and name == "tiie.procesos.duracion"
    | extend proceso = tostring(customDimensions["proceso"])
    | distinct proceso
  ) on proceso
'''
  }
  {
    nombre: 'bloqueos-cuenta'
    titulo: 'Más de 10 respuestas de cuenta bloqueada en 10 minutos'
    severidad: 1
    ventana: 'PT10M'
    frecuencia: 'PT5M'
    // 423 es CuentaBloqueada (docs/06#errores).
    consulta: '''
requests
| where resultCode == "423"
| summarize bloqueos = count()
| where bloqueos > 10
'''
  }
]

resource alertasConsulta 'Microsoft.Insights/scheduledQueryRules@2023-12-01' = [
  for c in consultas: {
    name: 'alerta-${c.nombre}'
    location: ubicacion
    tags: etiquetas
    properties: {
      displayName: c.titulo
      severity: c.severidad
      enabled: true
      scopes: [appInsightsId]
      evaluationFrequency: c.frecuencia
      windowSize: c.ventana
      autoMitigate: true
      criteria: {
        allOf: [
          {
            query: c.consulta
            timeAggregation: 'Count'
            operator: 'GreaterThan'
            threshold: 0
            failingPeriods: { numberOfEvaluationPeriods: 1, minFailingPeriodsToAlert: 1 }
          }
        ]
      }
      actions: { actionGroups: [grupoAcciones.id] }
    }
  }
]

// Sitio caído: prueba de disponibilidad de /salud desde cinco ubicaciones; alerta si fallan dos.
resource pruebaDisponibilidad 'Microsoft.Insights/webtests@2022-06-15' = {
  name: 'salud-${sitioHost}'
  location: ubicacion
  tags: union(etiquetas, { 'hidden-link:${appInsightsId}': 'Resource' })
  kind: 'standard'
  properties: {
    SyntheticMonitorId: 'salud-${sitioHost}'
    Name: 'Salud del sitio'
    Kind: 'standard'
    Enabled: true
    Frequency: 300
    Timeout: 30
    RetryEnabled: true
    Locations: [
      { Id: 'us-tx-sn1-azr' }
      { Id: 'us-il-ch1-azr' }
      { Id: 'us-ca-sjc-azr' }
      { Id: 'us-va-ash-azr' }
      { Id: 'latam-br-gru-edge' }
    ]
    Request: {
      RequestUrl: 'https://${sitioHost}/salud'
      HttpVerb: 'GET'
      ParseDependentRequests: false
    }
    ValidationRules: {
      ExpectedHttpStatusCode: 200
      SSLCheck: true
      SSLCertRemainingLifetimeCheck: 14
    }
  }
}

resource alertaSitioCaido 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: 'alerta-sitio-caido'
  location: 'global'
  tags: etiquetas
  properties: {
    description: '/salud falla en 2 ubicaciones de la prueba de disponibilidad'
    severity: 0
    enabled: true
    scopes: [pruebaDisponibilidad.id, appInsightsId]
    evaluationFrequency: 'PT1M'
    windowSize: 'PT5M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.WebtestLocationAvailabilityCriteria'
      webTestId: pruebaDisponibilidad.id
      componentId: appInsightsId
      failedLocationCount: 2
    }
    actions: [{ actionGroupId: grupoCriticas.id }]
  }
}

resource alertaSqlCpu 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: 'alerta-sql-cpu'
  location: 'global'
  tags: etiquetas
  properties: {
    description: 'CPU de Azure SQL por encima de 80 % durante 10 minutos'
    severity: 1
    enabled: true
    scopes: [baseDatosId]
    evaluationFrequency: 'PT1M'
    windowSize: 'PT10M'
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          criterionType: 'StaticThresholdCriterion'
          name: 'cpu'
          metricName: 'cpu_percent'
          operator: 'GreaterThan'
          threshold: 80
          timeAggregation: 'Average'
        }
      ]
    }
    actions: [{ actionGroupId: grupoAcciones.id }]
  }
}
