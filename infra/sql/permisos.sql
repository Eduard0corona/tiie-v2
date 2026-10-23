-- Usuarios de Azure SQL para el sitio y el pipeline (docs/13-despliegue-y-operacion.md#identidades-y-permisos).
-- Lo ejecuta un miembro del grupo administrador de Entra, una vez por entorno, conectado a sqldb-tiie:
--
--   sqlcmd -S sql-tiie-qa.database.windows.net -d sqldb-tiie --authentication-method ActiveDirectoryDefault ^
--     -v Sitio="id-tiie-qa" Pipeline="<nombre del service principal de despliegue>" -i infra/sql/permisos.sql
--
-- Es seguro repetirlo. Ejecútalo antes de la primera migración (el pipeline necesita su usuario) y otra vez
-- después: los permisos sobre SecuenciaFolio y Auditoria necesitan que esos objetos existan.

SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- Sitio: identidad asignada por el usuario que comparten el sitio y la ranura staging. Lee y escribe datos,
-- pero no cambia el esquema.
IF DATABASE_PRINCIPAL_ID(N'$(Sitio)') IS NULL
    CREATE USER [$(Sitio)] FROM EXTERNAL PROVIDER;
ALTER ROLE db_datareader ADD MEMBER [$(Sitio)];
ALTER ROLE db_datawriter ADD MEMBER [$(Sitio)];
-- Con EXEC, el lote se compila aunque los objetos aún no existan.
-- El folio sale de la secuencia (RN-10): NEXT VALUE FOR exige UPDATE sobre ella.
IF OBJECT_ID(N'dbo.SecuenciaFolio', N'SO') IS NOT NULL
    EXEC (N'GRANT UPDATE ON OBJECT::dbo.SecuenciaFolio TO [$(Sitio)];');
ELSE
    PRINT N'Falta dbo.SecuenciaFolio: vuelve a ejecutar el script después de la primera migración.';
-- La bitácora solo crece: el sitio inserta y consulta, nunca modifica ni borra.
IF OBJECT_ID(N'dbo.Auditoria', N'U') IS NOT NULL
    EXEC (N'DENY UPDATE, DELETE ON OBJECT::dbo.Auditoria TO [$(Sitio)];');
ELSE
    PRINT N'Falta dbo.Auditoria: vuelve a ejecutar el script después de la primera migración.';

-- Pipeline: aplica las migraciones con efbundle antes de desplegar el código.
IF DATABASE_PRINCIPAL_ID(N'$(Pipeline)') IS NULL
    CREATE USER [$(Pipeline)] FROM EXTERNAL PROVIDER;
ALTER ROLE db_ddladmin ADD MEMBER [$(Pipeline)];
ALTER ROLE db_datareader ADD MEMBER [$(Pipeline)];
ALTER ROLE db_datawriter ADD MEMBER [$(Pipeline)];

COMMIT;

SELECT p.name AS Usuario, r.name AS Rol
FROM sys.database_role_members m
JOIN sys.database_principals r ON r.principal_id = m.role_principal_id
JOIN sys.database_principals p ON p.principal_id = m.member_principal_id
WHERE p.name IN (N'$(Sitio)', N'$(Pipeline)')
ORDER BY p.name, r.name;
