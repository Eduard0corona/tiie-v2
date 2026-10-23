-- Esquema SUPUESTO de la base de 2016 (catálogo TIEDev), reconstruido a partir de docs/14-migracion-de-datos.md.
-- No es el script real: antes del primer ensayo, compáralo con el script de la base de producción y ajusta
-- Origen/EsquemaOrigen.cs y Origen/LectorOrigen.cs. `analizar` verifica que estas tablas y columnas existan.
--
-- Las pruebas de integración crean con este script una base de origen de ejemplo.

CREATE TABLE dbo.Entidad (
    EntidadId int NOT NULL PRIMARY KEY,
    Entidad varchar(60) NOT NULL,
    Estatus bit NOT NULL
);

CREATE TABLE dbo.Municipio (
    MunicipioId int NOT NULL PRIMARY KEY,
    EntidadId int NOT NULL,
    Municipio varchar(80) NOT NULL
);

CREATE TABLE dbo.Modulo (
    ModuloId int NOT NULL PRIMARY KEY,
    EntidadId int NULL,
    MunicipioId int NOT NULL,
    Nombre varchar(120) NOT NULL,
    Calle varchar(100) NULL,
    NumExt varchar(15) NULL,
    NumInt varchar(15) NULL,
    Colonia varchar(80) NULL,
    CP varchar(5) NULL,
    Poblacion varchar(80) NULL,
    Telefono varchar(20) NULL,
    Email varchar(150) NULL,
    Estatus bit NOT NULL
);

-- Un trámite del sistema anterior es un tipo de trámite atendido en un módulo (la «oferta» de la versión 2).
CREATE TABLE dbo.Tramite (
    TramiteId int NOT NULL PRIMARY KEY,
    ModuloId int NULL,
    Nombre varchar(150) NOT NULL,
    Detalle varchar(max) NULL,
    Costo decimal(10, 2) NULL,
    Notas varchar(max) NULL,
    Requisitos varchar(max) NULL,
    Estatus bit NOT NULL
);

CREATE TABLE dbo.Horario (
    HorarioId int NOT NULL PRIMARY KEY,
    TramiteId int NOT NULL,
    Hora time(0) NOT NULL,
    Turnos int NOT NULL,
    Estatus bit NOT NULL
);

CREATE TABLE dbo.Cita (
    CitaId int NOT NULL PRIMARY KEY,
    Folio varchar(20) NOT NULL,
    CURP varchar(20) NULL,
    Nombre varchar(80) NOT NULL,
    ApellidoPaterno varchar(80) NOT NULL,
    ApellidoMaterno varchar(80) NULL,
    Celular varchar(20) NULL,
    Email varchar(150) NULL,
    FechaCita date NOT NULL,
    Estatus varchar(20) NOT NULL,
    FechaRegistro datetime NULL
);

CREATE TABLE dbo.HorarioCita (
    HorarioCitaId int NOT NULL PRIMARY KEY,
    CitaId int NOT NULL,
    HorarioId int NOT NULL
);

CREATE TABLE dbo.Usuario (
    UsuarioId int NOT NULL PRIMARY KEY,
    Usuario varchar(50) NOT NULL,
    Nombre varchar(80) NULL,
    ApellidoPaterno varchar(80) NULL,
    ApellidoMaterno varchar(80) NULL,
    Email varchar(150) NULL,
    Tipo int NOT NULL,
    ModuloId int NULL,
    Estatus bit NOT NULL
);
