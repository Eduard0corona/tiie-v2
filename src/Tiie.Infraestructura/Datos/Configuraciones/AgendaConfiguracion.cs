using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tiie.Dominio.Agenda;
using Tiie.Infraestructura.Identidad;

namespace Tiie.Infraestructura.Datos.Configuraciones;

internal sealed class OfertaConfiguracion : IEntityTypeConfiguration<Oferta>
{
    public void Configure(EntityTypeBuilder<Oferta> b)
    {
        b.ToTable(nameof(Oferta), t => t.HasCheckConstraint(
            "CK_Oferta_DiasVentana",
            $"[DiasVentana] BETWEEN {Oferta.DiasVentanaMinimos} AND {Oferta.DiasVentanaMaximos}"));
        b.HasKey(o => o.OfertaId);
        b.HasIndex(o => new { o.ModuloId, o.TipoTramiteId }).IsUnique().HasDatabaseName("UX_Oferta_ModuloTipo");
        b.HasIndex(o => o.TipoTramiteId).HasDatabaseName("IX_Oferta_TipoTramite");
        b.Property(o => o.RowVersion).IsRowVersion();
        b.HasOne(o => o.Modulo).WithMany(m => m.Ofertas).HasForeignKey(o => o.ModuloId);
        b.HasOne(o => o.TipoTramite).WithMany(t => t.Ofertas).HasForeignKey(o => o.TipoTramiteId);
    }
}

internal sealed class PlantillaHorarioConfiguracion : IEntityTypeConfiguration<PlantillaHorario>
{
    public void Configure(EntityTypeBuilder<PlantillaHorario> b)
    {
        b.ToTable(nameof(PlantillaHorario), t =>
        {
            t.HasCheckConstraint("CK_PlantillaHorario_DiaSemana", "[DiaSemana] BETWEEN 0 AND 6");
            t.HasCheckConstraint("CK_PlantillaHorario_Turnos", "[Turnos] > 0");
        });
        b.HasKey(p => p.PlantillaHorarioId);
        b.HasIndex(p => new { p.OfertaId, p.DiaSemana, p.Hora }).IsUnique().HasDatabaseName("UX_Plantilla_OfertaDiaHora");
        b.HasOne(p => p.Oferta).WithMany(o => o.Plantilla).HasForeignKey(p => p.OfertaId);
    }
}

internal sealed class BloqueoConfiguracion : IEntityTypeConfiguration<Bloqueo>
{
    public void Configure(EntityTypeBuilder<Bloqueo> b)
    {
        b.ToTable(nameof(Bloqueo), t =>
        {
            t.HasCheckConstraint("CK_Bloqueo_Rango", "[Desde] <= [Hasta]");
            t.HasCheckConstraint("CK_Bloqueo_Alcance", "[ModuloId] IS NULL OR [OfertaId] IS NULL");
        });
        b.HasKey(x => x.BloqueoId);
        b.Property(x => x.Motivo).HasMaxLength(200);
        b.HasIndex(x => new { x.Desde, x.Hasta }).HasDatabaseName("IX_Bloqueo_Rango");
        b.HasOne(x => x.Modulo).WithMany().HasForeignKey(x => x.ModuloId);
        b.HasOne(x => x.Oferta).WithMany().HasForeignKey(x => x.OfertaId);
        b.HasOne<Usuario>().WithMany().HasForeignKey(x => x.CreadoPor);
    }
}

internal sealed class CupoConfiguracion : IEntityTypeConfiguration<Cupo>
{
    public void Configure(EntityTypeBuilder<Cupo> b)
    {
        b.ToTable(nameof(Cupo), t =>
        {
            t.HasCheckConstraint("CK_Cupo_Capacidad", "[Capacidad] > 0");
            // RN-02: la base de datos protege contra la sobreventa aunque el código falle.
            t.HasCheckConstraint("CK_Cupo_Ocupados", "[Ocupados] BETWEEN 0 AND [Capacidad]");
        });
        b.HasKey(c => c.CupoId);
        b.Property(c => c.Ocupados).HasDefaultValue((short)0);
        b.Property(c => c.Cerrado).HasDefaultValue(false);
        b.Property(c => c.RowVersion).IsRowVersion();
        b.HasIndex(c => new { c.OfertaId, c.Fecha, c.Hora })
            .IsUnique()
            .HasDatabaseName("UX_Cupo_OfertaFechaHora")
            .IncludeProperties(c => new { c.Capacidad, c.Ocupados, c.Cerrado });
        b.HasIndex(c => c.Fecha)
            .HasDatabaseName("IX_Cupo_Fecha")
            .IncludeProperties(c => new { c.OfertaId, c.Hora });
        b.HasOne(c => c.Oferta).WithMany(o => o.Cupos).HasForeignKey(c => c.OfertaId);
    }
}

internal sealed class HistoricoCitaConfiguracion : IEntityTypeConfiguration<HistoricoCita>
{
    public void Configure(EntityTypeBuilder<HistoricoCita> b)
    {
        b.ToTable(nameof(HistoricoCita));
        b.HasKey(h => new { h.Fecha, h.OfertaId });
        b.HasIndex(h => h.OfertaId).HasDatabaseName("IX_HistoricoCita_Oferta");
        b.HasOne(h => h.Oferta).WithMany().HasForeignKey(h => h.OfertaId);
    }
}
