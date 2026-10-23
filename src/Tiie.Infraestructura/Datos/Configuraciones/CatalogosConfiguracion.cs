using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tiie.Dominio.Catalogos;

namespace Tiie.Infraestructura.Datos.Configuraciones;

internal sealed class EntidadConfiguracion : IEntityTypeConfiguration<Entidad>
{
    public void Configure(EntityTypeBuilder<Entidad> b)
    {
        b.ToTable(nameof(Entidad));
        b.HasKey(e => e.EntidadId);
        b.Property(e => e.EntidadId).ValueGeneratedNever();
        b.Property(e => e.Nombre).HasMaxLength(60);
    }
}

internal sealed class MunicipioConfiguracion : IEntityTypeConfiguration<Municipio>
{
    public void Configure(EntityTypeBuilder<Municipio> b)
    {
        b.ToTable(nameof(Municipio));
        b.HasKey(m => m.MunicipioId);
        b.Property(m => m.MunicipioId).ValueGeneratedNever();
        b.Property(m => m.Nombre).HasMaxLength(100);
        b.HasOne(m => m.Entidad).WithMany(e => e.Municipios).HasForeignKey(m => m.EntidadId);
    }
}

internal sealed class NivelEscolarConfiguracion : IEntityTypeConfiguration<NivelEscolar>
{
    public void Configure(EntityTypeBuilder<NivelEscolar> b)
    {
        b.ToTable(nameof(NivelEscolar));
        b.HasKey(n => n.NivelEscolarId);
        b.Property(n => n.NivelEscolarId).ValueGeneratedNever();
        b.Property(n => n.Nombre).HasMaxLength(80);

        // Catálogo fijo: se siembra por migración en todos los entornos.
        b.HasData(
            new NivelEscolar { NivelEscolarId = NivelEscolar.Basico, Nombre = "Básico (preescolar, primaria y secundaria)", Orden = 1 },
            new NivelEscolar { NivelEscolarId = NivelEscolar.MedioSuperiorYSuperior, Nombre = "Medio superior y superior", Orden = 2 });
    }
}

internal sealed class ModuloConfiguracion : IEntityTypeConfiguration<Modulo>
{
    public void Configure(EntityTypeBuilder<Modulo> b)
    {
        b.ToTable(nameof(Modulo));
        b.HasKey(m => m.ModuloId);
        b.Property(m => m.Nombre).HasMaxLength(120);
        b.Property(m => m.Calle).HasMaxLength(100);
        b.Property(m => m.NumeroExterior).HasMaxLength(15);
        b.Property(m => m.NumeroInterior).HasMaxLength(15);
        b.Property(m => m.Colonia).HasMaxLength(80);
        b.Property(m => m.CodigoPostal).HasMaxLength(5).IsFixedLength().IsUnicode(false);
        b.Property(m => m.Localidad).HasMaxLength(80);
        b.Property(m => m.Telefono).HasMaxLength(10).IsUnicode(false);
        b.Property(m => m.Correo).HasMaxLength(150);
        b.Property(m => m.HorarioAtencion).HasMaxLength(255);
        b.Property(m => m.Latitud).HasPrecision(9, 6);
        b.Property(m => m.Longitud).HasPrecision(9, 6);
        b.Property(m => m.RowVersion).IsRowVersion();
        b.HasOne(m => m.Municipio).WithMany(m => m.Modulos).HasForeignKey(m => m.MunicipioId);
    }
}

internal sealed class TipoTramiteConfiguracion : IEntityTypeConfiguration<TipoTramite>
{
    public void Configure(EntityTypeBuilder<TipoTramite> b)
    {
        b.ToTable(nameof(TipoTramite));
        b.HasKey(t => t.TipoTramiteId);
        b.Property(t => t.Nombre).HasMaxLength(120);
        b.HasIndex(t => t.Nombre).IsUnique().HasDatabaseName("UX_TipoTramite_Nombre");
        b.Property(t => t.Descripcion).HasMaxLength(500);
        b.Property(t => t.Costo).HasPrecision(10, 2);
        b.Property(t => t.RowVersion).IsRowVersion();
    }
}

internal sealed class RequisitoConfiguracion : IEntityTypeConfiguration<Requisito>
{
    public void Configure(EntityTypeBuilder<Requisito> b)
    {
        b.ToTable(nameof(Requisito));
        b.HasKey(r => r.RequisitoId);
        b.HasOne(r => r.TipoTramite).WithMany(t => t.Requisitos).HasForeignKey(r => r.TipoTramiteId);
        b.HasOne(r => r.NivelEscolar).WithMany().HasForeignKey(r => r.NivelEscolarId);
    }
}
