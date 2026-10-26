using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tiie.Aplicacion.Seguridad;
using Tiie.Dominio.Bitacora;
using Tiie.Dominio.Catalogos;
using Tiie.Infraestructura.Identidad;

namespace Tiie.Infraestructura.Datos.Configuraciones;

internal sealed class UsuarioConfiguracion : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> b)
    {
        b.ToTable(nameof(Usuario));
        b.Property(u => u.Nombre).HasMaxLength(80);
        b.Property(u => u.ApellidoPaterno).HasMaxLength(80);
        b.Property(u => u.ApellidoMaterno).HasMaxLength(80);
        b.Ignore(u => u.NombreCompleto);
        b.HasIndex(u => u.NormalizedEmail).IsUnique().HasFilter("[NormalizedEmail] IS NOT NULL").HasDatabaseName("UX_Usuario_Correo");
    }
}

internal sealed class RolConfiguracion : IEntityTypeConfiguration<Rol>
{
    public void Configure(EntityTypeBuilder<Rol> b)
    {
        b.ToTable(nameof(Rol));

        // Los roles se siembran por migración en todos los entornos. Los valores son fijos para que
        // la migración sea estable.
        b.HasData(
            new Rol { Id = 1, Name = Roles.Administrador, NormalizedName = "ADMINISTRADOR", ConcurrencyStamp = "4b0a5c39-2f0e-4c5e-9d0f-6f2f1d7c0a01" },
            new Rol { Id = 2, Name = Roles.Supervisor, NormalizedName = "SUPERVISOR", ConcurrencyStamp = "4b0a5c39-2f0e-4c5e-9d0f-6f2f1d7c0a02" },
            new Rol { Id = 3, Name = Roles.Capturista, NormalizedName = "CAPTURISTA", ConcurrencyStamp = "4b0a5c39-2f0e-4c5e-9d0f-6f2f1d7c0a03" });
    }
}

internal sealed class UsuarioModuloConfiguracion : IEntityTypeConfiguration<UsuarioModulo>
{
    public void Configure(EntityTypeBuilder<UsuarioModulo> b)
    {
        b.ToTable(nameof(UsuarioModulo));
        b.HasKey(um => new { um.UsuarioId, um.ModuloId });
        b.HasIndex(um => um.ModuloId).HasDatabaseName("IX_UsuarioModulo_Modulo");
        b.HasOne(um => um.Usuario).WithMany(u => u.Modulos).HasForeignKey(um => um.UsuarioId);
        b.HasOne<Modulo>().WithMany().HasForeignKey(um => um.ModuloId);
    }
}

internal sealed class AuditoriaConfiguracion : IEntityTypeConfiguration<Auditoria>
{
    public void Configure(EntityTypeBuilder<Auditoria> b)
    {
        b.ToTable(nameof(Auditoria));
        b.HasKey(a => a.AuditoriaId);
        b.Property(a => a.Accion).HasMaxLength(60).IsUnicode(false);
        b.Property(a => a.Entidad).HasMaxLength(60).IsUnicode(false);
        b.Property(a => a.EntidadId).HasMaxLength(40).IsUnicode(false);
        b.Property(a => a.Ip).HasMaxLength(45).IsUnicode(false);
        b.HasIndex(a => a.Fecha).HasDatabaseName("IX_Auditoria_Fecha");
        b.HasIndex(a => new { a.UsuarioId, a.Fecha }).HasDatabaseName("IX_Auditoria_Usuario");
        // Movimientos de un registro en el detalle de la cita; sin él se recorre toda la bitácora.
        b.HasIndex(a => new { a.Entidad, a.EntidadId }).HasDatabaseName("IX_Auditoria_Entidad");
        b.HasOne<Usuario>().WithMany().HasForeignKey(a => a.UsuarioId);
    }
}

internal sealed class EjecucionProcesoConfiguracion : IEntityTypeConfiguration<EjecucionProceso>
{
    public void Configure(EntityTypeBuilder<EjecucionProceso> b)
    {
        b.ToTable(nameof(EjecucionProceso));
        b.HasKey(e => e.Nombre);
        b.Property(e => e.Nombre).HasMaxLength(60).IsUnicode(false);
        b.Property(e => e.UltimoResultado).HasMaxLength(20).IsUnicode(false);
        b.Property(e => e.Detalle).HasMaxLength(1000);
    }
}
