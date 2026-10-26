using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tiie.Infraestructura.Datos.Migraciones
{
    /// <summary>
    /// Contenido editable de la portada (docs/16#contenido-editable). Solo agrega tablas; sin filas, el sitio
    /// muestra el contenido predeterminado, así que el código anterior y el nuevo conviven.
    /// </summary>
    public partial class AgregaContenidoPortada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContenidoPortada",
                schema: "dbo",
                columns: table => new
                {
                    ContenidoPortadaId = table.Column<byte>(type: "tinyint", nullable: false),
                    Etiqueta = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Titulo = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    TituloDestacado = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Descripcion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    AvisoTexto = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    AvisoTono = table.Column<byte>(type: "tinyint", nullable: false),
                    AvisoDesde = table.Column<DateOnly>(type: "date", nullable: true),
                    AvisoHasta = table.Column<DateOnly>(type: "date", nullable: true),
                    CreadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    ModificadoEn = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContenidoPortada", x => x.ContenidoPortadaId);
                    table.CheckConstraint("CK_ContenidoPortada_AvisoRango", "[AvisoDesde] IS NULL OR [AvisoHasta] IS NULL OR [AvisoDesde] <= [AvisoHasta]");
                    table.CheckConstraint("CK_ContenidoPortada_AvisoTono", "[AvisoTono] BETWEEN 1 AND 2");
                    table.CheckConstraint("CK_ContenidoPortada_Unico", "[ContenidoPortadaId] = 1");
                });

            migrationBuilder.CreateTable(
                name: "PreguntaFrecuente",
                schema: "dbo",
                columns: table => new
                {
                    PreguntaFrecuenteId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContenidoPortadaId = table.Column<byte>(type: "tinyint", nullable: false),
                    Pregunta = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Respuesta = table.Column<string>(type: "nvarchar(1500)", maxLength: 1500, nullable: false),
                    Orden = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreguntaFrecuente", x => x.PreguntaFrecuenteId);
                    table.ForeignKey(
                        name: "FK_PreguntaFrecuente_ContenidoPortada_ContenidoPortadaId",
                        column: x => x.ContenidoPortadaId,
                        principalSchema: "dbo",
                        principalTable: "ContenidoPortada",
                        principalColumn: "ContenidoPortadaId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PreguntaFrecuente_Contenido",
                schema: "dbo",
                table: "PreguntaFrecuente",
                column: "ContenidoPortadaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PreguntaFrecuente",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ContenidoPortada",
                schema: "dbo");
        }
    }
}
