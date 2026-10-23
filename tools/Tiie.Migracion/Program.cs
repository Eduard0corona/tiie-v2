namespace Tiie.Migracion;

// Main explícito: un Program global chocaría con el de Tiie.Web en las pruebas de integración.
internal static class Programa
{
    private static async Task<int> Main(string[] args)
    {
        using var cancelacion = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            // Ctrl+C cancela con orden: la transacción del cupo en curso se revierte y lo migrado se conserva.
            e.Cancel = true;
            cancelacion.Cancel();
        };
        return await Consola.EjecutarAsync(args, Console.Out, Console.Error, cancelacion.Token);
    }
}
