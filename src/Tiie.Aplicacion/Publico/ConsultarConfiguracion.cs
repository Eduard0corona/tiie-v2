using Microsoft.Extensions.Options;
using Tiie.Aplicacion.Opciones;

namespace Tiie.Aplicacion.Publico;

/// <summary>Valores que la SPA necesita al arrancar.</summary>
public sealed record ConfiguracionPublica(string ClaveSitioCaptcha, string VersionAvisoPrivacidad, string ZonaHoraria);

public sealed class ConsultarConfiguracionManejador(
    IOptions<OpcionesCaptcha> captcha, IOptions<OpcionesSitio> sitio, IOptions<OpcionesAgenda> agenda)
{
    public ConfiguracionPublica Ejecutar() =>
        new(captcha.Value.ClaveSitio, sitio.Value.VersionAvisoPrivacidad, agenda.Value.ZonaHoraria);
}
