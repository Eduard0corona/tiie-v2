import { useEffect, useRef } from 'react';
import { mensajeDeError } from '@/api/errores';
import { Requisitos } from '@/components/Requisitos';
import { Alerta } from '@/components/ui/Alerta';
import { Boton } from '@/components/ui/Boton';
import { Cargando, EncabezadoPaso } from '@/components/ui/Encabezado';
import { GrupoOpciones } from '@/components/ui/GrupoOpciones';
import { Icono } from '@/components/ui/Icono';
import { Insignia } from '@/components/ui/Insignia';
import { formatearCosto } from '../comun/formatos';
import { useModulosDe, useMunicipios, useOfertas, useTiposTramite } from '../comun/usePublico';
import { BarraAcciones } from './BarraAcciones';
import type { useEstadoAsistente } from './estadoAsistente';

type Asistente = ReturnType<typeof useEstadoAsistente>;
type Grupo = 'sede' | 'tramite';

/**
 * Paso 1: municipio, sede y trámite. Al elegir, la siguiente pregunta se desplaza a la vista; al elegir el
 * trámite se muestran sus requisitos, para saber qué llevar antes de comprometer un horario.
 */
export function PasoSede({ seleccion, ir, aviso }: Asistente) {
  const municipios = useMunicipios();
  const modulos = useModulosDe(seleccion.municipio);
  const ofertas = useOfertas(seleccion.modulo);
  const tipos = useTiposTramite();
  const error = municipios.error ?? modulos.error ?? ofertas.error;
  // Pregunta que se lleva a la vista cuando lleguen sus opciones. Solo después de una elección de la persona,
  // no al llegar con la sede ya elegida desde la portada.
  const siguiente = useRef<Grupo | null>(null);
  const grupoSede = useRef<HTMLDivElement>(null);
  const grupoTramite = useRef<HTMLDivElement>(null);
  useEffect(() => {
    const destino = siguiente.current === 'sede' && modulos.data ? grupoSede : null;
    const otro = siguiente.current === 'tramite' && ofertas.data ? grupoTramite : null;
    const grupo = destino ?? otro;
    if (!grupo) return;
    siguiente.current = null;
    grupo.current?.scrollIntoView?.({ block: 'start' });
  }, [modulos.data, ofertas.data]);

  const tipoElegido = tipos.data?.find(
    (t) => t.tipoTramiteId === ofertas.data?.find((o) => o.ofertaId === seleccion.oferta)?.tipoTramiteId,
  );

  return (
    <section className="flex flex-col gap-8">
      <div className="flex flex-col gap-2">
        <EncabezadoPaso className="text-3xl tracking-tight sm:text-4xl">
          ¿Dónde quieres tu cita?
        </EncabezadoPaso>
        <p className="text-lg text-slate-600">Elige el municipio, la sede y el trámite que necesitas.</p>
      </div>
      {aviso && <Alerta tono="aviso">{aviso}</Alerta>}
      {error && <Alerta tono="error">{mensajeDeError(error)}</Alerta>}

      {municipios.isPending && <Cargando texto="Cargando municipios…" />}
      {municipios.data && (
        <GrupoOpciones
          leyenda="Municipio"
          numero={1}
          variante="chips"
          opciones={municipios.data.map((m) => ({ valor: m.municipioId, etiqueta: m.nombre }))}
          seleccion={seleccion.municipio}
          alElegir={(municipio) => {
            ir({ municipio, modulo: null, oferta: null, fecha: null, cupo: null });
            siguiente.current = 'sede';
          }}
        />
      )}

      <div ref={grupoSede} className="scroll-mt-24">
        {seleccion.municipio !== null && modulos.isPending && <Cargando texto="Cargando sedes…" />}
        {modulos.data && (
          <GrupoOpciones
            leyenda="Sede"
            numero={2}
            opciones={modulos.data.map((m) => ({
              valor: m.moduloId,
              etiqueta: m.nombre,
              icono: 'sede',
              detalle: (
                <>
                  {m.direccion}
                  {m.horarioAtencion && (
                    <span className="mt-1 flex items-start gap-1.5">
                      <Icono nombre="reloj" className="mt-0.5 size-4" />
                      {m.horarioAtencion}
                    </span>
                  )}
                </>
              ),
            }))}
            seleccion={seleccion.modulo}
            alElegir={(modulo) => {
              ir({ modulo, oferta: null, fecha: null, cupo: null });
              siguiente.current = 'tramite';
            }}
          />
        )}
      </div>

      <div ref={grupoTramite} className="scroll-mt-24">
        {seleccion.modulo !== null && ofertas.isPending && <Cargando texto="Cargando trámites…" />}
        {ofertas.data?.length === 0 && (
          <Alerta>Esta sede no tiene trámites para agendar por ahora. Elige otra sede.</Alerta>
        )}
        {ofertas.data && ofertas.data.length > 0 && (
          <GrupoOpciones
            leyenda="Trámite"
            numero={3}
            opciones={ofertas.data.map((o) => ({
              valor: o.ofertaId,
              etiqueta: o.nombre,
              insignia: <Insignia tono={o.costo ? 'neutro' : 'exito'}>{formatearCosto(o.costo)}</Insignia>,
              detalle:
                [tipos.data?.find((t) => t.tipoTramiteId === o.tipoTramiteId)?.descripcion, o.notasSede]
                  .filter(Boolean)
                  .join(' ') || undefined,
            }))}
            seleccion={seleccion.oferta}
            alElegir={(oferta) => ir({ oferta, fecha: null, cupo: null })}
          />
        )}
      </div>

      {tipoElegido && tipoElegido.requisitos.length > 0 && (
        <section
          aria-labelledby="titulo-que-llevar"
          className="rounded-2xl border border-emerald-200 bg-emerald-50/60 p-5"
        >
          <h2 id="titulo-que-llevar" className="flex items-center gap-2 font-bold text-slate-950">
            <Icono nombre="documento" className="text-emerald-700" />
            Lo que deberás llevar a tu cita
          </h2>
          <div className="mt-4">
            <Requisitos requisitos={tipoElegido.requisitos} />
          </div>
        </section>
      )}

      <BarraAcciones
        nota={seleccion.oferta === null ? 'Elige municipio, sede y trámite para continuar.' : undefined}
      >
        <Boton
          tamano="grande"
          className="flex-1 sm:flex-none"
          disabled={seleccion.oferta === null}
          onClick={() => ir({ paso: 'fecha' })}
        >
          Continuar
          <Icono nombre="flecha" />
        </Boton>
      </BarraAcciones>
    </section>
  );
}
