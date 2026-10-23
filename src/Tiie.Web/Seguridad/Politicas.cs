namespace Tiie.Web.Seguridad;

/// <summary>Políticas de autorización. Ver docs/07-autenticacion-y-autorizacion.md#roles-y-políticas.</summary>
public static class Politicas
{
    /// <summary>Sesión del personal aunque aún no configure MFA: <c>yo</c>, <c>mfa</c> y <c>salir</c>.</summary>
    public const string PersonalSinMfa = nameof(PersonalSinMfa);

    /// <summary>Sesión del personal con MFA: todo <c>/api/admin</c> salvo lo anterior.</summary>
    public const string Personal = nameof(Personal);

    /// <summary>Personal con rol Administrador o Supervisor.</summary>
    public const string Supervisor = nameof(Supervisor);

    /// <summary>Personal con rol Administrador.</summary>
    public const string Administrador = nameof(Administrador);

    /// <summary>Cookie <c>tiie_cita</c> del ciudadano.</summary>
    public const string Cita = nameof(Cita);
}

/// <summary>Esquemas de autenticación propios, además de los de Identity.</summary>
public static class EsquemasTiie
{
    public const string Cita = "Cita";
}

/// <summary>Nombres de entorno. <c>Pruebas</c> lo usan las pruebas de integración.</summary>
public static class Entornos
{
    public const string QA = "QA";
    public const string Pruebas = "Pruebas";
}
