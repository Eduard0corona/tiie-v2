using Microsoft.EntityFrameworkCore;
using Tiie.Aplicacion.Comun;
using Tiie.Aplicacion.Puertos;
using Tiie.Dominio.Citas;
using Tiie.Dominio.Errores;

namespace Tiie.Aplicacion.Citas;

public sealed record RequisitoTexto(string? NivelEscolar, string Texto);

/// <summary>
/// Todo lo que se muestra o se envía de una cita: Mi cita, comprobante, correos y panel. Contiene datos
/// personales completos: quien lo use decide qué enmascarar.
/// </summary>
public sealed record DatosCita(
    int CitaId,
    string Folio,
    string? FolioAnterior,
    EstadoCita Estado,
    OrigenCita Origen,
    int CupoId,
    DateOnly Fecha,
    TimeOnly Hora,
    int OfertaId,
    int ModuloId,
    string Modulo,
    string Direccion,
    string Municipio,
    string? HorarioAtencion,
    decimal? Latitud,
    decimal? Longitud,
    int TipoTramiteId,
    string Tramite,
    decimal? Costo,
    string? Notas,
    string? NotasSede,
    IReadOnlyList<RequisitoTexto> Requisitos,
    int SolicitanteId,
    string Nombre,
    string NombreCompleto,
    string? Curp,
    string? Correo,
    string? Celular,
    string TokenProtegido,
    DateTimeOffset TokenVenceEn,
    string? MotivoCambio,
    DateTimeOffset CreadaEn);

public static class ConsultasCita
{
    /// <summary>Datos de una cita en una sola consulta, sin seguimiento.</summary>
    public static async Task<DatosCita?> ObtenerAsync(ITiieDb db, int citaId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        var fila = await db.Citas.AsNoTracking()
            .Where(c => c.CitaId == citaId)
            .Select(c => new
            {
                c.CitaId,
                c.Folio,
                c.FolioAnterior,
                c.Estado,
                c.Origen,
                c.CupoId,
                c.Cupo.Fecha,
                c.Cupo.Hora,
                c.Cupo.OfertaId,
                c.Cupo.Oferta.ModuloId,
                Modulo = c.Cupo.Oferta.Modulo.Nombre,
                c.Cupo.Oferta.Modulo.Calle,
                c.Cupo.Oferta.Modulo.NumeroExterior,
                c.Cupo.Oferta.Modulo.NumeroInterior,
                c.Cupo.Oferta.Modulo.Colonia,
                c.Cupo.Oferta.Modulo.CodigoPostal,
                Municipio = c.Cupo.Oferta.Modulo.Municipio.Nombre,
                c.Cupo.Oferta.Modulo.HorarioAtencion,
                c.Cupo.Oferta.Modulo.Latitud,
                c.Cupo.Oferta.Modulo.Longitud,
                c.Cupo.Oferta.TipoTramiteId,
                Tramite = c.Cupo.Oferta.TipoTramite.Nombre,
                c.Cupo.Oferta.TipoTramite.Costo,
                c.Cupo.Oferta.TipoTramite.Notas,
                c.Cupo.Oferta.NotasSede,
                Requisitos = c.Cupo.Oferta.TipoTramite.Requisitos
                    .OrderBy(r => r.NivelEscolarId ?? 0).ThenBy(r => r.Orden)
                    .Select(r => new RequisitoTexto(r.NivelEscolar != null ? r.NivelEscolar.Nombre : null, r.Texto))
                    .ToList(),
                c.SolicitanteId,
                c.Solicitante.Nombre,
                c.Solicitante.ApellidoPaterno,
                c.Solicitante.ApellidoMaterno,
                c.Solicitante.Curp,
                c.Solicitante.Correo,
                c.Solicitante.Celular,
                c.TokenProtegido,
                c.TokenVenceEn,
                c.MotivoCambio,
                c.CreadaEn,
            })
            .SingleOrDefaultAsync(ct);

        if (fila is null)
            return null;

        return new DatosCita(
            fila.CitaId, fila.Folio, fila.FolioAnterior, fila.Estado, fila.Origen, fila.CupoId, fila.Fecha, fila.Hora,
            fila.OfertaId, fila.ModuloId, fila.Modulo,
            Formatos.Direccion(fila.Calle, fila.NumeroExterior, fila.NumeroInterior, fila.Colonia, fila.CodigoPostal),
            fila.Municipio, fila.HorarioAtencion, fila.Latitud, fila.Longitud,
            fila.TipoTramiteId, fila.Tramite, fila.Costo, fila.Notas, fila.NotasSede, fila.Requisitos,
            fila.SolicitanteId, fila.Nombre,
            Formatos.NombreCompleto(fila.Nombre, fila.ApellidoPaterno, fila.ApellidoMaterno),
            fila.Curp, fila.Correo, fila.Celular,
            fila.TokenProtegido, fila.TokenVenceEn, fila.MotivoCambio, fila.CreadaEn);
    }

    /// <exception cref="NoEncontradoException">La cita no existe.</exception>
    public static async Task<DatosCita> ExigirAsync(ITiieDb db, int citaId, CancellationToken ct) =>
        await ObtenerAsync(db, citaId, ct) ?? throw new NoEncontradoException();
}
