using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Tiie.Dominio.Agenda;
using Tiie.Dominio.Bitacora;
using Tiie.Dominio.Catalogos;
using Tiie.Dominio.Citas;
using Tiie.Dominio.Notificaciones;
using Tiie.Dominio.Solicitantes;

namespace Tiie.Aplicacion.Puertos;

/// <summary>Acceso a datos que usan los casos de uso. Lo implementa <c>TiieDbContext</c>.</summary>
public interface ITiieDb
{
    DbSet<Entidad> Entidades { get; }
    DbSet<Municipio> Municipios { get; }
    DbSet<NivelEscolar> NivelesEscolares { get; }
    DbSet<Modulo> Modulos { get; }
    DbSet<TipoTramite> TiposTramite { get; }
    DbSet<Requisito> Requisitos { get; }
    DbSet<Oferta> Ofertas { get; }
    DbSet<PlantillaHorario> Plantillas { get; }
    DbSet<Bloqueo> Bloqueos { get; }
    DbSet<Cupo> Cupos { get; }
    DbSet<HistoricoCita> HistoricoCitas { get; }
    DbSet<Solicitante> Solicitantes { get; }
    DbSet<Cita> Citas { get; }
    DbSet<Notificacion> Notificaciones { get; }
    DbSet<Auditoria> Auditorias { get; }
    DbSet<EjecucionProceso> EjecucionesProceso { get; }

    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
