using System.Globalization;

namespace Tiie.Dominio.Citas;

/// <summary>Folio legible de la cita: <c>AAMMDD-NNNNNNN</c> (RN-10).</summary>
public static class Folio
{
    /// <summary>Primer valor de la secuencia; los folios de 2016 tenían de 1 a 6 dígitos y nunca chocan.</summary>
    public const long PrimerNumero = 1_000_000;

    /// <param name="fechaLocal">Fecha local de creación (zona de negocio).</param>
    /// <param name="numero">Siguiente valor de <c>SecuenciaFolio</c>.</param>
    public static string Formatear(DateOnly fechaLocal, long numero) =>
        string.Create(CultureInfo.InvariantCulture, $"{fechaLocal:yyMMdd}-{numero:D7}");
}
