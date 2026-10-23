using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Tiie.Infraestructura.Datos.Migraciones
{
    /// <inheritdoc />
    public partial class ModeloInicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.CreateSequence(
                name: "SecuenciaFolio",
                schema: "dbo",
                startValue: 1000000L);

            migrationBuilder.CreateTable(
                name: "EjecucionProceso",
                schema: "dbo",
                columns: table => new
                {
                    Nombre = table.Column<string>(type: "varchar(60)", unicode: false, maxLength: 60, nullable: false),
                    UltimoInicio = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: true),
                    UltimoFin = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: true),
                    UltimoResultado = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    Detalle = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EjecucionProceso", x => x.Nombre);
                });

            migrationBuilder.CreateTable(
                name: "Entidad",
                schema: "dbo",
                columns: table => new
                {
                    EntidadId = table.Column<byte>(type: "tinyint", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Entidad", x => x.EntidadId);
                });

            migrationBuilder.CreateTable(
                name: "NivelEscolar",
                schema: "dbo",
                columns: table => new
                {
                    NivelEscolarId = table.Column<byte>(type: "tinyint", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Orden = table.Column<byte>(type: "tinyint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NivelEscolar", x => x.NivelEscolarId);
                });

            migrationBuilder.CreateTable(
                name: "Rol",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rol", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Solicitante",
                schema: "dbo",
                columns: table => new
                {
                    SolicitanteId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Curp = table.Column<string>(type: "char(18)", unicode: false, fixedLength: true, maxLength: 18, nullable: true),
                    Nombre = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ApellidoPaterno = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ApellidoMaterno = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    FechaNacimiento = table.Column<DateOnly>(type: "date", nullable: true),
                    Sexo = table.Column<string>(type: "char(1)", nullable: true),
                    Celular = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true),
                    Correo = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    CreadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    ModificadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: true),
                    AnonimizadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Solicitante", x => x.SolicitanteId);
                    table.CheckConstraint("CK_Solicitante_Sexo", "[Sexo] IN ('H', 'M', 'X')");
                });

            migrationBuilder.CreateTable(
                name: "TipoTramite",
                schema: "dbo",
                columns: table => new
                {
                    TipoTramiteId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Costo = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    Notas = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    CreadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    ModificadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TipoTramite", x => x.TipoTramiteId);
                });

            migrationBuilder.CreateTable(
                name: "Usuario",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ApellidoPaterno = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ApellidoMaterno = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    CreadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    UltimoAccesoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuario", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Municipio",
                schema: "dbo",
                columns: table => new
                {
                    MunicipioId = table.Column<short>(type: "smallint", nullable: false),
                    EntidadId = table.Column<byte>(type: "tinyint", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Municipio", x => x.MunicipioId);
                    table.ForeignKey(
                        name: "FK_Municipio_Entidad_EntidadId",
                        column: x => x.EntidadId,
                        principalSchema: "dbo",
                        principalTable: "Entidad",
                        principalColumn: "EntidadId");
                });

            migrationBuilder.CreateTable(
                name: "RolClaim",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolClaim", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RolClaim_Rol_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "dbo",
                        principalTable: "Rol",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Requisito",
                schema: "dbo",
                columns: table => new
                {
                    RequisitoId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TipoTramiteId = table.Column<int>(type: "int", nullable: false),
                    NivelEscolarId = table.Column<byte>(type: "tinyint", nullable: true),
                    Texto = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Orden = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Requisito", x => x.RequisitoId);
                    table.ForeignKey(
                        name: "FK_Requisito_NivelEscolar_NivelEscolarId",
                        column: x => x.NivelEscolarId,
                        principalSchema: "dbo",
                        principalTable: "NivelEscolar",
                        principalColumn: "NivelEscolarId");
                    table.ForeignKey(
                        name: "FK_Requisito_TipoTramite_TipoTramiteId",
                        column: x => x.TipoTramiteId,
                        principalSchema: "dbo",
                        principalTable: "TipoTramite",
                        principalColumn: "TipoTramiteId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Auditoria",
                schema: "dbo",
                columns: table => new
                {
                    AuditoriaId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Fecha = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    UsuarioId = table.Column<int>(type: "int", nullable: true),
                    Accion = table.Column<string>(type: "varchar(60)", unicode: false, maxLength: 60, nullable: false),
                    Entidad = table.Column<string>(type: "varchar(60)", unicode: false, maxLength: 60, nullable: false),
                    EntidadId = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: true),
                    Cambios = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Ip = table.Column<string>(type: "varchar(45)", unicode: false, maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Auditoria", x => x.AuditoriaId);
                    table.ForeignKey(
                        name: "FK_Auditoria_Usuario_UsuarioId",
                        column: x => x.UsuarioId,
                        principalSchema: "dbo",
                        principalTable: "Usuario",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UsuarioClaim",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsuarioClaim", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UsuarioClaim_Usuario_UserId",
                        column: x => x.UserId,
                        principalSchema: "dbo",
                        principalTable: "Usuario",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UsuarioLogin",
                schema: "dbo",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsuarioLogin", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_UsuarioLogin_Usuario_UserId",
                        column: x => x.UserId,
                        principalSchema: "dbo",
                        principalTable: "Usuario",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UsuarioRol",
                schema: "dbo",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    RoleId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsuarioRol", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_UsuarioRol_Rol_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "dbo",
                        principalTable: "Rol",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UsuarioRol_Usuario_UserId",
                        column: x => x.UserId,
                        principalSchema: "dbo",
                        principalTable: "Usuario",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UsuarioToken",
                schema: "dbo",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsuarioToken", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_UsuarioToken_Usuario_UserId",
                        column: x => x.UserId,
                        principalSchema: "dbo",
                        principalTable: "Usuario",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Modulo",
                schema: "dbo",
                columns: table => new
                {
                    ModuloId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MunicipioId = table.Column<short>(type: "smallint", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Calle = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NumeroExterior = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    NumeroInterior = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    Colonia = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    CodigoPostal = table.Column<string>(type: "char(5)", unicode: false, fixedLength: true, maxLength: 5, nullable: false),
                    Localidad = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Telefono = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true),
                    Correo = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    HorarioAtencion = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Latitud = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    Longitud = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    IdAnterior = table.Column<int>(type: "int", nullable: true),
                    CreadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    ModificadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Modulo", x => x.ModuloId);
                    table.ForeignKey(
                        name: "FK_Modulo_Municipio_MunicipioId",
                        column: x => x.MunicipioId,
                        principalSchema: "dbo",
                        principalTable: "Municipio",
                        principalColumn: "MunicipioId");
                });

            migrationBuilder.CreateTable(
                name: "Oferta",
                schema: "dbo",
                columns: table => new
                {
                    OfertaId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ModuloId = table.Column<int>(type: "int", nullable: false),
                    TipoTramiteId = table.Column<int>(type: "int", nullable: false),
                    DiasVentana = table.Column<short>(type: "smallint", nullable: false),
                    NotasSede = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    IdAnterior = table.Column<int>(type: "int", nullable: true),
                    CreadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    ModificadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Oferta", x => x.OfertaId);
                    table.CheckConstraint("CK_Oferta_DiasVentana", "[DiasVentana] BETWEEN 1 AND 120");
                    table.ForeignKey(
                        name: "FK_Oferta_Modulo_ModuloId",
                        column: x => x.ModuloId,
                        principalSchema: "dbo",
                        principalTable: "Modulo",
                        principalColumn: "ModuloId");
                    table.ForeignKey(
                        name: "FK_Oferta_TipoTramite_TipoTramiteId",
                        column: x => x.TipoTramiteId,
                        principalSchema: "dbo",
                        principalTable: "TipoTramite",
                        principalColumn: "TipoTramiteId");
                });

            migrationBuilder.CreateTable(
                name: "UsuarioModulo",
                schema: "dbo",
                columns: table => new
                {
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    ModuloId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsuarioModulo", x => new { x.UsuarioId, x.ModuloId });
                    table.ForeignKey(
                        name: "FK_UsuarioModulo_Modulo_ModuloId",
                        column: x => x.ModuloId,
                        principalSchema: "dbo",
                        principalTable: "Modulo",
                        principalColumn: "ModuloId");
                    table.ForeignKey(
                        name: "FK_UsuarioModulo_Usuario_UsuarioId",
                        column: x => x.UsuarioId,
                        principalSchema: "dbo",
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Bloqueo",
                schema: "dbo",
                columns: table => new
                {
                    BloqueoId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Desde = table.Column<DateOnly>(type: "date", nullable: false),
                    Hasta = table.Column<DateOnly>(type: "date", nullable: false),
                    ModuloId = table.Column<int>(type: "int", nullable: true),
                    OfertaId = table.Column<int>(type: "int", nullable: true),
                    Motivo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreadoPor = table.Column<int>(type: "int", nullable: false),
                    CreadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bloqueo", x => x.BloqueoId);
                    table.CheckConstraint("CK_Bloqueo_Alcance", "[ModuloId] IS NULL OR [OfertaId] IS NULL");
                    table.CheckConstraint("CK_Bloqueo_Rango", "[Desde] <= [Hasta]");
                    table.ForeignKey(
                        name: "FK_Bloqueo_Modulo_ModuloId",
                        column: x => x.ModuloId,
                        principalSchema: "dbo",
                        principalTable: "Modulo",
                        principalColumn: "ModuloId");
                    table.ForeignKey(
                        name: "FK_Bloqueo_Oferta_OfertaId",
                        column: x => x.OfertaId,
                        principalSchema: "dbo",
                        principalTable: "Oferta",
                        principalColumn: "OfertaId");
                    table.ForeignKey(
                        name: "FK_Bloqueo_Usuario_CreadoPor",
                        column: x => x.CreadoPor,
                        principalSchema: "dbo",
                        principalTable: "Usuario",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Cupo",
                schema: "dbo",
                columns: table => new
                {
                    CupoId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OfertaId = table.Column<int>(type: "int", nullable: false),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    Hora = table.Column<TimeOnly>(type: "time(0)", precision: 0, nullable: false),
                    Capacidad = table.Column<short>(type: "smallint", nullable: false),
                    Ocupados = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    Cerrado = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cupo", x => x.CupoId);
                    table.CheckConstraint("CK_Cupo_Capacidad", "[Capacidad] > 0");
                    table.CheckConstraint("CK_Cupo_Ocupados", "[Ocupados] BETWEEN 0 AND [Capacidad]");
                    table.ForeignKey(
                        name: "FK_Cupo_Oferta_OfertaId",
                        column: x => x.OfertaId,
                        principalSchema: "dbo",
                        principalTable: "Oferta",
                        principalColumn: "OfertaId");
                });

            migrationBuilder.CreateTable(
                name: "HistoricoCita",
                schema: "dbo",
                columns: table => new
                {
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    OfertaId = table.Column<int>(type: "int", nullable: false),
                    Total = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoricoCita", x => new { x.Fecha, x.OfertaId });
                    table.ForeignKey(
                        name: "FK_HistoricoCita_Oferta_OfertaId",
                        column: x => x.OfertaId,
                        principalSchema: "dbo",
                        principalTable: "Oferta",
                        principalColumn: "OfertaId");
                });

            migrationBuilder.CreateTable(
                name: "PlantillaHorario",
                schema: "dbo",
                columns: table => new
                {
                    PlantillaHorarioId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OfertaId = table.Column<int>(type: "int", nullable: false),
                    DiaSemana = table.Column<byte>(type: "tinyint", nullable: false),
                    Hora = table.Column<TimeOnly>(type: "time(0)", precision: 0, nullable: false),
                    Turnos = table.Column<byte>(type: "tinyint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlantillaHorario", x => x.PlantillaHorarioId);
                    table.CheckConstraint("CK_PlantillaHorario_DiaSemana", "[DiaSemana] BETWEEN 0 AND 6");
                    table.CheckConstraint("CK_PlantillaHorario_Turnos", "[Turnos] > 0");
                    table.ForeignKey(
                        name: "FK_PlantillaHorario_Oferta_OfertaId",
                        column: x => x.OfertaId,
                        principalSchema: "dbo",
                        principalTable: "Oferta",
                        principalColumn: "OfertaId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Cita",
                schema: "dbo",
                columns: table => new
                {
                    CitaId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Folio = table.Column<string>(type: "varchar(14)", unicode: false, maxLength: 14, nullable: false),
                    CupoId = table.Column<int>(type: "int", nullable: false),
                    SolicitanteId = table.Column<int>(type: "int", nullable: false),
                    Estado = table.Column<byte>(type: "tinyint", nullable: false),
                    Origen = table.Column<byte>(type: "tinyint", nullable: false),
                    TokenHash = table.Column<byte[]>(type: "binary(32)", fixedLength: true, maxLength: 32, nullable: false),
                    TokenProtegido = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: false),
                    TokenVenceEn = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    ClaveIdempotencia = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VersionAvisoPrivacidad = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    AvisoAceptadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: true),
                    CitaOrigenId = table.Column<int>(type: "int", nullable: true),
                    MotivoCambio = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    FolioAnterior = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    CreadaEn = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    CreadaPor = table.Column<int>(type: "int", nullable: true),
                    ActualizadaEn = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: true),
                    ActualizadaPor = table.Column<int>(type: "int", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cita", x => x.CitaId);
                    table.CheckConstraint("CK_Cita_Estado", "[Estado] BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_Cita_Origen", "[Origen] BETWEEN 1 AND 3");
                    table.ForeignKey(
                        name: "FK_Cita_Cita_CitaOrigenId",
                        column: x => x.CitaOrigenId,
                        principalSchema: "dbo",
                        principalTable: "Cita",
                        principalColumn: "CitaId");
                    table.ForeignKey(
                        name: "FK_Cita_Cupo_CupoId",
                        column: x => x.CupoId,
                        principalSchema: "dbo",
                        principalTable: "Cupo",
                        principalColumn: "CupoId");
                    table.ForeignKey(
                        name: "FK_Cita_Solicitante_SolicitanteId",
                        column: x => x.SolicitanteId,
                        principalSchema: "dbo",
                        principalTable: "Solicitante",
                        principalColumn: "SolicitanteId");
                    table.ForeignKey(
                        name: "FK_Cita_Usuario_ActualizadaPor",
                        column: x => x.ActualizadaPor,
                        principalSchema: "dbo",
                        principalTable: "Usuario",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Cita_Usuario_CreadaPor",
                        column: x => x.CreadaPor,
                        principalSchema: "dbo",
                        principalTable: "Usuario",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Notificacion",
                schema: "dbo",
                columns: table => new
                {
                    NotificacionId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CitaId = table.Column<int>(type: "int", nullable: false),
                    Tipo = table.Column<byte>(type: "tinyint", nullable: false),
                    Canal = table.Column<byte>(type: "tinyint", nullable: false),
                    Destinatario = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Estado = table.Column<byte>(type: "tinyint", nullable: false),
                    Intentos = table.Column<byte>(type: "tinyint", nullable: false),
                    ProximoIntentoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    EnviadaEn = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: true),
                    UltimoError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreadaEn = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notificacion", x => x.NotificacionId);
                    table.CheckConstraint("CK_Notificacion_Canal", "[Canal] = 1");
                    table.CheckConstraint("CK_Notificacion_Estado", "[Estado] BETWEEN 1 AND 4");
                    table.CheckConstraint("CK_Notificacion_Tipo", "[Tipo] BETWEEN 1 AND 7");
                    table.ForeignKey(
                        name: "FK_Notificacion_Cita_CitaId",
                        column: x => x.CitaId,
                        principalSchema: "dbo",
                        principalTable: "Cita",
                        principalColumn: "CitaId");
                });

            migrationBuilder.InsertData(
                schema: "dbo",
                table: "NivelEscolar",
                columns: new[] { "NivelEscolarId", "Nombre", "Orden" },
                values: new object[,]
                {
                    { (byte)1, "Básico (preescolar, primaria y secundaria)", (byte)1 },
                    { (byte)2, "Medio superior y superior", (byte)2 }
                });

            migrationBuilder.InsertData(
                schema: "dbo",
                table: "Rol",
                columns: new[] { "Id", "ConcurrencyStamp", "Name", "NormalizedName" },
                values: new object[,]
                {
                    { 1, "4b0a5c39-2f0e-4c5e-9d0f-6f2f1d7c0a01", "Administrador", "ADMINISTRADOR" },
                    { 2, "4b0a5c39-2f0e-4c5e-9d0f-6f2f1d7c0a02", "Supervisor", "SUPERVISOR" },
                    { 3, "4b0a5c39-2f0e-4c5e-9d0f-6f2f1d7c0a03", "Capturista", "CAPTURISTA" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Auditoria_Fecha",
                schema: "dbo",
                table: "Auditoria",
                column: "Fecha");

            migrationBuilder.CreateIndex(
                name: "IX_Auditoria_Usuario",
                schema: "dbo",
                table: "Auditoria",
                columns: new[] { "UsuarioId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_Bloqueo_CreadoPor",
                schema: "dbo",
                table: "Bloqueo",
                column: "CreadoPor");

            migrationBuilder.CreateIndex(
                name: "IX_Bloqueo_ModuloId",
                schema: "dbo",
                table: "Bloqueo",
                column: "ModuloId");

            migrationBuilder.CreateIndex(
                name: "IX_Bloqueo_OfertaId",
                schema: "dbo",
                table: "Bloqueo",
                column: "OfertaId");

            migrationBuilder.CreateIndex(
                name: "IX_Bloqueo_Rango",
                schema: "dbo",
                table: "Bloqueo",
                columns: new[] { "Desde", "Hasta" });

            migrationBuilder.CreateIndex(
                name: "IX_Cita_ActualizadaPor",
                schema: "dbo",
                table: "Cita",
                column: "ActualizadaPor");

            migrationBuilder.CreateIndex(
                name: "IX_Cita_CitaOrigen",
                schema: "dbo",
                table: "Cita",
                column: "CitaOrigenId");

            migrationBuilder.CreateIndex(
                name: "IX_Cita_CreadaPor",
                schema: "dbo",
                table: "Cita",
                column: "CreadaPor");

            migrationBuilder.CreateIndex(
                name: "IX_Cita_Cupo",
                schema: "dbo",
                table: "Cita",
                column: "CupoId")
                .Annotation("SqlServer:Include", new[] { "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_Cita_FolioAnterior",
                schema: "dbo",
                table: "Cita",
                column: "FolioAnterior",
                filter: "[FolioAnterior] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Cita_Solicitante",
                schema: "dbo",
                table: "Cita",
                columns: new[] { "SolicitanteId", "Estado" });

            migrationBuilder.CreateIndex(
                name: "UX_Cita_ClaveIdempotencia",
                schema: "dbo",
                table: "Cita",
                column: "ClaveIdempotencia",
                unique: true,
                filter: "[ClaveIdempotencia] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Cita_Folio",
                schema: "dbo",
                table: "Cita",
                column: "Folio",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Cita_SolicitanteActiva",
                schema: "dbo",
                table: "Cita",
                column: "SolicitanteId",
                unique: true,
                filter: "[Estado] = 1");

            migrationBuilder.CreateIndex(
                name: "UX_Cita_TokenHash",
                schema: "dbo",
                table: "Cita",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cupo_Fecha",
                schema: "dbo",
                table: "Cupo",
                column: "Fecha")
                .Annotation("SqlServer:Include", new[] { "OfertaId", "Hora" });

            migrationBuilder.CreateIndex(
                name: "UX_Cupo_OfertaFechaHora",
                schema: "dbo",
                table: "Cupo",
                columns: new[] { "OfertaId", "Fecha", "Hora" },
                unique: true)
                .Annotation("SqlServer:Include", new[] { "Capacidad", "Ocupados", "Cerrado" });

            migrationBuilder.CreateIndex(
                name: "IX_HistoricoCita_Oferta",
                schema: "dbo",
                table: "HistoricoCita",
                column: "OfertaId");

            migrationBuilder.CreateIndex(
                name: "IX_Modulo_MunicipioId",
                schema: "dbo",
                table: "Modulo",
                column: "MunicipioId");

            migrationBuilder.CreateIndex(
                name: "IX_Municipio_EntidadId",
                schema: "dbo",
                table: "Municipio",
                column: "EntidadId");

            migrationBuilder.CreateIndex(
                name: "IX_Notificacion_Cita",
                schema: "dbo",
                table: "Notificacion",
                column: "CitaId");

            migrationBuilder.CreateIndex(
                name: "IX_Notificacion_Pendientes",
                schema: "dbo",
                table: "Notificacion",
                column: "ProximoIntentoEn",
                filter: "[Estado] IN (1, 3)");

            migrationBuilder.CreateIndex(
                name: "UX_Notificacion_Recordatorio",
                schema: "dbo",
                table: "Notificacion",
                columns: new[] { "CitaId", "Tipo" },
                unique: true,
                filter: "[Tipo] = 2");

            migrationBuilder.CreateIndex(
                name: "IX_Oferta_TipoTramite",
                schema: "dbo",
                table: "Oferta",
                column: "TipoTramiteId");

            migrationBuilder.CreateIndex(
                name: "UX_Oferta_ModuloTipo",
                schema: "dbo",
                table: "Oferta",
                columns: new[] { "ModuloId", "TipoTramiteId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Plantilla_OfertaDiaHora",
                schema: "dbo",
                table: "PlantillaHorario",
                columns: new[] { "OfertaId", "DiaSemana", "Hora" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Requisito_NivelEscolarId",
                schema: "dbo",
                table: "Requisito",
                column: "NivelEscolarId");

            migrationBuilder.CreateIndex(
                name: "IX_Requisito_TipoTramiteId",
                schema: "dbo",
                table: "Requisito",
                column: "TipoTramiteId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                schema: "dbo",
                table: "Rol",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RolClaim_RoleId",
                schema: "dbo",
                table: "RolClaim",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "UX_Solicitante_Curp",
                schema: "dbo",
                table: "Solicitante",
                column: "Curp",
                unique: true,
                filter: "[Curp] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_TipoTramite_Nombre",
                schema: "dbo",
                table: "TipoTramite",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                schema: "dbo",
                table: "Usuario",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Usuario_Correo",
                schema: "dbo",
                table: "Usuario",
                column: "NormalizedEmail",
                unique: true,
                filter: "[NormalizedEmail] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UsuarioClaim_UserId",
                schema: "dbo",
                table: "UsuarioClaim",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UsuarioLogin_UserId",
                schema: "dbo",
                table: "UsuarioLogin",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UsuarioModulo_Modulo",
                schema: "dbo",
                table: "UsuarioModulo",
                column: "ModuloId");

            migrationBuilder.CreateIndex(
                name: "IX_UsuarioRol_RoleId",
                schema: "dbo",
                table: "UsuarioRol",
                column: "RoleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Auditoria",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Bloqueo",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "EjecucionProceso",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "HistoricoCita",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Notificacion",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PlantillaHorario",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Requisito",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RolClaim",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "UsuarioClaim",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "UsuarioLogin",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "UsuarioModulo",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "UsuarioRol",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "UsuarioToken",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Cita",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "NivelEscolar",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Rol",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Cupo",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Solicitante",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Usuario",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Oferta",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Modulo",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TipoTramite",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Municipio",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Entidad",
                schema: "dbo");

            migrationBuilder.DropSequence(
                name: "SecuenciaFolio",
                schema: "dbo");
        }
    }
}
