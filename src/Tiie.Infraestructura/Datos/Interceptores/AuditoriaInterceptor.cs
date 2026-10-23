using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Tiie.Aplicacion.Puertos;
using Tiie.Dominio.Bitacora;
using Tiie.Dominio.Privacidad;

namespace Tiie.Infraestructura.Datos.Interceptores;

/// <summary>
/// Escribe en <c>Auditoria</c> los cambios que hace el personal sobre catálogos, horarios, cupos, citas y
/// usuarios. Los datos personales se enmascaran y los secretos nunca se registran.
/// </summary>
/// <remarks>
/// Los registros se insertan justo después de guardar, cuando ya se conocen las llaves de las filas nuevas.
/// Si el caso de uso abrió una transacción, quedan dentro de ella.
/// </remarks>
public sealed class AuditoriaInterceptor(IUsuarioActual usuarioActual, TimeProvider tiempo) : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private List<CambioPendiente>? _pendientes;
    private bool _guardandoBitacora;

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Capturar(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Capturar(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (AgregarRegistros(eventData.Context))
            GuardarBitacora(() => eventData.Context!.SaveChanges());
        return result;
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (AgregarRegistros(eventData.Context))
        {
            _guardandoBitacora = true;
            try
            {
                await eventData.Context!.SaveChangesAsync(cancellationToken);
            }
            finally
            {
                _guardandoBitacora = false;
            }
        }
        return result;
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData) => _pendientes = null;

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        _pendientes = null;
        return Task.CompletedTask;
    }

    public override void SaveChangesCanceled(DbContextEventData eventData) => _pendientes = null;

    public override Task SaveChangesCanceledAsync(DbContextEventData eventData, CancellationToken cancellationToken = default)
    {
        _pendientes = null;
        return Task.CompletedTask;
    }

    private void GuardarBitacora(Action guardar)
    {
        _guardandoBitacora = true;
        try
        {
            guardar();
        }
        finally
        {
            _guardandoBitacora = false;
        }
    }

    private void Capturar(DbContext? contexto)
    {
        _pendientes = null;
        // Solo se auditan los cambios del personal; el ciudadano y los procesos no tienen usuario.
        if (_guardandoBitacora || contexto is null || usuarioActual.UsuarioId is null)
            return;

        foreach (var entrada in contexto.ChangeTracker.Entries())
        {
            if (entrada.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
                continue;

            var tabla = entrada.Metadata.GetTableName() ?? entrada.Metadata.ClrType.Name;
            if (!PoliticaAuditoria.TablasAuditadas.Contains(tabla))
                continue;

            var cambios = Cambios(entrada, tabla);
            if (entrada.State == EntityState.Modified && cambios.Count == 0)
                continue;

            _pendientes ??= [];
            _pendientes.Add(new CambioPendiente(
                entrada,
                entrada.State,
                tabla,
                cambios,
                entrada.State == EntityState.Added ? null : Llave(entrada)));
        }
    }

    private bool AgregarRegistros(DbContext? contexto)
    {
        var pendientes = _pendientes;
        _pendientes = null;
        if (contexto is null || pendientes is null || pendientes.Count == 0)
            return false;

        var ahora = tiempo.GetUtcNow();
        foreach (var cambio in pendientes)
        {
            contexto.Add(new Auditoria
            {
                Fecha = ahora,
                UsuarioId = usuarioActual.UsuarioId,
                Accion = $"{cambio.Tabla}.{Verbo(cambio.Estado)}",
                Entidad = cambio.Tabla,
                // Las llaves de las filas nuevas se conocen hasta después de guardar.
                EntidadId = cambio.EntidadId ?? Llave(cambio.Entrada),
                Cambios = JsonSerializer.Serialize(cambio.Cambios, Json),
                Ip = usuarioActual.Ip,
            });
        }
        return true;
    }

    private static Dictionary<string, object?> Cambios(EntityEntry entrada, string tabla)
    {
        var cambios = new Dictionary<string, object?>();
        foreach (var propiedad in entrada.Properties)
        {
            var nombre = propiedad.Metadata.Name;
            if (PoliticaAuditoria.PropiedadesOmitidas.Contains(nombre) || propiedad.Metadata.IsPrimaryKey())
                continue;

            var clave = JsonNamingPolicy.CamelCase.ConvertName(nombre);
            switch (entrada.State)
            {
                case EntityState.Added:
                    cambios[clave] = PoliticaAuditoria.Valor(tabla, nombre, propiedad.CurrentValue);
                    break;
                case EntityState.Deleted:
                    cambios[clave] = PoliticaAuditoria.Valor(tabla, nombre, propiedad.OriginalValue);
                    break;
                case EntityState.Modified when propiedad.IsModified && !Equals(propiedad.OriginalValue, propiedad.CurrentValue):
                    cambios[clave] = new
                    {
                        anterior = PoliticaAuditoria.Valor(tabla, nombre, propiedad.OriginalValue),
                        nuevo = PoliticaAuditoria.Valor(tabla, nombre, propiedad.CurrentValue),
                    };
                    break;
            }
        }
        return cambios;
    }

    private static string? Llave(EntityEntry entrada)
    {
        var llave = entrada.Metadata.FindPrimaryKey();
        if (llave is null)
            return null;
        var valores = llave.Properties.Select(p => Convert.ToString(entrada.Property(p.Name).CurrentValue, CultureInfo.InvariantCulture));
        return string.Join(',', valores);
    }

    private static string Verbo(EntityState estado) => estado switch
    {
        EntityState.Added => "Crear",
        EntityState.Deleted => "Eliminar",
        _ => "Modificar",
    };

    private sealed record CambioPendiente(
        EntityEntry Entrada, EntityState Estado, string Tabla, Dictionary<string, object?> Cambios, string? EntidadId);
}

/// <summary>Qué se audita y cómo se protegen los datos personales en la bitácora.</summary>
internal static class PoliticaAuditoria
{
    /// <summary>Tablas cuyos cambios hechos por el personal se registran.</summary>
    public static readonly IReadOnlySet<string> TablasAuditadas = new HashSet<string>(StringComparer.Ordinal)
    {
        "Entidad", "Municipio", "Modulo", "TipoTramite", "Requisito", "Oferta", "PlantillaHorario",
        "Bloqueo", "Cupo", "Cita", "Solicitante", "Usuario", "UsuarioRol", "UsuarioModulo",
    };

    /// <summary>Secretos, valores técnicos y marcas de tiempo que la bitácora ya tiene.</summary>
    public static readonly IReadOnlySet<string> PropiedadesOmitidas = new HashSet<string>(StringComparer.Ordinal)
    {
        "RowVersion", "ConcurrencyStamp", "SecurityStamp", "PasswordHash", "TokenHash", "TokenProtegido",
        "AccessFailedCount", "LockoutEnd", "TwoFactorEnabled", "PhoneNumberConfirmed", "NormalizedEmail",
        "NormalizedUserName", "UltimoAccesoEn", "CreadoEn", "ModificadoEn", "CreadaEn", "ActualizadaEn",
        "ActualizadaPor",
    };

    private static readonly HashSet<string> TablasConNombresPersonales = new(StringComparer.Ordinal)
    {
        "Solicitante", "Usuario",
    };

    public static object? Valor(string tabla, string propiedad, object? valor)
    {
        if (valor is not string texto)
            return valor;

        return propiedad switch
        {
            "Curp" => Enmascarar.Curp(texto),
            "Correo" or "Email" or "UserName" when texto.Contains('@', StringComparison.Ordinal) => Enmascarar.Correo(texto),
            "Correo" or "Email" => Enmascarar.Correo(texto),
            "Celular" or "PhoneNumber" => Enmascarar.Celular(texto),
            "Nombre" or "ApellidoPaterno" or "ApellidoMaterno" when TablasConNombresPersonales.Contains(tabla) =>
                Enmascarar.Nombre(texto),
            _ => texto,
        };
    }
}
