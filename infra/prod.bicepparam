// Producción. Los valores entre <> se completan al crear la suscripción (docs/01#preguntas-abiertas).
using 'main.bicep'

param entorno = 'prod'
param administradorSql = {
  nombre: '<grupo-entra-administradores-sql>'
  objectId: '<object-id-del-grupo>'
}
param principalDespliegueObjectId = '<object-id-del-service-principal-de-github-actions>'
param urlPublica = 'https://<dominio-publico>'
param versionAvisoPrivacidad = '2026-01'
// Pendiente de decidir (docs/01#preguntas-abiertas): Community solo aplica si la organización califica.
param licenciaQuestPdf = 'Professional'
param claveSitioCaptcha = '<clave-de-sitio-de-turnstile>'
param dominioCorreo = 'tarjetainteligentesinaloa.org.mx'
param correoAlertas = '<correo-del-equipo>'
param celularAlertas = '<celular-de-guardia>'
