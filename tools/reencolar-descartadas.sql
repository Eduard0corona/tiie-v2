-- Runbook «Reenviar correos descartados» (docs/13-despliegue-y-operacion.md#runbooks).
-- Devuelve a Pendiente las notificaciones descartadas tras agotar los reintentos en un rango de creación.
-- Corrige primero la causa (por ejemplo, el dominio del remitente). El envío vuelve a comprobar que la cita
-- siga agendada antes de mandar confirmaciones y recordatorios.
--
--   sqlcmd -S sql-tiie-prod.database.windows.net -d sqldb-tiie --authentication-method ActiveDirectoryDefault ^
--     -v Desde="2026-11-01T00:00:00-07:00" Hasta="2026-11-02T00:00:00-07:00" Aplicar=0 -i tools/reencolar-descartadas.sql
--
-- Con Aplicar=0 solo cuenta; con Aplicar=1 hace el cambio.

-- Notificacion tiene índices filtrados: modificarla exige QUOTED_IDENTIFIER y ANSI_NULLS activos (error 1934),
-- y sqlcmd deja QUOTED_IDENTIFIER apagado si no se le pasa -I.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET XACT_ABORT ON;
SET NOCOUNT ON;

DECLARE @Desde datetimeoffset(0) = CAST(N'$(Desde)' AS datetimeoffset(0));
DECLARE @Hasta datetimeoffset(0) = CAST(N'$(Hasta)' AS datetimeoffset(0));
DECLARE @Aplicar bit = $(Aplicar);

-- Estado 4 = Descartada, 1 = Pendiente (EstadoNotificacion). Las descartadas porque la cita ya no estaba
-- agendada no se reenvían.
SELECT Tipo, COUNT(*) AS Cantidad
FROM dbo.Notificacion
WHERE Estado = 4 AND CreadaEn >= @Desde AND CreadaEn < @Hasta
  AND ISNULL(UltimoError, N'') <> N'La cita ya no está agendada.'
GROUP BY Tipo;

IF @Aplicar = 1
BEGIN
    BEGIN TRANSACTION;
    UPDATE dbo.Notificacion
    SET Estado = 1, Intentos = 0, ProximoIntentoEn = SYSDATETIMEOFFSET()
    WHERE Estado = 4 AND CreadaEn >= @Desde AND CreadaEn < @Hasta
      AND ISNULL(UltimoError, N'') <> N'La cita ya no está agendada.';
    PRINT CONCAT(@@ROWCOUNT, N' notificaciones reencoladas.');
    COMMIT;
END
