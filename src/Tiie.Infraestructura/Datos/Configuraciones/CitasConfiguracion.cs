using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tiie.Dominio.Citas;
using Tiie.Dominio.Notificaciones;
using Tiie.Dominio.Solicitantes;
using Tiie.Infraestructura.Identidad;

namespace Tiie.Infraestructura.Datos.Configuraciones;

internal sealed class SolicitanteConfiguracion : IEntityTypeConfiguration<Solicitante>
{
    public void Configure(EntityTypeBuilder<Solicitante> b)
    {
        b.ToTable(nameof(Solicitante), t => t.HasCheckConstraint("CK_Solicitante_Sexo", "[Sexo] IN ('H', 'M', 'X')"));
        b.HasKey(s => s.SolicitanteId);
        b.Property(s => s.Curp).HasMaxLength(Curp.Longitud).IsFixedLength().IsUnicode(false);
        b.HasIndex(s => s.Curp).IsUnique().HasFilter("[Curp] IS NOT NULL").HasDatabaseName("UX_Solicitante_Curp");
        b.Property(s => s.Nombre).HasMaxLength(80);
        b.Property(s => s.ApellidoPaterno).HasMaxLength(80);
        b.Property(s => s.ApellidoMaterno).HasMaxLength(80);
        b.Property(s => s.Sexo).HasColumnType("char(1)");
        b.Property(s => s.Celular).HasMaxLength(10).IsUnicode(false);
        b.Property(s => s.Correo).HasMaxLength(150);
    }
}

internal sealed class CitaConfiguracion : IEntityTypeConfiguration<Cita>
{
    public void Configure(EntityTypeBuilder<Cita> b)
    {
        b.ToTable(nameof(Cita), t =>
        {
            t.HasCheckConstraint("CK_Cita_Estado", "[Estado] BETWEEN 1 AND 5");
            t.HasCheckConstraint("CK_Cita_Origen", "[Origen] BETWEEN 1 AND 3");
        });
        b.HasKey(c => c.CitaId);
        b.Property(c => c.Folio).HasMaxLength(14).IsUnicode(false);
        b.HasIndex(c => c.Folio).IsUnique().HasDatabaseName("UX_Cita_Folio");
        b.Property(c => c.TokenHash).HasMaxLength(32).IsFixedLength();
        b.HasIndex(c => c.TokenHash).IsUnique().HasDatabaseName("UX_Cita_TokenHash");
        // Data Protection produce Base64url: solo ASCII, la mitad de bytes que en nvarchar.
        b.Property(c => c.TokenProtegido).HasMaxLength(600).IsUnicode(false);
        b.HasIndex(c => c.ClaveIdempotencia)
            .IsUnique()
            .HasFilter("[ClaveIdempotencia] IS NOT NULL")
            .HasDatabaseName("UX_Cita_ClaveIdempotencia");
        b.Property(c => c.VersionAvisoPrivacidad).HasMaxLength(20).IsUnicode(false);
        b.Property(c => c.MotivoCambio).HasMaxLength(300);
        b.Property(c => c.FolioAnterior).HasMaxLength(20).IsUnicode(false);
        b.HasIndex(c => c.FolioAnterior)
            .HasFilter("[FolioAnterior] IS NOT NULL")
            .HasDatabaseName("IX_Cita_FolioAnterior");
        b.Property(c => c.RowVersion).IsRowVersion();

        // RN-01: un solicitante solo puede tener una cita Agendada.
        b.HasIndex(c => c.SolicitanteId)
            .IsUnique()
            .HasFilter($"[Estado] = {(byte)EstadoCita.Agendada}")
            .HasDatabaseName("UX_Cita_SolicitanteActiva");
        b.HasIndex(c => new { c.SolicitanteId, c.Estado }).HasDatabaseName("IX_Cita_Solicitante");
        // Agenda, listados, tablero y reportes llegan a la cita por su cupo; las columnas incluidas evitan volver
        // a la tabla por cada fila. Ninguna cambia después de crear la cita, salvo Estado.
        b.HasIndex(c => c.CupoId)
            .HasDatabaseName("IX_Cita_Cupo")
            .IncludeProperties(c => new { c.Estado, c.SolicitanteId, c.Folio, c.Origen });
        // Llaves foráneas casi siempre vacías: el filtro deja fuera los nulos y la búsqueda por valor sigue usando el índice.
        b.HasIndex(c => c.CitaOrigenId).HasFilter("[CitaOrigenId] IS NOT NULL").HasDatabaseName("IX_Cita_CitaOrigen");
        b.HasIndex(c => c.CreadaPor).HasFilter("[CreadaPor] IS NOT NULL").HasDatabaseName("IX_Cita_CreadaPor");
        b.HasIndex(c => c.ActualizadaPor).HasFilter("[ActualizadaPor] IS NOT NULL").HasDatabaseName("IX_Cita_ActualizadaPor");

        b.HasOne(c => c.Cupo).WithMany().HasForeignKey(c => c.CupoId);
        b.HasOne(c => c.Solicitante).WithMany().HasForeignKey(c => c.SolicitanteId);
        b.HasOne(c => c.CitaOrigen).WithMany().HasForeignKey(c => c.CitaOrigenId);
        b.HasOne<Usuario>().WithMany().HasForeignKey(c => c.CreadaPor);
        b.HasOne<Usuario>().WithMany().HasForeignKey(c => c.ActualizadaPor);
    }
}

internal sealed class NotificacionConfiguracion : IEntityTypeConfiguration<Notificacion>
{
    public void Configure(EntityTypeBuilder<Notificacion> b)
    {
        b.ToTable(nameof(Notificacion), t =>
        {
            t.HasCheckConstraint("CK_Notificacion_Tipo", "[Tipo] BETWEEN 1 AND 7");
            t.HasCheckConstraint("CK_Notificacion_Canal", "[Canal] = 1");
            t.HasCheckConstraint("CK_Notificacion_Estado", "[Estado] BETWEEN 1 AND 4");
        });
        b.HasKey(n => n.NotificacionId);
        b.Property(n => n.Destinatario).HasMaxLength(150);
        b.Property(n => n.UltimoError).HasMaxLength(1000);
        b.HasIndex(n => n.ProximoIntentoEn)
            .HasFilter($"[Estado] IN ({(byte)EstadoNotificacion.Pendiente}, {(byte)EstadoNotificacion.Fallida})")
            .HasDatabaseName("IX_Notificacion_Pendientes");
        // RN-14: una cita recibe a lo sumo un recordatorio.
        b.HasIndex(n => new { n.CitaId, n.Tipo })
            .IsUnique()
            .HasFilter($"[Tipo] = {(byte)TipoNotificacion.Recordatorio}")
            .HasDatabaseName("UX_Notificacion_Recordatorio");
        b.HasIndex(n => n.CitaId).HasDatabaseName("IX_Notificacion_Cita");
        b.HasOne(n => n.Cita).WithMany(c => c.Notificaciones).HasForeignKey(n => n.CitaId);
    }
}
