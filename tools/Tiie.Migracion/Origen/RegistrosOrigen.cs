namespace Tiie.Migracion.Origen;

// Filas de la base de 2016 tal como las lee LectorOrigen. Los nombres siguen el esquema supuesto
// (esquema-supuesto.sql); los tipos ya vienen convertidos a los de la versión 2.

public sealed record EntidadOrigen(int EntidadId, string Nombre, bool Activo);

public sealed record MunicipioOrigen(int MunicipioId, int EntidadId, string Nombre);

public sealed record ModuloOrigen(
    int ModuloId,
    int MunicipioId,
    string Nombre,
    string? Calle,
    string? NumExt,
    string? NumInt,
    string? Colonia,
    string? CodigoPostal,
    string? Poblacion,
    string? Telefono,
    string? Email,
    bool Activo);

public sealed record TramiteOrigen(
    int TramiteId,
    int? ModuloId,
    string Nombre,
    string? Detalle,
    decimal? Costo,
    string? Notas,
    string? Requisitos,
    bool Activo);

public sealed record HorarioOrigen(int HorarioId, int TramiteId, TimeOnly Hora, int Turnos, bool Activo);

/// <summary>Cita con su horario. <see cref="HorarioId"/> vacío: la cita no tiene horario o el horario ya no existe.</summary>
public sealed record CitaOrigen(
    int CitaId,
    string Folio,
    string? Curp,
    string Nombre,
    string ApellidoPaterno,
    string? ApellidoMaterno,
    string? Celular,
    string? Email,
    DateOnly FechaCita,
    string Estatus,
    DateTime? FechaRegistro,
    int? HorarioId,
    int? TramiteId,
    TimeOnly? Hora);

/// <summary>Total de citas anteriores al corte por fecha y trámite. <see cref="TramiteId"/> vacío: sin horario.</summary>
public sealed record TotalHistorico(DateOnly Fecha, int? TramiteId, int Total);

public sealed record UsuarioOrigen(
    int UsuarioId,
    string Usuario,
    string? Nombre,
    string? ApellidoPaterno,
    string? ApellidoMaterno,
    string? Email,
    int Tipo,
    int? ModuloId,
    bool Activo);

/// <summary>Todo lo que la migración necesita de la base anterior.</summary>
public sealed record DatosOrigen(
    IReadOnlyList<EntidadOrigen> Entidades,
    IReadOnlyList<MunicipioOrigen> Municipios,
    IReadOnlyList<ModuloOrigen> Modulos,
    IReadOnlyList<TramiteOrigen> Tramites,
    IReadOnlyList<HorarioOrigen> Horarios,
    IReadOnlyList<CitaOrigen> CitasVigentes,
    IReadOnlyList<TotalHistorico> Historicos,
    IReadOnlyList<UsuarioOrigen> Usuarios);
