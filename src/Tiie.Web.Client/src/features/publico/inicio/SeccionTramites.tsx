import { mensajeDeError } from '@/api/errores';
import { Requisitos } from '@/components/Requisitos';
import { Alerta } from '@/components/ui/Alerta';
import { Contenedor } from '@/components/ui/Contenedor';
import { Icono } from '@/components/ui/Icono';
import { Insignia } from '@/components/ui/Insignia';
import { formatearCosto } from '../comun/formatos';
import { useTiposTramite } from '../comun/usePublico';
import { EncabezadoSeccion } from './EncabezadoSeccion';
import { Esqueletos } from './Esqueletos';

/** Trámites con su costo y requisitos por nivel escolar (RF-01). Los requisitos se abren a demanda. */
export function SeccionTramites() {
  const tipos = useTiposTramite();
  return (
    <section
      id="tramites"
      aria-labelledby="titulo-tramites"
      className="scroll-mt-20 border-y border-slate-200 bg-white py-16 sm:py-20"
    >
      <Contenedor ancho="amplio" className="flex flex-col gap-10">
        <EncabezadoSeccion
          id="titulo-tramites"
          antetitulo="Antes de agendar"
          titulo="Trámites y requisitos"
          descripcion="Revisa qué trámite necesitas y qué debes llevar el día de tu cita."
        />
        {tipos.isPending && <Esqueletos texto="Cargando trámites…" />}
        {tipos.isError && <Alerta tono="error">{mensajeDeError(tipos.error)}</Alerta>}
        {tipos.data?.length === 0 && <p className="text-slate-700">Por ahora no hay trámites disponibles.</p>}
        {tipos.data && tipos.data.length > 0 && (
          <div className="grid items-start gap-4 md:grid-cols-2 lg:grid-cols-3">
            {tipos.data.map((tipo) => (
              <article
                key={tipo.tipoTramiteId}
                className="flex flex-col rounded-2xl border border-slate-200 bg-slate-50/60 p-6"
              >
                <header className="flex items-start justify-between gap-3">
                  <h3 className="text-xl font-bold text-slate-950">{tipo.nombre}</h3>
                  <Insignia tono={tipo.costo ? 'neutro' : 'exito'}>{formatearCosto(tipo.costo)}</Insignia>
                </header>
                {tipo.descripcion && <p className="mt-2 text-slate-700">{tipo.descripcion}</p>}
                <details className="group mt-5 rounded-xl border border-slate-200 bg-white">
                  <summary className="flex min-h-12 items-center justify-between gap-2 rounded-xl px-4 py-3 font-semibold text-guinda-800 hover:bg-guinda-50">
                    <span className="flex items-center gap-2">
                      <Icono nombre="documento" />
                      Ver requisitos ({tipo.requisitos.length})
                    </span>
                    <Icono
                      nombre="chevronAbajo"
                      className="size-5 transition-transform group-open:rotate-180"
                    />
                  </summary>
                  <div className="border-t border-slate-100 px-4 py-4">
                    <Requisitos requisitos={tipo.requisitos} />
                  </div>
                </details>
              </article>
            ))}
          </div>
        )}
      </Contenedor>
    </section>
  );
}
