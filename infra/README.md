# Infraestructura

Bicep de los entornos de la TIIE. El diseño está en [docs/13](../docs/13-despliegue-y-operacion.md); aquí solo los pasos.

| Archivo | Contenido |
| --- | --- |
| `main.bicep` | Identidad del sitio y orquestación de los módulos |
| `modulos/monitoreo.bicep` | Log Analytics y Application Insights |
| `modulos/llaves.bicep` | Key Vault y la llave de Data Protection |
| `modulos/almacenamiento.bicep` | Storage con el contenedor `dataprotection` |
| `modulos/sql.bicep` | Azure SQL solo con Microsoft Entra, respaldos y auditoría |
| `modulos/correo.bicep` | Communication Services, dominio de correo y rol de envío |
| `modulos/sitio.bicep` | Plan, App Service y ranura `staging` (solo producción) |
| `modulos/alertas.bicep` | Grupos de acciones, prueba de disponibilidad y alertas |
| `qa.bicepparam`, `prod.bicepparam` | Parámetros por entorno |
| `sql/permisos.sql` | Usuarios de base de datos del sitio y del pipeline |

## Crear un entorno

Lo hace una persona con rol *Owner* sobre el grupo de recursos: la plantilla crea asignaciones de roles, un rol personalizado y bloqueos, que el pipeline (*Contributor*) no puede crear.

1. Completa los valores entre `<>` del archivo de parámetros.
2. Crea el grupo y aplica la plantilla:

   ```bash
   az group create -n rg-tiie-qa -l mexicocentral
   az deployment group create -g rg-tiie-qa -f infra/main.bicep -p infra/qa.bicepparam
   ```

3. Guarda los secretos en Key Vault (nunca en el repositorio ni en los parámetros):

   ```bash
   az keyvault secret set --vault-name kv-tiie-qa -n captcha-clave-secreta --value "<clave secreta de Turnstile>"
   az keyvault secret set --vault-name kv-tiie-qa -n semilla-contrasena-administrador --value "<contraseña inicial>"   # solo QA
   ```

4. Crea los usuarios de base de datos con `sql/permisos.sql` (instrucciones en el encabezado del script).
5. Configura la credencial federada del pipeline (sección siguiente) y ejecútalo una vez: crea el esquema con la primera migración.
6. Vuelve a ejecutar `sql/permisos.sql`: ahora sí aplica los permisos sobre la secuencia y la bitácora.
7. Con dominio propio, publica los registros DNS de la salida `registrosDnsCorreo` (verificación, SPF y DKIM) y verifica el dominio en el portal de Communication Services.
8. Configura el dominio personalizado y su certificado administrado en App Service.

## Cambios

- Prueba antes de aplicar: `az deployment group what-if -g rg-tiie-qa -f infra/main.bicep -p infra/qa.bicepparam`.
- Valida localmente con `bicep lint infra/main.bicep` y `bicep build-params infra/qa.bicepparam`.
- Los nombres siguen `<tipo>-tiie-<entorno>`. Key Vault y Storage necesitan nombres únicos en Azure: si ya existen, usa el parámetro `sufijoUnico`.

## Credencial del pipeline

El workflow `.github/workflows/despliegue.yml` entra a Azure con OIDC, sin secretos:

1. Crea un registro de aplicación por entorno (por ejemplo `gh-tiie-qa`) y usa el *object id* de su service principal en `principalDespliegueObjectId`.
2. Agrega una credencial federada con emisor `https://token.actions.githubusercontent.com` y sujeto `repo:<organización>/tiie-v2:environment:qa` (o `:environment:produccion`).
3. En GitHub, crea los entornos `qa` y `produccion`. En `produccion`, exige revisores: es la aprobación manual antes de producción.
4. En cada entorno, define las variables `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `GRUPO_RECURSOS`, `SITIO`, `SERVIDOR_SQL` y `URL_SITIO` (y `URL_RANURA` en producción), y los secretos `HUMO_USUARIO`, `HUMO_CONTRASENA` y `HUMO_TOTP` de un usuario de pruebas del panel con MFA. En `qa`, además, `E2E_USUARIO`, `E2E_CONTRASENA` y `E2E_TOTP` de un Administrador de pruebas: los flujos E2E del supervisor y del administrador lo necesitan ([tests/e2e/README.md](../tests/e2e/README.md)).
5. Cuando QA esté listo, define la variable de repositorio `DESPLIEGUE_HABILITADO` con `true`. Sin ella, el workflow solo construye los artefactos y omite QA y producción.
