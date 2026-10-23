-- Volumen sintético para analizar planes de ejecución (docs/18-analisis-de-base-de-datos.md).
-- Unos 3 años de operación de 40 módulos con 3 trámites: 1.3 millones de cupos, 2.1 millones de citas,
-- 3.5 millones de notificaciones y 2.2 millones de registros de auditoría. Determinista: los valores
-- «aleatorios» salen de CHECKSUM. Tarda unos 5 minutos en SQL Server 2022 en contenedor.
--
-- Solo para una base local vacía con las migraciones aplicadas; se niega a correr si ya hay citas:
--
--   dotnet ef migrations script --idempotent --project src/Tiie.Infraestructura --startup-project src/Tiie.Web -o esquema.sql
--   sqlcmd -S 127.0.0.1 -U sa -I -Q "CREATE DATABASE TiieAnalisis"
--   sqlcmd -S 127.0.0.1 -U sa -I -d TiieAnalisis -i esquema.sql
--   sqlcmd -S 127.0.0.1 -U sa -I -d TiieAnalisis -v Hoy="2026-10-23" -i tools/volumen-sintetico.sql
--
-- Hoy es la fecha local que se toma como «hoy»: hay citas atendidas antes y agendadas hasta 49 días después.

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() IN (N'master', N'model', N'msdb', N'tempdb') OR EXISTS (SELECT 1 FROM dbo.Cita) OR EXISTS (SELECT 1 FROM dbo.Modulo)
    THROW 50000, N'La base no está vacía: este script solo se usa en una base local nueva.', 1;

ALTER DATABASE CURRENT SET RECOVERY SIMPLE;

DECLARE @hoy date = CAST(N'$(Hoy)' AS date);
DECLARE @desde date = DATEADD(year, -2, DATEFROMPARTS(YEAR(@hoy), 1, 1));
DECLARE @hasta date = DATEADD(day, 49, @hoy);
DECLARE @ahora datetimeoffset(0) = TODATETIMEOFFSET(DATEADD(hour, 17, CAST(@hoy AS datetime2(0))), '+00:00');   -- 10:00 en Sinaloa
DECLARE @corte datetime2(0) = DATEADD(hour, 17, CAST(@hoy AS datetime2(0)));

IF OBJECT_ID('tempdb..#n') IS NOT NULL DROP TABLE #n;
SELECT TOP (2000000) n = CAST(ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS int)
INTO #n FROM sys.all_columns a CROSS JOIN sys.all_columns b CROSS JOIN sys.all_columns c;
CREATE UNIQUE CLUSTERED INDEX ix ON #n (n);

-- Catálogos
INSERT dbo.Entidad (EntidadId, Nombre, Activo) VALUES (25, N'Sinaloa', 1);
INSERT dbo.Municipio (MunicipioId, EntidadId, Nombre, Activo)
SELECT n, 25, CONCAT(N'Municipio ', n), 1 FROM #n WHERE n <= 20;

INSERT dbo.TipoTramite (Nombre, Descripcion, Costo, Notas, Activo, CreadoEn)
VALUES (N'Primera vez', N'Credencial nueva', 0, N'Notas', 1, @ahora),
       (N'Sustitución', N'Reposición', 150, N'Notas', 1, @ahora),
       (N'Reactivación', N'Reactivar', 50, N'Notas', 1, @ahora);
INSERT dbo.Requisito (TipoTramiteId, NivelEscolarId, Texto, Orden)
SELECT t.TipoTramiteId, CASE WHEN r.n % 2 = 0 THEN 1 ELSE 2 END, CONCAT(N'Requisito ', r.n), r.n
FROM dbo.TipoTramite t CROSS JOIN #n r WHERE r.n <= 4;

INSERT dbo.Modulo (MunicipioId, Nombre, Calle, NumeroExterior, Colonia, CodigoPostal, Activo, CreadoEn, HorarioAtencion)
SELECT (n % 20) + 1, CONCAT(N'Módulo ', n), N'Calle', N'S/N', N'Centro', '80000', CASE WHEN n = 40 THEN 0 ELSE 1 END, @ahora, N'8 a 14'
FROM #n WHERE n <= 40;

INSERT dbo.Oferta (ModuloId, TipoTramiteId, DiasVentana, Activo, CreadoEn)
SELECT m.ModuloId, t.TipoTramiteId, 49, 1, @ahora FROM dbo.Modulo m CROSS JOIN dbo.TipoTramite t;

INSERT dbo.PlantillaHorario (OfertaId, DiaSemana, Hora, Turnos)
SELECT o.OfertaId, d.n, DATEADD(minute, 30 * (h.n - 1), CAST('08:00' AS time(0))), 3
FROM dbo.Oferta o CROSS JOIN #n d CROSS JOIN #n h WHERE d.n BETWEEN 1 AND 6 AND h.n <= 12;

-- Personal: 2 administradores, 8 supervisores, 50 capturistas
INSERT dbo.Usuario (Nombre, ApellidoPaterno, Activo, CreadoEn, UserName, NormalizedUserName, Email, NormalizedEmail,
    EmailConfirmed, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled, AccessFailedCount)
SELECT CONCAT(N'Usuario', n), N'Prueba', 1, @ahora, CONCAT('u', n), CONCAT('U', n), CONCAT('u', n, '@ejemplo.gob.mx'),
    CONCAT('U', n, '@EJEMPLO.GOB.MX'), 1, 0, 1, 1, 0
FROM #n WHERE n <= 60;
INSERT dbo.UsuarioRol (UserId, RoleId) SELECT Id, CASE WHEN Id <= 2 THEN 1 WHEN Id <= 10 THEN 2 ELSE 3 END FROM dbo.Usuario;
INSERT dbo.UsuarioModulo (UsuarioId, ModuloId) SELECT Id, ((Id - 11) % 40) + 1 FROM dbo.Usuario WHERE Id > 10;
INSERT dbo.UsuarioModulo (UsuarioId, ModuloId) SELECT u.Id, m.ModuloId FROM dbo.Usuario u JOIN dbo.Modulo m ON m.ModuloId % 8 = u.Id - 3 WHERE u.Id BETWEEN 3 AND 10;

-- Bloqueos: días inhábiles generales y algunos por módulo
INSERT dbo.Bloqueo (Desde, Hasta, ModuloId, OfertaId, Motivo, CreadoPor, CreadoEn)
SELECT DATEADD(day, 30 * n, @desde), DATEADD(day, 30 * n, @desde), CASE WHEN n % 3 = 0 THEN (n % 40) + 1 END, NULL, N'Día inhábil', 1, @ahora
FROM #n WHERE n <= 36;

-- Cupos: lunes a sábado, 12 horas, capacidad 3
PRINT 'Cupos';
INSERT dbo.Cupo (OfertaId, Fecha, Hora, Capacidad, Ocupados, Cerrado)
SELECT o.OfertaId, f.Fecha, DATEADD(minute, 30 * (h.n - 1), CAST('08:00' AS time(0))), 3,
    CASE
        WHEN f.Fecha <= @hoy THEN ABS(CHECKSUM(o.OfertaId, f.Fecha, h.n)) % 4
        WHEN DATEDIFF(day, @hoy, f.Fecha) <= 7 THEN 3
        WHEN DATEDIFF(day, @hoy, f.Fecha) <= 20 THEN ABS(CHECKSUM(o.OfertaId, f.Fecha, h.n)) % 3 + 1
        WHEN DATEDIFF(day, @hoy, f.Fecha) <= 35 THEN ABS(CHECKSUM(o.OfertaId, f.Fecha, h.n)) % 2
        ELSE 0
    END,
    0
FROM dbo.Oferta o
CROSS JOIN (SELECT Fecha = DATEADD(day, n - 1, @desde) FROM #n WHERE n <= DATEDIFF(day, @desde, @hasta) + 1) f
CROSS JOIN #n h
WHERE h.n <= 12 AND DATEDIFF(day, '19000107', f.Fecha) % 7 <> 0;

-- Citas que ocupan lugar + canceladas y reprogramadas (no ocupan)
PRINT 'Citas (preparación)';
IF OBJECT_ID('tempdb..#c') IS NOT NULL DROP TABLE #c;
SELECT c.CupoId, c.OfertaId, c.Fecha, c.Hora, k.n AS k,
    Estado = CAST(CASE
        WHEN c.Fecha < @hoy THEN CASE WHEN ABS(CHECKSUM(c.CupoId, k.n)) % 100 < 85 THEN 2 ELSE 3 END
        WHEN c.Fecha = @hoy AND c.Hora < '10:15' THEN CASE WHEN ABS(CHECKSUM(c.CupoId, k.n)) % 100 < 85 THEN 2 ELSE 3 END
        ELSE 1 END AS tinyint)
INTO #c
FROM dbo.Cupo c JOIN #n k ON k.n <= c.Ocupados;
INSERT #c (CupoId, OfertaId, Fecha, Hora, k, Estado)
SELECT c.CupoId, c.OfertaId, c.Fecha, c.Hora, 9, CASE WHEN ABS(CHECKSUM(c.CupoId)) % 100 < 72 THEN 4 ELSE 5 END
FROM dbo.Cupo c WHERE ABS(CHECKSUM(c.CupoId, 'x')) % 100 < 11 AND c.Fecha <= DATEADD(day, 30, @hoy);

ALTER TABLE #c ADD rn int NULL, sol int NULL;
WITH x AS (SELECT rn, r = ROW_NUMBER() OVER (ORDER BY Fecha, Hora, CupoId, k) FROM #c) UPDATE x SET rn = r;
DECLARE @agendadas int = (SELECT COUNT(*) FROM #c WHERE Estado = 1);
WITH x AS (SELECT sol, r = ROW_NUMBER() OVER (ORDER BY rn) FROM #c WHERE Estado = 1) UPDATE x SET sol = r;
UPDATE #c SET sol = @agendadas + 1 + (rn % 900000) WHERE Estado <> 1;

PRINT 'Solicitantes';
INSERT dbo.Solicitante (Curp, Nombre, ApellidoPaterno, ApellidoMaterno, FechaNacimiento, Sexo, Celular, Correo, CreadoEn)
SELECT CONCAT('CURP', RIGHT(CONCAT('00000000000000', n), 14)), CONCAT(N'Nombre', n), N'Apellido', N'Materno',
    DATEADD(day, -(n % 4000) - 5000, @hoy), CASE WHEN n % 2 = 0 THEN 'H' ELSE 'M' END, '6670000000',
    CASE WHEN n % 20 = 0 THEN NULL ELSE CONCAT(N'persona', n, N'@correo.mx') END, @ahora
FROM #n WHERE n <= @agendadas + 900000;

PRINT 'Citas';
SET IDENTITY_INSERT dbo.Cita ON;
INSERT dbo.Cita (CitaId, Folio, CupoId, SolicitanteId, Estado, Origen, TokenHash, TokenProtegido, TokenVenceEn,
    ClaveIdempotencia, VersionAvisoPrivacidad, AvisoAceptadoEn, FolioAnterior, CreadaEn, CreadaPor, ActualizadaEn, ActualizadaPor, MotivoCambio)
SELECT rn,
    CONCAT(FORMAT(d.creada, 'yyMMdd'), '-', 1000000 + rn),
    CupoId, sol, Estado,
    CASE WHEN Fecha < DATEADD(month, 6, @desde) AND rn % 3 = 0 THEN 3 WHEN rn % 10 = 0 THEN 2 ELSE 1 END,
    HASHBYTES('SHA2_256', CAST(rn AS varchar(12))),
    REPLICATE(N'p', 180),
    TODATETIMEOFFSET(DATEADD(day, 1, CAST(Fecha AS datetime2(0))), '+00:00'),
    CASE WHEN rn % 10 <> 0 THEN NEWID() END,
    '2026-01', TODATETIMEOFFSET(d.creada, '+00:00'),
    CASE WHEN Fecha < DATEADD(month, 6, @desde) AND rn % 3 = 0 THEN CONCAT('A', rn) END,
    TODATETIMEOFFSET(d.creada, '+00:00'),
    CASE WHEN rn % 10 = 0 THEN 11 + rn % 50 END,
    CASE WHEN Estado <> 1 THEN TODATETIMEOFFSET(CAST(Fecha AS datetime2(0)), '+00:00') END,
    CASE WHEN Estado IN (2, 3) THEN 11 + rn % 50 END,
    CASE WHEN Estado IN (4, 5) THEN N'Motivo' END
FROM #c
CROSS APPLY (SELECT base = DATEADD(minute, rn % 600, DATEADD(day, -(rn % 40), CAST(Fecha AS datetime2(0))))) b
CROSS APPLY (SELECT creada = CASE WHEN b.base > @corte THEN DATEADD(minute, -(rn % 20000), @corte) ELSE b.base END) d;
SET IDENTITY_INSERT dbo.Cita OFF;
-- Algunas citas nacen de una reprogramación
UPDATE c SET CitaOrigenId = r.CitaId
FROM dbo.Cita c JOIN dbo.Cita r ON r.CitaId = c.CitaId - 1
WHERE r.Estado = 5 AND c.Estado <> 5;

PRINT 'Notificaciones';
INSERT dbo.Notificacion (CitaId, Tipo, Canal, Destinatario, Estado, Intentos, ProximoIntentoEn, EnviadaEn, UltimoError, CreadaEn)
SELECT c.CitaId, 1, 1, s.Correo,
    CASE WHEN c.CreadaEn > DATEADD(minute, -5, @ahora) THEN 1 WHEN c.CitaId % 500 = 0 THEN 4 ELSE 2 END,
    1, c.CreadaEn, CASE WHEN c.CitaId % 500 <> 0 THEN c.CreadaEn END,
    CASE WHEN c.CitaId % 500 = 0 THEN N'Buzón lleno' END, c.CreadaEn
FROM dbo.Cita c JOIN dbo.Solicitante s ON s.SolicitanteId = c.SolicitanteId WHERE s.Correo IS NOT NULL;
INSERT dbo.Notificacion (CitaId, Tipo, Canal, Destinatario, Estado, Intentos, ProximoIntentoEn, EnviadaEn, CreadaEn)
SELECT c.CitaId, 2, 1, s.Correo, 2, 1, x.Momento, x.Momento, x.Momento
FROM dbo.Cita c JOIN dbo.Cupo cu ON cu.CupoId = c.CupoId JOIN dbo.Solicitante s ON s.SolicitanteId = c.SolicitanteId
CROSS APPLY (SELECT Momento = TODATETIMEOFFSET(DATEADD(hour, -6, CAST(cu.Fecha AS datetime2(0))), '+00:00')) x
WHERE s.Correo IS NOT NULL AND cu.Fecha <= @hoy AND c.CitaId % 10 < 7;
-- Bandeja viva: algunas pendientes y fallidas por reintentar
INSERT dbo.Notificacion (CitaId, Tipo, Canal, Destinatario, Estado, Intentos, ProximoIntentoEn, CreadaEn)
SELECT TOP (300) c.CitaId, 1, 1, N'x@correo.mx', CASE WHEN c.CitaId % 3 = 0 THEN 3 ELSE 1 END, 0,
    DATEADD(minute, (c.CitaId % 30) - 10, @ahora), @ahora
FROM dbo.Cita c WHERE c.Estado = 1 ORDER BY c.CitaId DESC;

PRINT 'Auditoría';
-- Asistencia registrada por el personal, ventanilla y sesiones
INSERT dbo.Auditoria (Fecha, UsuarioId, Accion, Entidad, EntidadId, Cambios, Ip)
SELECT c.ActualizadaEn, c.ActualizadaPor, 'Cita.Asistencia', 'Cita', CAST(c.CitaId AS varchar(40)), N'{"estado":"Atendida"}', '10.0.0.1'
FROM dbo.Cita c WHERE c.Estado IN (2, 3) AND c.ActualizadaPor IS NOT NULL;
INSERT dbo.Auditoria (Fecha, UsuarioId, Accion, Entidad, EntidadId, Cambios, Ip)
SELECT c.CreadaEn, c.CreadaPor, 'Cita.Crear', 'Cita', CAST(c.CitaId AS varchar(40)), N'{"folio":"x","curp":"ABCD************01"}', '10.0.0.1'
FROM dbo.Cita c WHERE c.CreadaPor IS NOT NULL;
INSERT dbo.Auditoria (Fecha, UsuarioId, Accion, Entidad, EntidadId, Cambios, Ip)
SELECT DATEADD(hour, n % 8, DATEADD(day, n / 60, CAST(@desde AS datetimeoffset(0)))), 1 + n % 60, 'Sesion.Iniciar', 'Usuario', CAST(1 + n % 60 AS varchar(40)), NULL, '10.0.0.1'
FROM #n WHERE n <= 60 * 1000;

-- Citas de ayer sin asistencia registrada, para el cierre diario
UPDATE c SET Estado = 1, ActualizadaEn = NULL, ActualizadaPor = NULL
FROM dbo.Cita c JOIN dbo.Cupo cu ON cu.CupoId = c.CupoId
WHERE cu.Fecha = DATEADD(day, -1, @hoy) AND c.Estado = 3;

PRINT 'Estadísticas';
EXEC sp_updatestats;
SELECT t.name, SUM(p.rows) AS filas
FROM sys.tables t JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0, 1)
GROUP BY t.name ORDER BY filas DESC;
