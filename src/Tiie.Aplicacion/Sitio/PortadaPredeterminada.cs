using Tiie.Dominio.Sitio;

namespace Tiie.Aplicacion.Sitio;

/// <summary>
/// Contenido de la portada mientras el administrador no guarde el suyo; es el texto con el que se diseñó
/// (docs/16#contenido-editable). No se siembra en la base: así un cambio aquí no pisa lo que ya se editó.
/// </summary>
public static class PortadaPredeterminada
{
    private static readonly (string Pregunta, string Respuesta)[] Preguntas =
    [
        ("¿Necesito crear una cuenta?",
            "No. Solo necesitas la CURP de la persona que hará el trámite y un correo electrónico. Ahí te enviamos el comprobante y un enlace privado para consultar tu cita."),
        ("¿Puedo agendar la cita de mi hija o hijo?",
            "Sí. Escribe los datos de la persona estudiante; el correo y el celular pueden ser de su madre, padre o tutor."),
        ("¿Puedo cambiar o cancelar mi cita?",
            "Sí. Abre el enlace que te enviamos por correo y elige «Cambiar fecha u hora» o «Cancelar cita». En tu cita verás hasta qué día puedes hacerlo en línea."),
        ("¿Cuántas citas puedo tener?",
            "Cada CURP puede tener una sola cita agendada a la vez. Si ya tienes una, cámbiala en lugar de agendar otra."),
        ("No encuentro el correo de mi cita",
            "Revisa la carpeta de correo no deseado. Si no está, te lo enviamos de nuevo con la CURP y el correo que registraste: [recupera tu enlace](/recuperar)."),
        ("¿Qué pasa si no llego a mi cita?",
            "Tu lugar se pierde y la cita queda como no asistida. Puedes agendar una nueva cuando quieras; si sabes que no podrás ir, cancélala para que alguien más use ese lugar."),
        ("¿Dónde consulto mi CURP?",
            "En el sitio oficial del Registro Nacional de Población: [gob.mx/curp](https://www.gob.mx/curp/)."),
        ("¿Mis datos están protegidos?",
            "Usamos tus datos solo para agendar y atender tu cita. Nadie puede ver tu cita solo con tu CURP: el acceso es con el enlace privado de tu correo. Consulta el [aviso de privacidad](/aviso-de-privacidad)."),
    ];

    /// <summary>Una instancia nueva, sin guardar.</summary>
    public static ContenidoPortada Crear()
    {
        var contenido = new ContenidoPortada
        {
            Etiqueta = "Trámite presencial en los módulos de Sinaloa",
            Titulo = "Agenda tu cita para la",
            TituloDestacado = "Tarjeta Inteligente de Identidad Estudiantil",
            Descripcion = "Elige la sede, el día y la hora desde tu celular. Te enviamos el comprobante y un enlace para consultar, cambiar o cancelar tu cita.",
        };
        for (var i = 0; i < Preguntas.Length; i++)
            contenido.Preguntas.Add(new PreguntaFrecuente { Pregunta = Preguntas[i].Pregunta, Respuesta = Preguntas[i].Respuesta, Orden = (short)(i + 1) });
        return contenido;
    }
}
