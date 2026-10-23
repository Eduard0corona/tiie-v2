namespace Tiie.Aplicacion.Puertos;

/// <summary>Quién hace la petición actual. Fuera de una petición (procesos), todo es vacío.</summary>
public interface IUsuarioActual
{
    /// <summary>Usuario del personal con sesión; vacío para el ciudadano o el sistema.</summary>
    int? UsuarioId { get; }

    /// <summary>IP de origen, ya resuelta por los encabezados reenviados del proxy.</summary>
    string? Ip { get; }
}
