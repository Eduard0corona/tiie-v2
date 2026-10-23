using System.Globalization;
using System.Text;

namespace Tiie.Aplicacion.Comun;

/// <summary>Textos para correos, comprobantes y reportes, en español de México.</summary>
public static class Formatos
{
    public static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-MX");

    /// <summary><c>2026-11-12</c> → «jueves 12 de noviembre de 2026».</summary>
    public static string FechaLarga(DateOnly fecha) => fecha.ToString("dddd d 'de' MMMM 'de' yyyy", Cultura);

    /// <summary><c>10:30</c>.</summary>
    public static string Hora(TimeOnly hora) => hora.ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary><c>12/11/2026</c>.</summary>
    public static string FechaCorta(DateOnly fecha) => fecha.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    /// <summary>«Av. Ejemplo 123 int. 4, Col. Centro, C.P. 80000».</summary>
    public static string Direccion(string calle, string numeroExterior, string? numeroInterior, string colonia, string codigoPostal)
    {
        var sb = new StringBuilder();
        sb.Append(calle).Append(' ').Append(numeroExterior);
        if (!string.IsNullOrWhiteSpace(numeroInterior))
            sb.Append(" int. ").Append(numeroInterior);
        sb.Append(", Col. ").Append(colonia).Append(", C.P. ").Append(codigoPostal);
        return sb.ToString();
    }

    public static string NombreCompleto(string nombre, string apellidoPaterno, string? apellidoMaterno) =>
        string.Join(' ', new[] { nombre, apellidoPaterno, apellidoMaterno }.Where(p => !string.IsNullOrWhiteSpace(p)));
}
