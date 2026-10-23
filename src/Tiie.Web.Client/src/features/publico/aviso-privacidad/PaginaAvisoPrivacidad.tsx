import { Alerta } from '@/components/ui/Alerta';
import { Contenedor } from '@/components/ui/Contenedor';
import { useConfiguracion } from '../comun/usePublico';

/**
 * Aviso de privacidad integral (RF-08). El texto es provisional: el definitivo lo redacta el área jurídica y se
 * publica con una versión nueva en Sitio:VersionAvisoPrivacidad (docs/11).
 */
export function PaginaAvisoPrivacidad() {
  const configuracion = useConfiguracion();
  const version = configuracion.data?.versionAvisoPrivacidad;

  return (
    <Contenedor ancho="angosto" className="py-10 sm:py-14">
      <article aria-labelledby="titulo-aviso" className="flex flex-col gap-6 text-slate-800">
        <header className="flex flex-col gap-2">
          <h1 id="titulo-aviso" className="text-3xl font-bold tracking-tight text-slate-950 sm:text-4xl">
            Aviso de privacidad integral
          </h1>
          {version && <p className="text-sm text-slate-600">Versión vigente: {version}</p>}
        </header>
        <Alerta titulo="Texto provisional">
          El texto definitivo de este aviso lo redacta el área jurídica de la dependencia responsable. Lo que
          sigue describe cómo funciona el sistema y se reemplazará antes de abrir la agenda al público.
        </Alerta>

        <Seccion titulo="¿Quién es responsable de tus datos?">
          El Gobierno del Estado de Sinaloa, a través de la dependencia que opera la Tarjeta Inteligente de
          Identidad Estudiantil (TIIE), es responsable del tratamiento de los datos personales que
          proporcionas en este sitio.
        </Seccion>
        <Seccion titulo="¿Qué datos usamos?">
          CURP, nombre y apellidos, celular y correo electrónico. La fecha de nacimiento y el sexo se obtienen
          de la CURP. No pedimos documentos, domicilio ni datos de la madre, el padre o el tutor.
        </Seccion>
        <Seccion titulo="¿Para qué los usamos?">
          Para agendar y atender tu cita del trámite de la TIIE, enviarte el comprobante, recordatorios y
          avisos sobre tu cita, y elaborar estadísticas sin datos que te identifiquen.
        </Seccion>
        <Seccion titulo="¿Con quién los compartimos?">
          Solo el personal del módulo donde agendaste puede ver tus datos para atenderte. No se transfieren a
          terceros salvo las excepciones que marca la ley.
        </Seccion>
        <Seccion titulo="¿Cómo ejerces tus derechos ARCO?">
          Puedes solicitar el acceso, la rectificación, la cancelación o la oposición al tratamiento de tus
          datos ante la unidad de transparencia de la dependencia responsable. El procedimiento y los medios
          de contacto se publicarán en el aviso definitivo.
        </Seccion>
        <Seccion titulo="¿Cómo te avisaremos de cambios?">
          Cualquier cambio a este aviso se publicará en esta página con una versión nueva. Cada cita guarda la
          versión que aceptaste.
        </Seccion>
      </article>
    </Contenedor>
  );
}

function Seccion({ titulo, children }: { titulo: string; children: string }) {
  return (
    <section className="flex flex-col gap-2 border-t border-slate-200 pt-6">
      <h2 className="text-xl font-bold text-slate-950">{titulo}</h2>
      <p className="text-lg leading-relaxed text-slate-700">{children}</p>
    </section>
  );
}
