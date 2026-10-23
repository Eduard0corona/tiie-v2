using Tiie.Dominio.Errores;

namespace Tiie.Web.Errores;

/// <summary>Código HTTP y título de cada <see cref="CodigoError"/>. Catálogo en docs/06-api.md#errores.</summary>
public static class CatalogoErrores
{
    private static readonly Dictionary<CodigoError, (int Estado, string Titulo)> Catalogo = new()
    {
        [CodigoError.SolicitudInvalida] = (StatusCodes.Status400BadRequest, "La solicitud no es válida"),
        [CodigoError.NoAutenticado] = (StatusCodes.Status401Unauthorized, "Inicia sesión para continuar"),
        [CodigoError.CredencialesInvalidas] = (StatusCodes.Status401Unauthorized, "Usuario o contraseña incorrectos"),
        [CodigoError.CodigoMfaInvalido] = (StatusCodes.Status401Unauthorized, "El código no es correcto"),
        [CodigoError.Prohibido] = (StatusCodes.Status403Forbidden, "No tienes permiso para esta acción"),
        [CodigoError.MfaRequerido] = (StatusCodes.Status403Forbidden, "Configura la verificación en dos pasos para continuar"),
        [CodigoError.NoEncontrado] = (StatusCodes.Status404NotFound, "No encontramos lo que buscas"),
        [CodigoError.SinCupo] = (StatusCodes.Status409Conflict, "No hay lugar en el horario elegido"),
        [CodigoError.YaTieneCita] = (StatusCodes.Status409Conflict, "Esta CURP ya tiene una cita agendada"),
        [CodigoError.Conflicto] = (StatusCodes.Status409Conflict, "Alguien más modificó este registro"),
        [CodigoError.Validacion] = (StatusCodes.Status422UnprocessableEntity, "Hay datos por corregir"),
        [CodigoError.CaptchaInvalido] = (StatusCodes.Status422UnprocessableEntity, "Confirma que no eres un robot"),
        [CodigoError.FueraDeVentana] = (StatusCodes.Status422UnprocessableEntity, "La fecha está fuera del periodo de reserva"),
        [CodigoError.CupoCerrado] = (StatusCodes.Status422UnprocessableEntity, "El horario está cerrado"),
        [CodigoError.OfertaInactiva] = (StatusCodes.Status422UnprocessableEntity, "El trámite no está disponible en este módulo"),
        [CodigoError.CitaNoModificable] = (StatusCodes.Status422UnprocessableEntity, "La cita ya no se puede modificar"),
        [CodigoError.EnlaceInvalido] = (StatusCodes.Status422UnprocessableEntity, "Este enlace ya no es válido"),
        [CodigoError.TokenInvalido] = (StatusCodes.Status422UnprocessableEntity, "Este enlace ya no es válido"),
        [CodigoError.CuentaBloqueada] = (StatusCodes.Status423Locked, "La cuenta está bloqueada temporalmente"),
        [CodigoError.DemasiadasSolicitudes] = (StatusCodes.Status429TooManyRequests, "Demasiados intentos. Espera unos minutos"),
        [CodigoError.ErrorInterno] = (StatusCodes.Status500InternalServerError, "Algo salió mal"),
        [CodigoError.CorreoNoDisponible] = (StatusCodes.Status503ServiceUnavailable, "No se pudo enviar el correo"),
    };

    public static int Estado(CodigoError codigo) => Catalogo[codigo].Estado;

    public static string Titulo(CodigoError codigo) => Catalogo[codigo].Titulo;

    /// <summary>Código por omisión para una respuesta de error que no trae uno.</summary>
    public static CodigoError PorEstado(int estado) => estado switch
    {
        StatusCodes.Status401Unauthorized => CodigoError.NoAutenticado,
        StatusCodes.Status403Forbidden => CodigoError.Prohibido,
        StatusCodes.Status404NotFound => CodigoError.NoEncontrado,
        StatusCodes.Status409Conflict => CodigoError.Conflicto,
        StatusCodes.Status422UnprocessableEntity => CodigoError.Validacion,
        StatusCodes.Status423Locked => CodigoError.CuentaBloqueada,
        StatusCodes.Status429TooManyRequests => CodigoError.DemasiadasSolicitudes,
        StatusCodes.Status503ServiceUnavailable => CodigoError.CorreoNoDisponible,
        >= 500 => CodigoError.ErrorInterno,
        _ => CodigoError.SolicitudInvalida,
    };
}
