using Microsoft.AspNetCore.Identity;
using Tiie.Dominio.Comun;

namespace Tiie.Infraestructura.Identidad;

/// <summary>Usuario del personal. Las cuentas solo las crea un administrador.</summary>
public sealed class Usuario : IdentityUser<int>, IConFechaCreacion
{
    public required string Nombre { get; set; }
    public required string ApellidoPaterno { get; set; }
    public string? ApellidoMaterno { get; set; }

    /// <summary>Un usuario inactivo no puede iniciar sesión y sus sesiones dejan de valer.</summary>
    public bool Activo { get; set; } = true;

    public DateTimeOffset CreadoEn { get; set; }
    public DateTimeOffset? UltimoAccesoEn { get; set; }

    public ICollection<UsuarioModulo> Modulos { get; } = [];

    public string NombreCompleto =>
        string.Join(' ', new[] { Nombre, ApellidoPaterno, ApellidoMaterno }.Where(p => !string.IsNullOrWhiteSpace(p)));
}

/// <summary>Rol del personal: Administrador, Supervisor o Capturista.</summary>
public sealed class Rol : IdentityRole<int>
{
    public Rol() { }

    public Rol(string nombre)
        : base(nombre) { }
}

/// <summary>Módulo asignado a un usuario. Los administradores no necesitan filas: ven todos.</summary>
public sealed class UsuarioModulo
{
    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;
    public int ModuloId { get; set; }
}
