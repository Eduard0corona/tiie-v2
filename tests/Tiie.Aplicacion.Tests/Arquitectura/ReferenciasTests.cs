using System.Reflection;
using Tiie.Aplicacion;
using Tiie.Dominio.Solicitantes;

namespace Tiie.Aplicacion.Tests.Arquitectura;

/// <summary>
/// Fronteras entre proyectos (docs/04-arquitectura.md): se revisan las referencias de cada ensamblado
/// compilado, que es lo que de verdad puede usar su código.
/// </summary>
public sealed class ReferenciasTests
{
    private static readonly Assembly Dominio = typeof(Curp).Assembly;
    private static readonly Assembly Aplicacion = typeof(ExtensionesAplicacion).Assembly;

    [Fact]
    public void Dominio_no_referencia_otros_ensamblados()
    {
        var ajenas = Dominio.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(n => n is not ("System.Runtime" or "netstandard") && !n.StartsWith("System.", StringComparison.Ordinal))
            .ToList();

        ajenas.ShouldBeEmpty();
    }

    [Fact]
    public void Aplicacion_no_referencia_infraestructura_ni_web()
    {
        var referencias = Aplicacion.GetReferencedAssemblies().Select(a => a.Name!).ToList();

        referencias.ShouldNotContain("Tiie.Infraestructura");
        referencias.ShouldNotContain("Tiie.Web");
        referencias.ShouldNotContain(n => n.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
        referencias.ShouldNotContain(n => n.StartsWith("Microsoft.EntityFrameworkCore.SqlServer", StringComparison.Ordinal));
    }

    [Fact]
    public void Las_clases_concretas_son_sealed()
    {
        var abiertas = new[] { Dominio, Aplicacion }
            .SelectMany(a => a.GetTypes())
            .Where(t => t is { IsClass: true, IsAbstract: false, IsSealed: false, IsNested: false }
                && t.Namespace?.StartsWith("Tiie.", StringComparison.Ordinal) == true
                && t.GetCustomAttribute<System.Runtime.CompilerServices.CompilerGeneratedAttribute>() is null)
            .Select(t => t.FullName)
            .ToList();

        abiertas.ShouldBeEmpty();
    }
}
