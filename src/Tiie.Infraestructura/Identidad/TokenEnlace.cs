using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace Tiie.Infraestructura.Identidad;

/// <summary>Tokens de Identity en enlaces: Base64 URL para que sobrevivan en la cadena de consulta.</summary>
public static class TokenEnlace
{
    public static string Codificar(string token) => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

    public static string? Decodificar(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;
        try
        {
            return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
