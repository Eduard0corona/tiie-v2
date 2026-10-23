using Microsoft.EntityFrameworkCore;
using Tiie.Aplicacion.Comun;
using Tiie.Aplicacion.Puertos;

namespace Tiie.Aplicacion.Bitacora;

public sealed record FiltroAuditoria(
    DateOnly? Desde, DateOnly? Hasta, int? UsuarioId, string? Accion, string? Entidad, int? Pagina, int? Tamano);

public sealed record RegistroBitacora(
    long AuditoriaId, DateTimeOffset Fecha, int? UsuarioId, string? Usuario, string Accion, string Entidad, string? EntidadId,
    string? Cambios, string? Ip);

/// <summary>Bitácora de auditoría (RF-19). Solo el administrador la consulta; nadie la edita ni la borra.</summary>
public sealed class ConsultarAuditoriaManejador(ITiieDb db, IUsuariosLectura usuarios, Reloj.RelojAgenda reloj)
{
    public async Task<ListaPaginada<RegistroBitacora>> EjecutarAsync(FiltroAuditoria filtro, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        // Las fechas del filtro son locales; los registros guardan instantes UTC.
        DateTimeOffset? desde = filtro.Desde is { } d ? reloj.AUtc(d, TimeOnly.MinValue) : null;
        DateTimeOffset? hasta = filtro.Hasta is { } h ? reloj.AUtc(h.AddDays(1), TimeOnly.MinValue) : null;

        var consulta = db.Auditorias.AsNoTracking()
            .Where(a => desde == null || a.Fecha >= desde)
            .Where(a => hasta == null || a.Fecha < hasta)
            .Where(a => filtro.UsuarioId == null || a.UsuarioId == filtro.UsuarioId)
            .Where(a => filtro.Accion == null || a.Accion.StartsWith(filtro.Accion))
            .Where(a => filtro.Entidad == null || a.Entidad == filtro.Entidad)
            .OrderByDescending(a => a.Fecha).ThenByDescending(a => a.AuditoriaId);

        var pagina = await Paginado.De(filtro.Pagina, filtro.Tamano).AplicarAsync(consulta, ct);
        var nombres = await usuarios.NombresAsync(pagina.Elementos.Select(a => a.UsuarioId).OfType<int>(), ct);
        return new ListaPaginada<RegistroBitacora>(
            [.. pagina.Elementos.Select(a => new RegistroBitacora(
                a.AuditoriaId, a.Fecha, a.UsuarioId,
                a.UsuarioId is { } u && nombres.TryGetValue(u, out var nombre) ? nombre : null,
                a.Accion, a.Entidad, a.EntidadId, a.Cambios, a.Ip))],
            pagina.Total, pagina.Pagina, pagina.Tamano);
    }
}
