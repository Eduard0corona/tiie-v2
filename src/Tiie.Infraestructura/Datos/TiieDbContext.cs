using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Tiie.Aplicacion.Puertos;
using Tiie.Dominio.Agenda;
using Tiie.Dominio.Bitacora;
using Tiie.Dominio.Catalogos;
using Tiie.Dominio.Citas;
using Tiie.Dominio.Notificaciones;
using Tiie.Dominio.Solicitantes;
using Tiie.Infraestructura.Identidad;

namespace Tiie.Infraestructura.Datos;

public sealed class TiieDbContext(DbContextOptions<TiieDbContext> options)
    : IdentityDbContext<Usuario, Rol, int>(options), ITiieDb
{
    /// <summary>Secuencia del número del folio (RN-10).</summary>
    public const string SecuenciaFolio = "SecuenciaFolio";

    public DbSet<Entidad> Entidades => Set<Entidad>();
    public DbSet<Municipio> Municipios => Set<Municipio>();
    public DbSet<NivelEscolar> NivelesEscolares => Set<NivelEscolar>();
    public DbSet<Modulo> Modulos => Set<Modulo>();
    public DbSet<TipoTramite> TiposTramite => Set<TipoTramite>();
    public DbSet<Requisito> Requisitos => Set<Requisito>();
    public DbSet<Oferta> Ofertas => Set<Oferta>();
    public DbSet<PlantillaHorario> Plantillas => Set<PlantillaHorario>();
    public DbSet<Bloqueo> Bloqueos => Set<Bloqueo>();
    public DbSet<Cupo> Cupos => Set<Cupo>();
    public DbSet<HistoricoCita> HistoricoCitas => Set<HistoricoCita>();
    public DbSet<Solicitante> Solicitantes => Set<Solicitante>();
    public DbSet<Cita> Citas => Set<Cita>();
    public DbSet<Notificacion> Notificaciones => Set<Notificacion>();
    public DbSet<Auditoria> Auditorias => Set<Auditoria>();
    public DbSet<EjecucionProceso> EjecucionesProceso => Set<EjecucionProceso>();
    public DbSet<UsuarioModulo> UsuariosModulos => Set<UsuarioModulo>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasDefaultSchema("dbo");
        builder.HasSequence<long>(SecuenciaFolio).StartsAt(1_000_000).IncrementsBy(1);

        // Tablas de Identity con nombres en español y en singular.
        builder.Entity<IdentityUserRole<int>>().ToTable("UsuarioRol");
        builder.Entity<IdentityUserClaim<int>>().ToTable("UsuarioClaim");
        builder.Entity<IdentityUserLogin<int>>().ToTable("UsuarioLogin");
        builder.Entity<IdentityUserToken<int>>().ToTable("UsuarioToken");
        builder.Entity<IdentityRoleClaim<int>>().ToTable("RolClaim");

        builder.ApplyConfigurationsFromAssembly(typeof(TiieDbContext).Assembly);

        // ON DELETE NO ACTION en todo, salvo filas de configuración hijas (docs/05-modelo-de-datos.md).
        var enCascada = new HashSet<(Type Dependiente, Type Principal)>
        {
            (typeof(Requisito), typeof(TipoTramite)),
            (typeof(PlantillaHorario), typeof(Oferta)),
            (typeof(UsuarioModulo), typeof(Usuario)),
        };
        foreach (var fk in builder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
        {
            fk.DeleteBehavior = enCascada.Contains((fk.DeclaringEntityType.ClrType, fk.PrincipalEntityType.ClrType))
                ? DeleteBehavior.Cascade
                : DeleteBehavior.NoAction;
        }
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Instantes en datetimeoffset(0) y horas en time(0).
        configurationBuilder.Properties<DateTimeOffset>().HavePrecision(0);
        configurationBuilder.Properties<TimeOnly>().HavePrecision(0);
        configurationBuilder.Properties<DayOfWeek>().HaveConversion<byte>();
    }
}
