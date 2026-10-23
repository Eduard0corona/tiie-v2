using Tiie.Dominio.Citas;

namespace Tiie.Aplicacion.Puertos;

/// <summary>Tokens de acceso del ciudadano a su cita (RN-16, ADR 0005).</summary>
public interface ITokensCita
{
    /// <summary>32 bytes aleatorios en Base64 URL; vence al final del día siguiente a la cita.</summary>
    TokenAcceso Emitir(DateOnly fechaCita);

    /// <summary>SHA-256 del token tal como llega en el enlace, para buscar la cita.</summary>
    byte[] Hash(string token);

    /// <summary>Recupera el token en claro para armar el enlace de un correo; vacío si no se puede descifrar.</summary>
    string? Recuperar(string tokenProtegido);
}
