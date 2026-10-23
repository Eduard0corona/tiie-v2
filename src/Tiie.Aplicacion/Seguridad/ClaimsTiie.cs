namespace Tiie.Aplicacion.Seguridad;

/// <summary>Claims propios de las sesiones del personal y del ciudadano.</summary>
public static class ClaimsTiie
{
    /// <summary><c>true</c> si la sesión ya pasó por MFA o la configuró.</summary>
    public const string Mfa = "mfa";

    public const string NombreCompleto = "nombre_completo";

    /// <summary>Un claim por cada módulo asignado. Los administradores no lo necesitan.</summary>
    public const string Modulo = "modulo";

    /// <summary>Cita a la que da acceso la cookie <c>tiie_cita</c>.</summary>
    public const string CitaId = "cita_id";

    /// <summary>Vencimiento del token de la cita (RN-16), en formato ISO 8601.</summary>
    public const string TokenVenceEn = "token_vence_en";
}
