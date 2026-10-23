using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;

namespace Tiie.Integracion.Tests.Infraestructura;

/// <summary>Código de 6 dígitos de una app autenticadora (RFC 6238), como lo calcula un celular.</summary>
public static class Totp
{
    private const string Alfabeto = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string Generar(string claveBase32, DateTimeOffset instante)
    {
        ArgumentNullException.ThrowIfNull(claveBase32);
        var clave = Base32(claveBase32.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant());
        Span<byte> contador = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(contador, instante.ToUnixTimeSeconds() / 30);
        // RFC 6238 usa HMAC-SHA1; es lo que implementan Identity y las apps autenticadoras.
#pragma warning disable CA5350
        var hash = HMACSHA1.HashData(clave, contador);
#pragma warning restore CA5350
        var desplazamiento = hash[^1] & 0x0f;
        var valor = (BinaryPrimitives.ReadInt32BigEndian(hash.AsSpan(desplazamiento)) & 0x7fffffff) % 1_000_000;
        return valor.ToString("D6", CultureInfo.InvariantCulture);
    }

    private static byte[] Base32(string texto)
    {
        var bytes = new List<byte>();
        int acumulado = 0, bits = 0;
        foreach (var c in texto.TrimEnd('='))
        {
            acumulado = (acumulado << 5) | Alfabeto.IndexOf(c, StringComparison.Ordinal);
            bits += 5;
            if (bits >= 8)
            {
                bytes.Add((byte)(acumulado >> (bits - 8)));
                bits -= 8;
            }
        }
        return [.. bytes];
    }
}
