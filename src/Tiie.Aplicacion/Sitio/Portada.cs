using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tiie.Aplicacion.Bitacora;
using Tiie.Aplicacion.Comun;
using Tiie.Aplicacion.Puertos;
using Tiie.Aplicacion.Reloj;
using Tiie.Dominio.Errores;
using Tiie.Dominio.Sitio;

namespace Tiie.Aplicacion.Sitio;

public sealed record AvisoPortada(string Texto, TonoAviso Tono);

public sealed record PreguntaPortada(string Pregunta, string Respuesta);

/// <summary>Lo que muestra la portada: el aviso solo llega si está vigente hoy.</summary>
public sealed record PortadaPublica(
    string Etiqueta, string Titulo, string? TituloDestacado, string Descripcion, AvisoPortada? Aviso,
    IReadOnlyList<PreguntaPortada> Preguntas);

/// <summary>Contenido para editar. <c>Version</c> vacía: nunca se ha guardado y se muestra el predeterminado.</summary>
public sealed record PortadaAdmin(
    string Etiqueta, string Titulo, string? TituloDestacado, string Descripcion,
    string? AvisoTexto, TonoAviso AvisoTono, DateOnly? AvisoDesde, DateOnly? AvisoHasta, EstadoAviso EstadoAviso,
    IReadOnlyList<PreguntaPortada> Preguntas, string? Version, DateTimeOffset? ModificadoEn);

/// <summary>Contenido completo; las preguntas se reemplazan en el orden recibido. <c>Version</c> es la que se leyó.</summary>
public sealed record DatosPortada(
    string Etiqueta, string Titulo, string? TituloDestacado, string Descripcion,
    string? AvisoTexto, TonoAviso AvisoTono, DateOnly? AvisoDesde, DateOnly? AvisoHasta,
    IReadOnlyList<PreguntaPortada> Preguntas, string? Version);

public sealed class DatosPortadaValidador : AbstractValidator<DatosPortada>
{
    public const int PreguntasMaximas = 20;
    private const string MensajeEnlaces = "Los enlaces deben empezar con /, https://, mailto: o tel:.";

    public DatosPortadaValidador()
    {
        RuleFor(x => x.Etiqueta).NotEmpty().WithMessage("Escribe la etiqueta.").MaximumLength(80);
        RuleFor(x => x.Titulo).NotEmpty().WithMessage("Escribe el título.").MaximumLength(120);
        RuleFor(x => x.TituloDestacado).MaximumLength(120);
        RuleFor(x => x.Descripcion).NotEmpty().WithMessage("Escribe la descripción.").MaximumLength(300);
        RuleFor(x => x.AvisoTexto).MaximumLength(300)
            .Must(ReglasPortada.EnlacesPermitidos).WithMessage(MensajeEnlaces);
        RuleFor(x => x.AvisoTexto).NotEmpty().WithMessage("Escribe el aviso o borra sus fechas.")
            .When(x => x.AvisoDesde is not null || x.AvisoHasta is not null);
        RuleFor(x => x.AvisoTono).IsInEnum();
        RuleFor(x => x.AvisoHasta).GreaterThanOrEqualTo(x => x.AvisoDesde)
            .WithMessage("La fecha final no puede ser anterior a la inicial.")
            .When(x => x.AvisoDesde is not null && x.AvisoHasta is not null);
        RuleFor(x => x.Preguntas).NotNull()
            .Must(p => p.Count <= PreguntasMaximas).WithMessage($"Agrega {PreguntasMaximas} preguntas o menos.");
        RuleForEach(x => x.Preguntas).ChildRules(p =>
        {
            p.RuleFor(x => x.Pregunta).NotEmpty().WithMessage("Escribe la pregunta.").MaximumLength(200);
            p.RuleFor(x => x.Respuesta).NotEmpty().WithMessage("Escribe la respuesta.").MaximumLength(1500)
                .Must(ReglasPortada.EnlacesPermitidos).WithMessage(MensajeEnlaces);
        });
    }
}

/// <summary>
/// Contenido de la portada (RF-01): lo lee el sitio público y lo edita el administrador. Mientras no se guarde,
/// se usa <see cref="PortadaPredeterminada"/>.
/// </summary>
public sealed class PortadaManejador(ITiieDb db, RelojAgenda reloj, IValidator<DatosPortada> validador, RegistroAuditoria auditoria)
{
    private const string LlavePrimaria = "PK_ContenidoPortada";

    public async Task<PortadaPublica> PublicaAsync(CancellationToken ct)
    {
        var c = await LeerAsync(ct) ?? PortadaPredeterminada.Crear();
        var aviso = ReglasPortada.Estado(c.AvisoTexto, c.AvisoDesde, c.AvisoHasta, reloj.HoyLocal()) == EstadoAviso.Visible
            ? new AvisoPortada(c.AvisoTexto!, c.AvisoTono)
            : null;
        return new PortadaPublica(c.Etiqueta, c.Titulo, c.TituloDestacado, c.Descripcion, aviso, Preguntas(c));
    }

    public async Task<PortadaAdmin> ObtenerAsync(CancellationToken ct)
    {
        var guardado = await LeerAsync(ct);
        var c = guardado ?? PortadaPredeterminada.Crear();
        return new PortadaAdmin(
            c.Etiqueta, c.Titulo, c.TituloDestacado, c.Descripcion,
            c.AvisoTexto, c.AvisoTono, c.AvisoDesde, c.AvisoHasta,
            ReglasPortada.Estado(c.AvisoTexto, c.AvisoDesde, c.AvisoHasta, reloj.HoyLocal()),
            Preguntas(c), guardado is null ? null : Versiones.ATexto(guardado.RowVersion), guardado?.ModificadoEn);
    }

    /// <summary>Guarda todo el contenido. Si alguien más guardó después de leerlo, responde 409 <c>Conflicto</c>.</summary>
    public async Task<PortadaAdmin> GuardarAsync(DatosPortada datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        await validador.ValidateAndThrowAsync(datos, ct);
        var version = datos.Version is null ? null : Versiones.DeTexto(datos.Version);

        var contenido = await db.ContenidosPortada.Include(c => c.Preguntas).SingleOrDefaultAsync(ct);
        // Sin versión se crea por primera vez; con versión se actualiza lo que se leyó.
        if ((contenido is null) != (version is null))
            throw new ReglaNegocioException(CodigoError.Conflicto);

        var anterior = Resumen(contenido ?? PortadaPredeterminada.Crear());
        if (contenido is null)
        {
            contenido = new ContenidoPortada { Etiqueta = "", Titulo = "", Descripcion = "" };
            db.ContenidosPortada.Add(contenido);
        }
        else
        {
            db.Entry(contenido).Property(c => c.RowVersion).OriginalValue = version!;
            // Aunque solo cambien las preguntas, la fila cambia de versión para detectar ediciones simultáneas.
            db.Entry(contenido).State = EntityState.Modified;
        }
        Aplicar(contenido, datos);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex is not DbUpdateConcurrencyException && ex.ViolaIndice(LlavePrimaria))
        {
            throw new ReglaNegocioException(CodigoError.Conflicto);           // dos administradores guardaron por primera vez
        }

        var nuevo = Resumen(contenido);
        await auditoria.RegistrarAsync("Portada.Guardar", nameof(ContenidoPortada), ContenidoPortada.Unico.ToString(System.Globalization.CultureInfo.InvariantCulture),
            new
            {
                cambios = anterior.Where(a => !Equals(a.Value, nuevo[a.Key]))
                    .ToDictionary(a => a.Key, a => new { anterior = a.Value, nuevo = nuevo[a.Key] }),
                preguntas = contenido.Preguntas.Count,
            }, null, ct);
        return await ObtenerAsync(ct);
    }

    private async Task<ContenidoPortada?> LeerAsync(CancellationToken ct) =>
        await db.ContenidosPortada.AsNoTracking().Include(c => c.Preguntas).SingleOrDefaultAsync(ct);

    private static List<PreguntaPortada> Preguntas(ContenidoPortada c) =>
        [.. c.Preguntas.OrderBy(p => p.Orden).Select(p => new PreguntaPortada(p.Pregunta, p.Respuesta))];

    private void Aplicar(ContenidoPortada c, DatosPortada d)
    {
        c.Etiqueta = d.Etiqueta.Trim();
        c.Titulo = d.Titulo.Trim();
        c.TituloDestacado = Texto(d.TituloDestacado);
        c.Descripcion = d.Descripcion.Trim();
        c.AvisoTexto = Texto(d.AvisoTexto);
        c.AvisoTono = d.AvisoTono;
        c.AvisoDesde = c.AvisoTexto is null ? null : d.AvisoDesde;
        c.AvisoHasta = c.AvisoTexto is null ? null : d.AvisoHasta;
        db.PreguntasFrecuentes.RemoveRange(c.Preguntas);
        c.Preguntas.Clear();
        for (var i = 0; i < d.Preguntas.Count; i++)
        {
            c.Preguntas.Add(new PreguntaFrecuente
            {
                Pregunta = d.Preguntas[i].Pregunta.Trim(),
                Respuesta = d.Preguntas[i].Respuesta.Trim(),
                Orden = (short)(i + 1),
            });
        }
    }

    private static string? Texto(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    /// <summary>Campos del encabezado y del aviso para el resumen de la bitácora; no hay datos personales.</summary>
    private static Dictionary<string, object?> Resumen(ContenidoPortada c) => new()
    {
        ["etiqueta"] = c.Etiqueta,
        ["titulo"] = c.Titulo,
        ["tituloDestacado"] = c.TituloDestacado,
        ["descripcion"] = c.Descripcion,
        ["avisoTexto"] = c.AvisoTexto,
        ["avisoTono"] = c.AvisoTono.ToString(),
        ["avisoDesde"] = c.AvisoDesde,
        ["avisoHasta"] = c.AvisoHasta,
    };
}
