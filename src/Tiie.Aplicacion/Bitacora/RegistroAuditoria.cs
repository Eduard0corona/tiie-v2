using System.Text.Json;
using Tiie.Aplicacion.Puertos;
using Tiie.Aplicacion.Reloj;
using Tiie.Dominio.Bitacora;

namespace Tiie.Aplicacion.Bitacora;

/// <summary>
/// Registra en la bitácora acciones que no son un cambio de entidad: inicios de sesión, intentos fallidos,
/// configuración de MFA. Los cambios de catálogos y citas los registra el interceptor de auditoría.
/// </summary>
public sealed class RegistroAuditoria(ITiieDb db, RelojAgenda reloj, IUsuarioActual usuarioActual)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Agrega el registro al contexto; se guarda con el siguiente <c>SaveChanges</c>.
    /// <paramref name="detalle"/> no debe contener datos personales sin enmascarar.
    /// </summary>
    public void Agregar(string accion, string entidad, string? entidadId = null, object? detalle = null, int? usuarioId = null)
    {
        db.Auditorias.Add(new Auditoria
        {
            Fecha = reloj.AhoraUtc(),
            UsuarioId = usuarioId ?? usuarioActual.UsuarioId,
            Accion = accion,
            Entidad = entidad,
            EntidadId = entidadId,
            Cambios = detalle is null ? null : JsonSerializer.Serialize(detalle, Json),
            Ip = usuarioActual.Ip,
        });
    }

    /// <summary>Agrega el registro y lo guarda de inmediato.</summary>
    public async Task RegistrarAsync(
        string accion, string entidad, string? entidadId, object? detalle, int? usuarioId, CancellationToken ct)
    {
        Agregar(accion, entidad, entidadId, detalle, usuarioId);
        await db.SaveChangesAsync(ct);
    }
}

/// <summary>Nombres de las acciones de la bitácora que no son cambios de entidad.</summary>
public static class AccionesAuditoria
{
    public const string SesionIniciar = "Sesion.Iniciar";
    public const string SesionCerrar = "Sesion.Cerrar";
    public const string SesionIntentoFallido = "Sesion.IntentoFallido";
    public const string SesionBloqueo = "Sesion.Bloqueo";
    public const string MfaConfigurar = "Mfa.Configurar";
    public const string MfaCodigoRecuperacion = "Mfa.CodigoRecuperacion";
    public const string ContrasenaCambiar = "Contrasena.Cambiar";
    public const string ContrasenaSolicitarRestablecimiento = "Contrasena.SolicitarRestablecimiento";
    public const string ContrasenaRestablecer = "Contrasena.Restablecer";
    public const string CuentaActivar = "Cuenta.Activar";
}
