using System.Text;

namespace Tiie.Migracion.Reportes;

/// <summary>Un CSV de hallazgos para operación. Puede contener datos personales (docs/14#herramienta).</summary>
public sealed class Reporte(string archivo, params string[] encabezados)
{
    private readonly List<string?[]> _filas = [];

    public string Archivo { get; } = archivo;

    public IReadOnlyList<string> Encabezados { get; } = encabezados;

    public IReadOnlyList<string?[]> Filas => _filas;

    public void Agregar(params string?[] fila)
    {
        if (fila.Length != Encabezados.Count)
            throw new ArgumentException($"{Archivo} espera {Encabezados.Count} columnas.", nameof(fila));
        _filas.Add(fila);
    }

    /// <summary>CSV en UTF-8 con BOM, para que Excel muestre bien los acentos.</summary>
    public async Task GuardarAsync(string carpeta, CancellationToken ct)
    {
        var texto = new StringBuilder();
        texto.AppendLine(string.Join(',', Encabezados.Select(Celda)));
        foreach (var fila in _filas)
            texto.AppendLine(string.Join(',', fila.Select(Celda)));
        await File.WriteAllTextAsync(Path.Combine(carpeta, Archivo), texto.ToString(), new UTF8Encoding(true), ct);
    }

    private static string Celda(string? valor) =>
        valor is null ? "" : valor.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? $"\"{valor.Replace("\"", "\"\"", StringComparison.Ordinal)}\"" : valor;
}
