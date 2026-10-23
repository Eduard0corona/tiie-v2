using Microsoft.EntityFrameworkCore;
using Tiie.Aplicacion.Puertos;
using Tiie.Dominio.Solicitantes;

namespace Tiie.Aplicacion.Citas;

/// <summary>Datos personales capturados en una reserva (en línea o en ventanilla).</summary>
public sealed record DatosSolicitante(
    string Nombre, string ApellidoPaterno, string? ApellidoMaterno, string? Celular, string? Correo);

public static class Solicitantes
{
    /// <summary>
    /// Busca al solicitante por su CURP y actualiza su contacto con el de esta reserva, o lo crea (RN-15).
    /// Un correo o celular vacío en ventanilla no borra el que ya tenía.
    /// </summary>
    public static async Task<Solicitante> ObtenerOActualizarAsync(
        ITiieDb db, DatosCurp curp, DatosSolicitante datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(curp);
        ArgumentNullException.ThrowIfNull(datos);

        var solicitante = await db.Solicitantes.SingleOrDefaultAsync(s => s.Curp == curp.Curp, ct);
        if (solicitante is null)
        {
            solicitante = new Solicitante
            {
                Curp = curp.Curp,
                Nombre = datos.Nombre.Trim(),
                ApellidoPaterno = datos.ApellidoPaterno.Trim(),
            };
            db.Solicitantes.Add(solicitante);
        }

        solicitante.Nombre = datos.Nombre.Trim();
        solicitante.ApellidoPaterno = datos.ApellidoPaterno.Trim();
        solicitante.ApellidoMaterno = string.IsNullOrWhiteSpace(datos.ApellidoMaterno) ? null : datos.ApellidoMaterno.Trim();
        solicitante.FechaNacimiento = curp.FechaNacimiento;
        solicitante.Sexo = curp.Sexo;
        if (!string.IsNullOrWhiteSpace(datos.Celular))
            solicitante.Celular = datos.Celular.Trim();
        if (!string.IsNullOrWhiteSpace(datos.Correo))
            solicitante.Correo = datos.Correo.Trim();
        return solicitante;
    }
}
