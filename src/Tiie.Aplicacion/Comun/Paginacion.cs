using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace Tiie.Aplicacion.Comun;

/// <summary>Página de resultados: <c>{ elementos, total, pagina, tamano }</c> (docs/06#convenciones).</summary>
public sealed record ListaPaginada<T>(IReadOnlyList<T> Elementos, int Total, int Pagina, int Tamano);

/// <summary>Parámetros de paginación ya normalizados: página desde 1 y tamaño entre 1 y 200.</summary>
public readonly record struct Paginado(int Pagina, int Tamano)
{
    public const int TamanoPredeterminado = 50;
    public const int TamanoMaximo = 200;

    public static Paginado De(int? pagina, int? tamano) =>
        new(Math.Max(1, pagina ?? 1), Math.Clamp(tamano ?? TamanoPredeterminado, 1, TamanoMaximo));

    public async Task<ListaPaginada<T>> AplicarAsync<T>(IQueryable<T> consulta, CancellationToken ct)
    {
        var total = await consulta.CountAsync(ct);
        var elementos = await consulta.Skip((Pagina - 1) * Tamano).Take(Tamano).ToListAsync(ct);
        return new ListaPaginada<T>(elementos, total, Pagina, Tamano);
    }
}

/// <summary>
/// Concurrencia optimista: los recursos editables viajan con <c>version</c>, el <c>RowVersion</c> en Base64.
/// Si cambió, guardar lanza <c>DbUpdateConcurrencyException</c> y la API responde 409 <c>Conflicto</c>.
/// </summary>
public static class Versiones
{
    public static string ATexto(byte[] rowVersion) => Convert.ToBase64String(rowVersion);

    /// <exception cref="ValidationException">La versión no es Base64 válido.</exception>
    public static byte[] DeTexto(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
            throw Validaciones.Error("version", "Falta la versión del registro.");
        try
        {
            return Convert.FromBase64String(version);
        }
        catch (FormatException)
        {
            throw Validaciones.Error("version", "La versión del registro no es válida.");
        }
    }
}

public static class Validaciones
{
    public static ValidationException Error(string campo, string mensaje) => new([new ValidationFailure(campo, mensaje)]);
}
