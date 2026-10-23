using Tiie.Dominio.Citas;
using Tiie.Dominio.Errores;

namespace Tiie.Dominio.Agenda;

/// <summary>Reglas de la ventana de reserva y de los cupos (RN-03, RN-07, RN-09, RN-18).</summary>
public static class ReglasAgenda
{
    /// <summary>RN-03: en línea se reserva desde mañana. RN-18: en ventanilla también hoy.</summary>
    public static DateOnly PrimerDiaReservable(DateOnly hoy, OrigenCita origen) =>
        origen == OrigenCita.Web ? hoy.AddDays(1) : hoy;

    /// <summary>RN-03: hasta hoy más los días de ventana de la oferta.</summary>
    public static DateOnly UltimoDiaReservable(DateOnly hoy, short diasVentana) => hoy.AddDays(diasVentana);

    public static bool EnVentana(DateOnly fecha, DateOnly hoy, short diasVentana, OrigenCita origen) =>
        fecha >= PrimerDiaReservable(hoy, origen) && fecha <= UltimoDiaReservable(hoy, diasVentana);

    /// <summary>Una hora de cupo «ya pasó» cuando la fecha y hora locales son anteriores al instante actual.</summary>
    public static bool YaPaso(DateOnly fecha, TimeOnly hora, DateTime ahoraLocal) => fecha.ToDateTime(hora) <= ahoraLocal;

    /// <summary>
    /// Valida que se pueda reservar en un cupo. El cupo determina oferta, fecha y hora (RN-09); nada de eso
    /// viene del cliente.
    /// </summary>
    /// <exception cref="ReglaNegocioException">
    /// <see cref="CodigoError.OfertaInactiva"/>, <see cref="CodigoError.FueraDeVentana"/> o <see cref="CodigoError.CupoCerrado"/>.
    /// </exception>
    public static void ValidarReserva(
        DateOnly fecha, TimeOnly hora, short diasVentana, bool cerrado, bool ofertaActiva, OrigenCita origen, DateTime ahoraLocal)
    {
        if (!ofertaActiva)
            throw new ReglaNegocioException(CodigoError.OfertaInactiva);

        var hoy = DateOnly.FromDateTime(ahoraLocal);
        if (!EnVentana(fecha, hoy, diasVentana, origen) || YaPaso(fecha, hora, ahoraLocal))
            throw new ReglaNegocioException(CodigoError.FueraDeVentana);

        if (cerrado)
            throw new ReglaNegocioException(CodigoError.CupoCerrado);
    }

    /// <summary>Rango pedido recortado a la ventana de reserva en línea.</summary>
    public static (DateOnly Desde, DateOnly Hasta) RecortarAVentana(
        DateOnly? desde, DateOnly? hasta, DateOnly hoy, short diasVentana)
    {
        var primero = PrimerDiaReservable(hoy, OrigenCita.Web);
        var ultimo = UltimoDiaReservable(hoy, diasVentana);
        var inicio = desde is { } d && d > primero ? d : primero;
        var fin = hasta is { } h && h < ultimo ? h : ultimo;
        return (inicio, fin);
    }
}
