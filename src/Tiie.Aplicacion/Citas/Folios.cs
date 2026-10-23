using Microsoft.EntityFrameworkCore;
using Tiie.Aplicacion.Puertos;
using Tiie.Dominio.Citas;

namespace Tiie.Aplicacion.Citas;

public static class Folios
{
    /// <summary>Siguiente folio con la fecha local de creación y la secuencia <c>SecuenciaFolio</c> (RN-10).</summary>
    public static async Task<string> SiguienteAsync(ITiieDb db, DateOnly hoyLocal, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        // NEXT VALUE FOR no se permite en subconsultas: no se compone la consulta.
        var numero = (await db.Database
            .SqlQuery<long>($"SELECT NEXT VALUE FOR dbo.SecuenciaFolio AS Value")
            .ToListAsync(ct)).Single();
        return Folio.Formatear(hoyLocal, numero);
    }
}
