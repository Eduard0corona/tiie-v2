using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tiie.Infraestructura.Datos.Migraciones
{
    /// <summary>
    /// Índices para la búsqueda y el detalle del personal, reportes y bitácora, y TokenProtegido en varchar
    /// (docs/18-analisis-de-base-de-datos.md). El cambio a varchar no pierde datos: el token protegido es
    /// Base64url, solo ASCII. Compatible con la versión anterior del código.
    /// </summary>
    public partial class OptimizacionConsultas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Cita_ActualizadaPor",
                schema: "dbo",
                table: "Cita");

            migrationBuilder.DropIndex(
                name: "IX_Cita_CitaOrigen",
                schema: "dbo",
                table: "Cita");

            migrationBuilder.DropIndex(
                name: "IX_Cita_CreadaPor",
                schema: "dbo",
                table: "Cita");

            migrationBuilder.DropIndex(
                name: "IX_Cita_Cupo",
                schema: "dbo",
                table: "Cita");

            migrationBuilder.AlterColumn<string>(
                name: "TokenProtegido",
                schema: "dbo",
                table: "Cita",
                type: "varchar(600)",
                unicode: false,
                maxLength: 600,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(600)",
                oldMaxLength: 600);

            migrationBuilder.CreateIndex(
                name: "IX_Cita_ActualizadaPor",
                schema: "dbo",
                table: "Cita",
                column: "ActualizadaPor",
                filter: "[ActualizadaPor] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Cita_CitaOrigen",
                schema: "dbo",
                table: "Cita",
                column: "CitaOrigenId",
                filter: "[CitaOrigenId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Cita_CreadaPor",
                schema: "dbo",
                table: "Cita",
                column: "CreadaPor",
                filter: "[CreadaPor] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Cita_Cupo",
                schema: "dbo",
                table: "Cita",
                column: "CupoId")
                .Annotation("SqlServer:Include", new[] { "Estado", "SolicitanteId", "Folio", "Origen" });

            migrationBuilder.CreateIndex(
                name: "IX_Auditoria_Entidad",
                schema: "dbo",
                table: "Auditoria",
                columns: new[] { "Entidad", "EntidadId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Cita_ActualizadaPor",
                schema: "dbo",
                table: "Cita");

            migrationBuilder.DropIndex(
                name: "IX_Cita_CitaOrigen",
                schema: "dbo",
                table: "Cita");

            migrationBuilder.DropIndex(
                name: "IX_Cita_CreadaPor",
                schema: "dbo",
                table: "Cita");

            migrationBuilder.DropIndex(
                name: "IX_Cita_Cupo",
                schema: "dbo",
                table: "Cita");

            migrationBuilder.DropIndex(
                name: "IX_Auditoria_Entidad",
                schema: "dbo",
                table: "Auditoria");

            migrationBuilder.AlterColumn<string>(
                name: "TokenProtegido",
                schema: "dbo",
                table: "Cita",
                type: "nvarchar(600)",
                maxLength: 600,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(600)",
                oldUnicode: false,
                oldMaxLength: 600);

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
        }
    }
}
