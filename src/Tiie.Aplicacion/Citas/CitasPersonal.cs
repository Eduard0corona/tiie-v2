using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Tiie.Aplicacion.Bitacora;
using Tiie.Aplicacion.Comun;
using Tiie.Aplicacion.Puertos;
using Tiie.Aplicacion.Reloj;
using Tiie.Aplicacion.Reservas;
using Tiie.Dominio.Citas;
using Tiie.Dominio.Errores;
using Tiie.Dominio.Notificaciones;
using Tiie.Dominio.Privacidad;
using Tiie.Dominio.Solicitantes;

namespace Tiie.Aplicacion.Citas;

public sealed record CitaAgenda(
    int CitaId, string Folio, TimeOnly Hora, string Tramite, string NombreCompleto, string? Curp, EstadoCita Estado, OrigenCita Origen);

public sealed record CitaListado(
    int CitaId, string Folio, string? FolioAnterior, DateOnly Fecha, TimeOnly Hora, int ModuloId, string Modulo, string Tramite,
    string NombreCompleto, string? Curp, EstadoCita Estado, OrigenCita Origen);

public sealed record FiltroCitas(
    DateOnly? Desde, DateOnly? Hasta, int? ModuloId, int? TipoTramiteId, EstadoCita? Estado, string? Busqueda, int? Pagina, int? Tamano);

public sealed record CitaRelacionada(int CitaId, string Folio, EstadoCita Estado, DateOnly Fecha, TimeOnly Hora);

public sealed record NotificacionCita(
    long NotificacionId, TipoNotificacion Tipo, EstadoNotificacion Estado, string Destinatario, int Intentos,
    DateTimeOffset CreadaEn, DateTimeOffset? EnviadaEn, string? UltimoError);

public sealed record MovimientoCita(DateTimeOffset Fecha, string Accion, string? Usuario);

/// <summary>Detalle para el personal del módulo: datos completos, historial de estados y notificaciones.</summary>
public sealed record DetalleCitaPersonal(
    int CitaId, string Folio, string? FolioAnterior, EstadoCita Estado, OrigenCita Origen, DateOnly Fecha, TimeOnly Hora,
    int OfertaId, int ModuloId, string Modulo, string Tramite, string NombreCompleto, string? Curp, string? Correo,
    string? Celular, string? MotivoCambio, DateTimeOffset CreadaEn, string? CreadaPor,
    IReadOnlyList<CitaRelacionada> Relacionadas, IReadOnlyList<NotificacionCita> Notificaciones,
    IReadOnlyList<MovimientoCita> Movimientos, bool PuedeModificar);

/// <summary>Cita en ventanilla (RF-10): mismas reglas que en línea, sin captcha y con cupos de hoy (RN-18).</summary>
public sealed record CitaVentanilla(
    int CupoId, string Curp, string Nombre, string ApellidoPaterno, string? ApellidoMaterno, string? Celular, string? Correo,
    bool AvisoLeido);

public sealed record CitaRegistrada(int CitaId, string Folio);

public sealed record EstadoResultante(EstadoCita Estado);

/// <summary>Operación diaria del personal sobre las citas de sus módulos (RF-09, RF-10, RF-11).</summary>
public sealed class CitasPersonalManejador(
    ITiieDb db,
    IAutorizacionModulo alcance,
    IUsuarioActual usuario,
    RelojAgenda reloj,
    CrearCitaManejador crear,
    OperacionesCita operaciones,
    IGeneradorPdf pdf,
    IUsuariosLectura usuarios,
    RegistroAuditoria auditoria)
{
    private const string Entidad = "Cita";

    /// <summary>Citas de un módulo en una fecha, ordenadas por hora; sin canceladas ni reprogramadas.</summary>
    public async Task<IReadOnlyList<CitaAgenda>> AgendaAsync(int moduloId, DateOnly? fecha, CancellationToken ct)
    {
        alcance.ExigirAcceso(moduloId);
        var dia = fecha ?? reloj.HoyLocal();
        var filas = await db.Citas.AsNoTracking()
            .Where(c => c.Cupo.Oferta.ModuloId == moduloId && c.Cupo.Fecha == dia)
            .Where(c => c.Estado != EstadoCita.Cancelada && c.Estado != EstadoCita.Reprogramada)
            .OrderBy(c => c.Cupo.Hora).ThenBy(c => c.Folio)
            .Select(c => new
            {
                c.CitaId,
                c.Folio,
                c.Cupo.Hora,
                Tramite = c.Cupo.Oferta.TipoTramite.Nombre,
                c.Solicitante.Nombre,
                c.Solicitante.ApellidoPaterno,
                c.Solicitante.ApellidoMaterno,
                c.Solicitante.Curp,
                c.Estado,
                c.Origen,
            })
            .ToListAsync(ct);
        return [.. filas.Select(f => new CitaAgenda(f.CitaId, f.Folio, f.Hora, f.Tramite,
            Formatos.NombreCompleto(f.Nombre, f.ApellidoPaterno, f.ApellidoMaterno), f.Curp, f.Estado, f.Origen))];
    }

    /// <summary>Búsqueda por rango, módulo, trámite y estado, o por folio, folio anterior o CURP (RF-09).</summary>
    public async Task<ListaPaginada<CitaListado>> BuscarAsync(FiltroCitas filtro, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        if (filtro.ModuloId is { } m)
            alcance.ExigirAcceso(m);
        var busqueda = string.IsNullOrWhiteSpace(filtro.Busqueda) ? null : Curp.Normalizar(filtro.Busqueda);

        var consulta = db.Citas.AsNoTracking()
            .SoloModulosPermitidos(alcance, c => c.Cupo.Oferta.ModuloId)
            .Where(c => filtro.Desde == null || c.Cupo.Fecha >= filtro.Desde)
            .Where(c => filtro.Hasta == null || c.Cupo.Fecha <= filtro.Hasta)
            .Where(c => filtro.ModuloId == null || c.Cupo.Oferta.ModuloId == filtro.ModuloId)
            .Where(c => filtro.TipoTramiteId == null || c.Cupo.Oferta.TipoTramiteId == filtro.TipoTramiteId)
            .Where(c => filtro.Estado == null || c.Estado == filtro.Estado)
            .Where(c => busqueda == null || c.Folio == busqueda || c.FolioAnterior == busqueda || c.Solicitante.Curp == busqueda)
            .OrderByDescending(c => c.Cupo.Fecha).ThenBy(c => c.Cupo.Hora).ThenBy(c => c.CitaId)
            .Select(c => new CitaListado(
                c.CitaId, c.Folio, c.FolioAnterior, c.Cupo.Fecha, c.Cupo.Hora, c.Cupo.Oferta.ModuloId, c.Cupo.Oferta.Modulo.Nombre,
                c.Cupo.Oferta.TipoTramite.Nombre,
                c.Solicitante.Nombre + " " + c.Solicitante.ApellidoPaterno + (c.Solicitante.ApellidoMaterno != null ? " " + c.Solicitante.ApellidoMaterno : ""),
                c.Solicitante.Curp, c.Estado, c.Origen));
        return await Paginado.De(filtro.Pagina, filtro.Tamano).AplicarAsync(consulta, ct);
    }

    public async Task<DetalleCitaPersonal> DetalleAsync(int citaId, CancellationToken ct)
    {
        var c = await ExigirCitaAsync(citaId, ct);
        var relacionadas = await db.Citas.AsNoTracking()
            .Where(x => x.CitaOrigenId == citaId || x.CitaId == db.Citas.Where(o => o.CitaId == citaId).Select(o => o.CitaOrigenId).FirstOrDefault())
            .Select(x => new CitaRelacionada(x.CitaId, x.Folio, x.Estado, x.Cupo.Fecha, x.Cupo.Hora))
            .ToListAsync(ct);
        var notificaciones = await db.Notificaciones.AsNoTracking()
            .Where(n => n.CitaId == citaId)
            .OrderBy(n => n.NotificacionId)
            .Select(n => new NotificacionCita(n.NotificacionId, n.Tipo, n.Estado, n.Destinatario, n.Intentos, n.CreadaEn, n.EnviadaEn, n.UltimoError))
            .ToListAsync(ct);
        var id = citaId.ToString(CultureInfo.InvariantCulture);
        var movimientos = await db.Auditorias.AsNoTracking()
            .Where(a => a.Entidad == Entidad && a.EntidadId == id)
            .OrderBy(a => a.Fecha)
            .Select(a => new { a.Fecha, a.Accion, a.UsuarioId })
            .ToListAsync(ct);
        var creadaPor = await db.Citas.Where(x => x.CitaId == citaId).Select(x => x.CreadaPor).SingleAsync(ct);
        var nombres = await usuarios.NombresAsync(movimientos.Select(m => m.UsuarioId).Append(creadaPor).OfType<int>(), ct);

        return new DetalleCitaPersonal(
            c.CitaId, c.Folio, c.FolioAnterior, c.Estado, c.Origen, c.Fecha, c.Hora, c.OfertaId, c.ModuloId, c.Modulo, c.Tramite,
            c.NombreCompleto, c.Curp, c.Correo, c.Celular, c.MotivoCambio, c.CreadaEn,
            creadaPor is { } u && nombres.TryGetValue(u, out var n) ? n : null,
            relacionadas, notificaciones,
            [.. movimientos.Select(m => new MovimientoCita(m.Fecha, m.Accion,
                m.UsuarioId is { } x && nombres.TryGetValue(x, out var nombre) ? nombre : null))],
            ReglasCita.PersonalPuedeModificar(c.Estado, c.Fecha, c.Hora, reloj.AhoraLocal()));
    }

    public async Task<CitaRegistrada> VentanillaAsync(CitaVentanilla datos, Guid claveIdempotencia, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        var moduloId = await db.Cupos.Where(c => c.CupoId == datos.CupoId).Select(c => (int?)c.Oferta.ModuloId).SingleOrDefaultAsync(ct)
            ?? throw new NoEncontradoException();
        alcance.ExigirAcceso(moduloId);

        var creada = await crear.EjecutarAsync(new CrearCita(
            datos.CupoId, datos.Curp, datos.Nombre, datos.ApellidoPaterno, datos.ApellidoMaterno,
            string.IsNullOrWhiteSpace(datos.Celular) ? null : datos.Celular,
            string.IsNullOrWhiteSpace(datos.Correo) ? null : datos.Correo,
            datos.AvisoLeido, claveIdempotencia, OrigenCita.Ventanilla, usuario.UsuarioId), ct);
        await auditoria.RegistrarAsync("Cita.Ventanilla", Entidad, Id(creada.CitaId),
            new { creada.Resumen.Folio, curp = Enmascarar.Curp(datos.Curp) }, null, ct);
        return new CitaRegistrada(creada.CitaId, creada.Resumen.Folio);
    }

    /// <summary>RF-09: Atendida o No asistió en una cita Agendada de hoy o anterior, nunca futura.</summary>
    public async Task<EstadoResultante> RegistrarAsistenciaAsync(int citaId, bool asistio, CancellationToken ct)
    {
        var cita = await ExigirCitaAsync(citaId, ct);
        ReglasCita.ExigirAsistenciaRegistrable(cita.Estado, cita.Fecha, reloj.HoyLocal());
        var nuevo = asistio ? EstadoCita.Atendida : EstadoCita.NoAsistio;
        await CambiarEstadoAsync(citaId, EstadoCita.Agendada, nuevo, null, ct);
        await auditoria.RegistrarAsync("Cita.Asistencia", Entidad, Id(citaId), new { estado = nuevo.ToString() }, null, ct);
        return new EstadoResultante(nuevo);
    }

    /// <summary>RN-12: el supervisor corrige Atendida ↔ No asistió hasta 7 días después de la cita, con motivo.</summary>
    public async Task<EstadoResultante> CorregirAsistenciaAsync(int citaId, bool asistio, string? motivo, CancellationToken ct)
    {
        ValidarMotivo(motivo);
        var cita = await ExigirCitaAsync(citaId, ct);
        ReglasCita.ExigirCorreccionPermitida(cita.Estado, cita.Fecha, reloj.HoyLocal());
        var nuevo = asistio ? EstadoCita.Atendida : EstadoCita.NoAsistio;
        if (nuevo != cita.Estado)
            await CambiarEstadoAsync(citaId, cita.Estado, nuevo, motivo, ct);
        await auditoria.RegistrarAsync("Cita.CorregirAsistencia", Entidad, Id(citaId),
            new { estado = new { anterior = cita.Estado.ToString(), nuevo = nuevo.ToString() }, motivo }, null, ct);
        return new EstadoResultante(nuevo);
    }

    /// <summary>RF-11: el personal cancela con motivo hasta la hora de la cita; se libera el lugar y se avisa.</summary>
    public async Task<EstadoResultante> CancelarAsync(int citaId, string? motivo, CancellationToken ct)
    {
        ValidarMotivo(motivo);
        var cita = await ExigirCitaAsync(citaId, ct);
        ReglasCita.ExigirModificablePorPersonal(cita.Estado, cita.Fecha, cita.Hora, reloj.AhoraLocal());
        await operaciones.CancelarAsync(cita, motivo!.Trim(), usuario.UsuarioId, TipoNotificacion.Cancelacion, ct);
        await auditoria.RegistrarAsync("Cita.Cancelar", Entidad, Id(citaId), new { motivo }, null, ct);
        return new EstadoResultante(EstadoCita.Cancelada);
    }

    /// <summary>RN-17 por el personal: otro cupo de la misma oferta; puede ser de hoy con hora futura (RN-18).</summary>
    public async Task<CitaRegistrada> ReprogramarAsync(int citaId, int cupoId, string? motivo, CancellationToken ct)
    {
        ValidarMotivo(motivo);
        var cita = await ExigirCitaAsync(citaId, ct);
        ReglasCita.ExigirModificablePorPersonal(cita.Estado, cita.Fecha, cita.Hora, reloj.AhoraLocal());
        var nueva = await operaciones.ReprogramarAsync(cita, cupoId, motivo!.Trim(), usuario.UsuarioId, OrigenCita.Ventanilla, ct);
        var folio = await db.Citas.Where(c => c.CitaId == nueva.CitaId).Select(c => c.Folio).SingleAsync(ct);
        await auditoria.RegistrarAsync("Cita.Reprogramar", Entidad, Id(citaId), new { motivo, citaNueva = nueva.CitaId, folio }, null, ct);
        return new CitaRegistrada(nueva.CitaId, folio);
    }

    /// <summary>Encola otra vez la confirmación (docs/10#proceso-de-envío).</summary>
    public async Task ReenviarAsync(int citaId, CancellationToken ct)
    {
        var cita = await ExigirCitaAsync(citaId, ct);
        if (cita.Estado != EstadoCita.Agendada)
            throw new ReglaNegocioException(CodigoError.CitaNoModificable, "Solo se reenvía la confirmación de una cita agendada.");
        if (string.IsNullOrWhiteSpace(cita.Correo))
            throw Validaciones.Error("correo", "La cita no tiene correo; imprime el comprobante.");
        db.Notificaciones.Add(Notificacion.Nueva(citaId, TipoNotificacion.Confirmacion, cita.Correo, reloj.AhoraUtc()));
        await db.SaveChangesAsync(ct);
        await auditoria.RegistrarAsync("Cita.Reenviar", Entidad, Id(citaId), null, null, ct);
    }

    public async Task<(byte[] Contenido, string Folio)> ComprobanteAsync(int citaId, CancellationToken ct)
    {
        var cita = await ExigirCitaAsync(citaId, ct);
        return (pdf.Comprobante(cita), cita.Folio);
    }

    /// <summary>La cita debe existir y ser de un módulo del usuario; si no, 404.</summary>
    private async Task<DatosCita> ExigirCitaAsync(int citaId, CancellationToken ct)
    {
        var cita = await ConsultasCita.ExigirAsync(db, citaId, ct);
        alcance.ExigirAcceso(cita.ModuloId);
        return cita;
    }

    private async Task CambiarEstadoAsync(int citaId, EstadoCita desde, EstadoCita hacia, string? motivo, CancellationToken ct)
    {
        var ahora = reloj.AhoraUtc();
        var actor = usuario.UsuarioId;
        var filas = await db.Citas
            .Where(c => c.CitaId == citaId && c.Estado == desde)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Estado, hacia)
                .SetProperty(c => c.MotivoCambio, c => motivo ?? c.MotivoCambio)
                .SetProperty(c => c.ActualizadaEn, ahora)
                .SetProperty(c => c.ActualizadaPor, actor), ct);
        if (filas == 0)
            throw new ReglaNegocioException(CodigoError.CitaNoModificable);
    }

    private static void ValidarMotivo(string? motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            throw Validaciones.Error("motivo", "Escribe el motivo.");
        if (motivo.Length > 300)
            throw Validaciones.Error("motivo", "Escribe 300 caracteres o menos.");
    }

    private static string Id(int id) => id.ToString(CultureInfo.InvariantCulture);
}
