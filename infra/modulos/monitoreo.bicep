// Log Analytics y Application Insights basado en el espacio de trabajo.
param ubicacion string
param etiquetas object
param nombreLogAnalytics string
param nombreAppInsights string
param diasRetencion int

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: nombreLogAnalytics
  location: ubicacion
  tags: etiquetas
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: diasRetencion
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: nombreAppInsights
  location: ubicacion
  tags: etiquetas
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
    RetentionInDays: diasRetencion
    IngestionMode: 'LogAnalytics'
  }
}

output logAnalyticsId string = logAnalytics.id
output appInsightsId string = appInsights.id
output cadenaAppInsights string = appInsights.properties.ConnectionString
