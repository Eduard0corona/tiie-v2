using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace Tiie.Aplicacion.Comun;

public static class ExtensionesDatos
{
    /// <summary>
    /// La actualización chocó con el índice único <paramref name="indice"/>. SQL Server incluye el nombre del
    /// índice en el mensaje de los errores 2601 y 2627, en cualquier idioma del servidor.
    /// </summary>
    public static bool ViolaIndice(this DbUpdateException ex, string indice)
    {
        ArgumentNullException.ThrowIfNull(ex);
        return ex.InnerException is DbException interna && interna.Message.Contains(indice, StringComparison.Ordinal);
    }

    /// <summary>Ejecuta <paramref name="operacion"/> en una transacción, dentro de la estrategia de reintentos de EF.</summary>
    /// <remarks>
    /// La conexión usa <c>EnableRetryOnFailure</c>, que solo admite transacciones dentro de la estrategia. Todo lo que
    /// se agrega al contexto debe crearse dentro de la operación, para que un reintento empiece limpio.
    /// </remarks>
    public static Task<T> EnTransaccionAsync<T>(
        this Puertos.ITiieDb db, Func<CancellationToken, Task<T>> operacion, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        var estrategia = db.Database.CreateExecutionStrategy();
        return estrategia.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var resultado = await operacion(ct);
            await tx.CommitAsync(ct);
            return resultado;
        });
    }
}
