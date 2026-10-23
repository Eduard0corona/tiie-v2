using Tiie.Dominio.Solicitantes;

namespace Tiie.Migracion.Plan;

/// <summary>
/// <c>decisiones-duplicados.csv</c>: para cada CURP con varias citas futuras, el folio anterior que operación
/// decidió conservar (RN-01). Columnas <c>Curp,FolioConservar</c>; la primera fila puede ser el encabezado.
/// </summary>
public static class Decisiones
{
    public static IReadOnlyDictionary<string, string> Vacias { get; } = new Dictionary<string, string>();

    public static IReadOnlyDictionary<string, string> Leer(string ruta)
    {
        var decisiones = new Dictionary<string, string>(StringComparer.Ordinal);
        var numero = 0;
        foreach (var linea in File.ReadLines(ruta))
        {
            numero++;
            if (string.IsNullOrWhiteSpace(linea))
                continue;
            var columnas = linea.Split(',', StringSplitOptions.TrimEntries);
            if (numero == 1 && columnas[0].Equals("Curp", StringComparison.OrdinalIgnoreCase))
                continue;
            if (columnas.Length < 2 || columnas[0].Length == 0 || columnas[1].Length == 0)
                throw new FormatException($"{ruta}, línea {numero}: se esperaba Curp,FolioConservar.");
            if (!decisiones.TryAdd(Curp.Normalizar(columnas[0]), columnas[1].Trim('"')))
                throw new FormatException($"{ruta}, línea {numero}: la CURP ya tiene una decisión.");
        }
        return decisiones;
    }
}
