using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;

namespace Tiie.Migracion.Origen;

/// <summary>
/// Única capa que conoce el esquema de 2016. Las tablas y columnas son SUPUESTAS (esquema-supuesto.sql):
/// si el script real difiere, se ajustan aquí y en <see cref="Columnas"/>, y en ningún otro lugar.
/// Solo lee, con <c>ApplicationIntent=ReadOnly</c>; aun así, conviene usar un usuario con permisos de lectura.
/// </summary>
public sealed class LectorOrigen(string cadenaConexion)
{
    /// <summary>Tablas y columnas que leen las consultas de abajo; <c>analizar</c> las verifica antes de todo.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> Columnas = new Dictionary<string, string[]>
    {
        ["Entidad"] = ["EntidadId", "Entidad", "Estatus"],
        ["Municipio"] = ["MunicipioId", "EntidadId", "Municipio"],
        ["Modulo"] = ["ModuloId", "MunicipioId", "Nombre", "Calle", "NumExt", "NumInt", "Colonia", "CP", "Poblacion", "Telefono", "Email", "Estatus"],
        ["Tramite"] = ["TramiteId", "ModuloId", "Nombre", "Detalle", "Costo", "Notas", "Requisitos", "Estatus"],
        ["Horario"] = ["HorarioId", "TramiteId", "Hora", "Turnos", "Estatus"],
        ["Cita"] = ["CitaId", "Folio", "CURP", "Nombre", "ApellidoPaterno", "ApellidoMaterno", "Celular", "Email", "FechaCita", "Estatus", "FechaRegistro"],
        ["HorarioCita"] = ["HorarioCitaId", "CitaId", "HorarioId"],
        ["Usuario"] = ["UsuarioId", "Usuario", "Nombre", "ApellidoPaterno", "ApellidoMaterno", "Email", "Tipo", "ModuloId", "Estatus"],
    };

    // Una cita se une a su horario por HorarioCita; si hubiera varias filas, cuenta la primera.
    private const string CitaConHorario = """
        FROM dbo.Cita c
        OUTER APPLY (SELECT TOP (1) hc.HorarioId FROM dbo.HorarioCita hc WHERE hc.CitaId = c.CitaId ORDER BY hc.HorarioCitaId) x
        LEFT JOIN dbo.Horario h ON h.HorarioId = x.HorarioId
        """;

    /// <summary>Columnas que faltan respecto de <see cref="Columnas"/>, como «Tabla.Columna».</summary>
    public async Task<IReadOnlyList<string>> ColumnasFaltantesAsync(CancellationToken ct)
    {
        var existentes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await LeerAsync("SELECT TABLE_NAME, COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'dbo'", null,
            r => existentes.Add($"{r.GetString(0)}.{r.GetString(1)}"), ct);
        return [.. Columnas.SelectMany(t => t.Value.Select(c => $"{t.Key}.{c}")).Where(c => !existentes.Contains(c))];
    }

    public async Task<DatosOrigen> LeerAsync(DateOnly fechaCorte, CancellationToken ct)
    {
        var entidades = new List<EntidadOrigen>();
        await LeerAsync("SELECT EntidadId, Entidad, Estatus FROM dbo.Entidad", null,
            r => entidades.Add(new(Entero(r, 0), Texto(r, 1)!, Booleano(r, 2))), ct);

        var municipios = new List<MunicipioOrigen>();
        await LeerAsync("SELECT MunicipioId, EntidadId, Municipio FROM dbo.Municipio", null,
            r => municipios.Add(new(Entero(r, 0), Entero(r, 1), Texto(r, 2)!)), ct);

        var modulos = new List<ModuloOrigen>();
        await LeerAsync(
            "SELECT ModuloId, MunicipioId, Nombre, Calle, NumExt, NumInt, Colonia, CP, Poblacion, Telefono, Email, Estatus FROM dbo.Modulo", null,
            r => modulos.Add(new(Entero(r, 0), Entero(r, 1), Texto(r, 2)!, Texto(r, 3), Texto(r, 4), Texto(r, 5), Texto(r, 6),
                Texto(r, 7), Texto(r, 8), Texto(r, 9), Texto(r, 10), Booleano(r, 11))), ct);

        var tramites = new List<TramiteOrigen>();
        await LeerAsync("SELECT TramiteId, ModuloId, Nombre, Detalle, Costo, Notas, Requisitos, Estatus FROM dbo.Tramite", null,
            r => tramites.Add(new(Entero(r, 0), EnteroONulo(r, 1), Texto(r, 2)!, Texto(r, 3), DecimalONulo(r, 4), Texto(r, 5),
                Texto(r, 6), Booleano(r, 7))), ct);

        var horarios = new List<HorarioOrigen>();
        await LeerAsync("SELECT HorarioId, TramiteId, CAST(Hora AS time(0)), Turnos, Estatus FROM dbo.Horario", null,
            r => horarios.Add(new(Entero(r, 0), Entero(r, 1), Hora(r, 2)!.Value, Entero(r, 3), Booleano(r, 4))), ct);

        var citas = new List<CitaOrigen>();
        await LeerAsync($"""
            SELECT c.CitaId, c.Folio, c.CURP, c.Nombre, c.ApellidoPaterno, c.ApellidoMaterno, c.Celular, c.Email,
                   c.FechaCita, c.Estatus, c.FechaRegistro, h.HorarioId, h.TramiteId, CAST(h.Hora AS time(0))
            {CitaConHorario}
            WHERE UPPER(LTRIM(RTRIM(c.Estatus))) = 'VIGENTE' AND c.FechaCita >= @corte
            """, fechaCorte,
            r => citas.Add(new(Entero(r, 0), Texto(r, 1)!, Texto(r, 2), Texto(r, 3) ?? "", Texto(r, 4) ?? "", Texto(r, 5),
                Texto(r, 6), Texto(r, 7), Fecha(r, 8), Texto(r, 9)!, r.IsDBNull(10) ? null : r.GetDateTime(10),
                EnteroONulo(r, 11), EnteroONulo(r, 12), Hora(r, 13))), ct);

        // Las citas pasadas solo se cuentan: sus datos personales no salen de la base anterior.
        var historicos = new List<TotalHistorico>();
        await LeerAsync($"""
            SELECT c.FechaCita, h.TramiteId, COUNT(*)
            {CitaConHorario}
            WHERE c.FechaCita < @corte
            GROUP BY c.FechaCita, h.TramiteId
            """, fechaCorte,
            r => historicos.Add(new(Fecha(r, 0), EnteroONulo(r, 1), Entero(r, 2))), ct);

        var usuarios = new List<UsuarioOrigen>();
        await LeerAsync("SELECT UsuarioId, Usuario, Nombre, ApellidoPaterno, ApellidoMaterno, Email, Tipo, ModuloId, Estatus FROM dbo.Usuario", null,
            r => usuarios.Add(new(Entero(r, 0), Texto(r, 1)!, Texto(r, 2), Texto(r, 3), Texto(r, 4), Texto(r, 5), Entero(r, 6),
                EnteroONulo(r, 7), Booleano(r, 8))), ct);

        return new DatosOrigen(entidades, municipios, modulos, tramites, horarios, citas, historicos, usuarios);
    }

    private async Task LeerAsync(string sql, DateOnly? corte, Action<SqlDataReader> fila, CancellationToken ct)
    {
        var cadena = new SqlConnectionStringBuilder(cadenaConexion) { ApplicationIntent = ApplicationIntent.ReadOnly };
        await using var conexion = new SqlConnection(cadena.ConnectionString);
        await conexion.OpenAsync(ct);
        await using var comando = new SqlCommand(sql, conexion) { CommandTimeout = 300 };
        if (corte is { } fecha)
            comando.Parameters.Add(new SqlParameter("@corte", SqlDbType.Date) { Value = fecha.ToDateTime(TimeOnly.MinValue) });
        await using var lector = await comando.ExecuteReaderAsync(ct);
        while (await lector.ReadAsync(ct))
            fila(lector);
    }

    private static string? Texto(SqlDataReader r, int i) =>
        r.IsDBNull(i) ? null : Convert.ToString(r.GetValue(i), CultureInfo.InvariantCulture)?.Trim() is { Length: > 0 } t ? t : null;

    private static int Entero(SqlDataReader r, int i) => Convert.ToInt32(r.GetValue(i), CultureInfo.InvariantCulture);

    private static int? EnteroONulo(SqlDataReader r, int i) => r.IsDBNull(i) ? null : Entero(r, i);

    private static decimal? DecimalONulo(SqlDataReader r, int i) =>
        r.IsDBNull(i) ? null : Convert.ToDecimal(r.GetValue(i), CultureInfo.InvariantCulture);

    private static bool Booleano(SqlDataReader r, int i) => !r.IsDBNull(i) && Convert.ToBoolean(r.GetValue(i), CultureInfo.InvariantCulture);

    private static DateOnly Fecha(SqlDataReader r, int i) => DateOnly.FromDateTime(r.GetDateTime(i));

    private static TimeOnly? Hora(SqlDataReader r, int i) => r.IsDBNull(i) ? null : TimeOnly.FromTimeSpan(r.GetTimeSpan(i));
}
