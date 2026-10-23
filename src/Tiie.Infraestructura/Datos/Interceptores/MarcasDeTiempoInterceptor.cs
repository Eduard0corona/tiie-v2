using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Tiie.Dominio.Comun;

namespace Tiie.Infraestructura.Datos.Interceptores;

/// <summary>Llena <c>CreadoEn</c> y <c>ModificadoEn</c> con <see cref="TimeProvider"/>, en UTC.</summary>
public sealed class MarcasDeTiempoInterceptor(TimeProvider tiempo) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Marcar(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Marcar(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void Marcar(DbContext? contexto)
    {
        if (contexto is null)
            return;

        var ahora = tiempo.GetUtcNow();
        foreach (var entrada in contexto.ChangeTracker.Entries<IConFechaCreacion>())
        {
            if (entrada.State == EntityState.Added && entrada.Entity.CreadoEn == default)
                entrada.Entity.CreadoEn = ahora;
            else if (entrada.State == EntityState.Modified && entrada.Entity is IConMarcasDeTiempo conModificacion)
                conModificacion.ModificadoEn = ahora;
        }
    }
}
