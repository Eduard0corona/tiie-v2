using System.Linq.Expressions;
using Tiie.Aplicacion.Puertos;
using Tiie.Dominio.Errores;

namespace Tiie.Aplicacion.Comun;

/// <summary>Ayudas del alcance por módulo (docs/07#alcance-por-módulo).</summary>
public static class Alcance
{
    /// <summary>
    /// Filtra una consulta a los módulos del usuario. Administrador: sin filtro.
    /// </summary>
    public static IQueryable<T> SoloModulosPermitidos<T>(
        this IQueryable<T> consulta, IAutorizacionModulo alcance, Expression<Func<T, int>> moduloDe)
    {
        ArgumentNullException.ThrowIfNull(alcance);
        ArgumentNullException.ThrowIfNull(moduloDe);
        if (alcance.ModulosPermitidos is not { } permitidos)
            return consulta;

        var ids = permitidos.ToList();
        var parametro = moduloDe.Parameters[0];
        var contiene = Expression.Call(
            typeof(Enumerable), nameof(Enumerable.Contains), [typeof(int)], Expression.Constant(ids), moduloDe.Body);
        return consulta.Where(Expression.Lambda<Func<T, bool>>(contiene, parametro));
    }

    /// <summary>Solo el administrador ve todos los módulos; lo demás responde 403 <c>Prohibido</c>.</summary>
    public static void ExigirAdministrador(this IAutorizacionModulo alcance)
    {
        ArgumentNullException.ThrowIfNull(alcance);
        if (alcance.ModulosPermitidos is not null)
            throw new ReglaNegocioException(CodigoError.Prohibido);
    }
}
