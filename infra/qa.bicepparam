// QA: misma topología que producción con SKUs menores, sin ranura y con correo restringido al equipo.
// Los valores entre <> se completan al crear la suscripción (docs/01#preguntas-abiertas).
using 'main.bicep'

param entorno = 'qa'
param administradorSql = {
  nombre: '<grupo-entra-administradores-sql>'
  objectId: '<object-id-del-grupo>'
}
param principalDespliegueObjectId = '<object-id-del-service-principal-de-github-actions>'
param urlPublica = 'https://app-tiie-qa.azurewebsites.net'
param versionAvisoPrivacidad = '2026-01'
param licenciaQuestPdf = 'Community'
param claveSitioCaptcha = '<clave-de-sitio-de-turnstile-de-qa>'
param destinatariosPermitidos = [
  '<correo-del-equipo>'
]
param correoAlertas = '<correo-del-equipo>'
