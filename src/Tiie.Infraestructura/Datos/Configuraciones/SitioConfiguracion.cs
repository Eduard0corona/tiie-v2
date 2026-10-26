using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tiie.Dominio.Sitio;

namespace Tiie.Infraestructura.Datos.Configuraciones;

internal sealed class ContenidoPortadaConfiguracion : IEntityTypeConfiguration<ContenidoPortada>
{
    public void Configure(EntityTypeBuilder<ContenidoPortada> b)
    {
        b.ToTable(nameof(ContenidoPortada), t =>
        {
            t.HasCheckConstraint("CK_ContenidoPortada_Unico", $"[ContenidoPortadaId] = {ContenidoPortada.Unico}");
            t.HasCheckConstraint("CK_ContenidoPortada_AvisoTono", "[AvisoTono] BETWEEN 1 AND 2");
            t.HasCheckConstraint("CK_ContenidoPortada_AvisoRango",
                "[AvisoDesde] IS NULL OR [AvisoHasta] IS NULL OR [AvisoDesde] <= [AvisoHasta]");
        });
        b.HasKey(c => c.ContenidoPortadaId);
        b.Property(c => c.ContenidoPortadaId).ValueGeneratedNever();
        b.Property(c => c.Etiqueta).HasMaxLength(80);
        b.Property(c => c.Titulo).HasMaxLength(120);
        b.Property(c => c.TituloDestacado).HasMaxLength(120);
        b.Property(c => c.Descripcion).HasMaxLength(300);
        b.Property(c => c.AvisoTexto).HasMaxLength(300);
        b.Property(c => c.RowVersion).IsRowVersion();
        b.HasMany(c => c.Preguntas).WithOne().HasForeignKey(p => p.ContenidoPortadaId);
    }
}

internal sealed class PreguntaFrecuenteConfiguracion : IEntityTypeConfiguration<PreguntaFrecuente>
{
    public void Configure(EntityTypeBuilder<PreguntaFrecuente> b)
    {
        b.ToTable(nameof(PreguntaFrecuente));
        b.HasKey(p => p.PreguntaFrecuenteId);
        b.Property(p => p.Pregunta).HasMaxLength(200);
        b.Property(p => p.Respuesta).HasMaxLength(1500);
        b.HasIndex(p => p.ContenidoPortadaId).HasDatabaseName("IX_PreguntaFrecuente_Contenido");
    }
}
