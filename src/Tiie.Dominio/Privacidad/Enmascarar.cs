namespace Tiie.Dominio.Privacidad;

/// <summary>
/// Enmascarado de datos personales para pantallas del ciudadano, logs y auditoría.
/// Nunca se registra una CURP, nombre, correo o celular sin pasar por aquí.
/// </summary>
public static class Enmascarar
{
    private const char Asterisco = '*';

    /// <summary><c>GOMA080515MSLRRNA5</c> → <c>GOMA************A5</c>.</summary>
    public static string Curp(string? curp)
    {
        if (string.IsNullOrEmpty(curp))
            return string.Empty;
        if (curp.Length <= 6)
            return new string(Asterisco, curp.Length);
        return string.Concat(curp.AsSpan(0, 4), new string(Asterisco, curp.Length - 6), curp.AsSpan(curp.Length - 2));
    }

    /// <summary><c>ana@example.com</c> → <c>a***@example.com</c>.</summary>
    public static string Correo(string? correo)
    {
        if (string.IsNullOrEmpty(correo))
            return string.Empty;
        var arroba = correo.LastIndexOf('@');
        if (arroba <= 0)
            return "***";
        return string.Concat(correo.AsSpan(0, 1), "***", correo.AsSpan(arroba));
    }

    /// <summary><c>6671234567</c> → <c>******4567</c>.</summary>
    public static string Celular(string? celular)
    {
        if (string.IsNullOrEmpty(celular))
            return string.Empty;
        if (celular.Length <= 4)
            return new string(Asterisco, celular.Length);
        return string.Concat(new string(Asterisco, celular.Length - 4), celular.AsSpan(celular.Length - 4));
    }

    /// <summary><c>Ana Gómez</c> → <c>A*** G***</c>.</summary>
    public static string Nombre(string? nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            return string.Empty;
        var palabras = nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(' ', palabras.Select(p => string.Concat(p.AsSpan(0, 1), "***")));
    }
}
