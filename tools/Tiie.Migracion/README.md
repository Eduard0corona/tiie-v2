# Tiie.Migracion

Consola .NET 10 que migra la base de 2016 a la versión 2. El diseño, el mapeo y el plan de corte están en [docs/14](../../docs/14-migracion-de-datos.md).

> **El esquema de origen es supuesto.** No había un script de la base de 2016 en el repositorio: [Origen/esquema-supuesto.sql](Origen/esquema-supuesto.sql) se reconstruyó a partir de docs/14. Antes del primer ensayo, compáralo con el script real y ajusta solo [Origen/LectorOrigen.cs](Origen/LectorOrigen.cs). `analizar` falla y lista las columnas que faltan si no coinciden.

## Comandos

```bash
# Diagnóstico y reportes; no escribe en ninguna base
dotnet run --project tools/Tiie.Migracion -- analizar --origen "<cadena 2016>" --fecha-corte 2026-11-30

# Migración repetible
dotnet run --project tools/Tiie.Migracion -- migrar \
  --origen "<cadena 2016>" --destino "<cadena v2>" \
  --fecha-corte 2026-11-30 \
  --mapa-roles "0=Capturista,1=Supervisor,2=Administrador" \
  --decisiones decisiones-duplicados.csv

# Comparación de conteos: código 1 si hay diferencias
dotnet run --project tools/Tiie.Migracion -- conciliar --origen "<cadena 2016>" --destino "<cadena v2>" \
  --fecha-corte 2026-11-30 --mapa-roles "0=Capturista,1=Supervisor,2=Administrador" --decisiones decisiones-duplicados.csv

# Avisos a los ciudadanos: solo después del cambio de DNS
dotnet run --project tools/Tiie.Migracion -- avisar --destino "<cadena v2>"
```

| Opción | Uso |
| --- | --- |
| `--salida <carpeta>` | Dónde quedan los CSV; por omisión `artifacts/migracion/<fecha y hora>` |
| `--reemplazar-plantillas` | Reescribe las plantillas de las ofertas que ya tienen una |
| `--llaves-locales` | Protege los tokens con las llaves del perfil local; solo para un destino local |

## Llaves de los tokens

Cada cita migrada recibe un token nuevo, protegido con Data Protection para armar el enlace del aviso. La herramienta debe usar **las mismas llaves que el sitio**; si no, los enlaces no abren. Antes de `migrar` contra Azure:

```bash
export DataProtection__BlobUri="https://sttiieprod.blob.core.windows.net/dataprotection/llaves.xml"
export DataProtection__KeyUri="https://kv-tiie-prod.vault.azure.net/keys/dataprotection"
az login   # la persona que migra necesita Storage Blob Data Contributor y Key Vault Crypto User
```

Sin esas variables, `migrar` se niega a correr salvo con `--llaves-locales`.

## Qué hace en cada corrida

- Reconoce lo ya migrado por `IdAnterior` (módulos y ofertas), nombre normalizado (tipos de trámite), `FolioAnterior` (citas) y nombre de usuario. No duplica nada.
- No sobrescribe catálogos que ya existen: operación pudo corregirlos en el panel después de un ensayo. Los totales históricos sí se reemplazan.
- Ocupa los cupos con un `UPDATE` condicional en la misma transacción que las citas, un cupo a la vez.
- Al final genera los cupos de la ventana a partir de las plantillas, como el proceso diario del sitio.
- Los usuarios quedan sin contraseña y sin correo confirmado; un administrador les reenvía la invitación desde `/admin/usuarios`.

## Reportes

| Archivo | Contenido |
| --- | --- |
| `modulos.csv` | Módulos con datos incompletos (se migran con «Sin dato» o CP `00000`) o con municipio inexistente |
| `tipos-tramite.csv` | Trámites agrupados en cada tipo y requisito por reestructurar por nivel |
| `tramites-sin-modulo.csv` | Trámites sin módulo válido: no generan oferta |
| `ofertas-fusionadas.csv` | Trámites del mismo tipo en un módulo que quedaron en una sola oferta |
| `diferencias-tramite.csv` | Notas o requisitos de una sede distintos a los del tipo (van a `NotasSede`) |
| `horas-sospechosas.csv` | Horarios activos antes de las 07:00 o después de las 19:00 |
| `excepciones.csv` | Citas futuras sin horario o de un trámite sin oferta: no se migran |
| `duplicados-curp.csv` | CURP con varias citas futuras y lo que se hizo con cada una |
| `curps-invalidas.csv` | CURP que no pasan la validación; se migran igual |
| `sobrecupos.csv` | Cupos con más citas que turnos; la capacidad toma el número de citas |
| `historico-sin-oferta.csv` | Citas pasadas por mes que no se pudieron asignar a una oferta |
| `usuarios.csv` | Qué pasó con cada usuario |
| `rechazos-destino.csv` | Lo que la versión 2 no aceptó (por ejemplo, un usuario con nombre inválido) |
| `conciliacion.csv` | Resultado de `conciliar` |

Los reportes contienen datos personales: genéralos solo en un equipo autorizado y bórralos después del corte.

## Pruebas

- Unitarias del plan: `tests/Tiie.Migracion.Tests`.
- De punta a punta contra SQL Server en contenedor, con una base de origen creada con el esquema supuesto: `tests/Tiie.Integracion.Tests/MigracionDatos`.
