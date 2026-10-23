namespace Tiie.Dominio.Errores;

/// <summary>
/// Catálogo de códigos de error. La API los envía en la extensión <c>codigo</c> de Problem Details
/// y la SPA decide el mensaje con ellos. Ver docs/06-api.md.
/// </summary>
public enum CodigoError
{
    SolicitudInvalida,
    NoAutenticado,
    CredencialesInvalidas,
    CodigoMfaInvalido,
    Prohibido,
    MfaRequerido,
    NoEncontrado,
    SinCupo,
    YaTieneCita,
    Conflicto,
    Validacion,
    CaptchaInvalido,
    FueraDeVentana,
    CupoCerrado,
    OfertaInactiva,
    CitaNoModificable,
    EnlaceInvalido,
    TokenInvalido,
    CuentaBloqueada,
    DemasiadasSolicitudes,
    ErrorInterno,
    CorreoNoDisponible,
}
