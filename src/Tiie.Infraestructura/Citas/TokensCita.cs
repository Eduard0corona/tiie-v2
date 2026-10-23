using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Tiie.Aplicacion.Puertos;
using Tiie.Aplicacion.Reloj;
using Tiie.Dominio.Citas;

namespace Tiie.Infraestructura.Citas;

/// <summary>
/// Token de acceso a la cita (RN-16): se guarda su hash para buscarlo y una copia protegida con Data Protection
/// para armar los enlaces de los correos. El token en claro nunca se guarda ni se registra.
/// </summary>
public sealed class TokensCita(IDataProtectionProvider proveedor, RelojAgenda reloj) : ITokensCita
{
    public const string Proposito = "Tiie.TokenCita.v1";

    private readonly IDataProtector _protector = proveedor.CreateProtector(Proposito);

    public TokenAcceso Emitir(DateOnly fechaCita)
    {
        var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));   // 43 caracteres
        return new TokenAcceso(token, Hash(token), _protector.Protect(token), reloj.FinDelDia(fechaCita.AddDays(1)));
    }

    public byte[] Hash(string token)
    {
        ArgumentNullException.ThrowIfNull(token);
        return SHA256.HashData(Encoding.ASCII.GetBytes(token));
    }

    public string? Recuperar(string tokenProtegido)
    {
        try
        {
            return _protector.Unprotect(tokenProtegido);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}
