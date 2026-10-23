import { mensajeDeError } from '@/api/errores';
import { Alerta } from '@/components/ui/Alerta';
import { Boton } from '@/components/ui/Boton';
import { Cargando, EncabezadoPaso } from '@/components/ui/Encabezado';
import { GrupoOpciones } from '@/components/ui/GrupoOpciones';
import { formatearCosto } from '../comun/formatos';
import { useModulosDe, useMunicipios, useOfertas } from '../comun/usePublico';
import type { useEstadoAsistente } from './estadoAsistente';

type Asistente = ReturnType<typeof useEstadoAsistente>;

/** Paso 1: municipio, sede y trámite. */
export function PasoSede({ seleccion, ir, aviso }: Asistente) {
  const municipios = useMunicipios();
  const modulos = useModulosDe(seleccion.municipio);
  const ofertas = useOfertas(seleccion.modulo);
  const error = municipios.error ?? modulos.error ?? ofertas.error;

  return (
    <section className="flex flex-col gap-6">
      <EncabezadoPaso>¿Dónde quieres tu cita?</EncabezadoPaso>
      {aviso && <Alerta tono="error">{aviso}</Alerta>}
      {error && <Alerta tono="error">{mensajeDeError(error)}</Alerta>}

      {municipios.isPending && <Cargando texto="Cargando municipios…" />}
      {municipios.data && (
        <GrupoOpciones
          leyenda="Municipio"
          opciones={municipios.data.map((m) => ({ valor: m.municipioId, etiqueta: m.nombre }))}
          seleccion={seleccion.municipio}
          alElegir={(municipio) => ir({ municipio, modulo: null, oferta: null, fecha: null, cupo: null })}
        />
      )}

      {seleccion.municipio !== null && modulos.isPending && <Cargando texto="Cargando sedes…" />}
      {modulos.data && (
        <GrupoOpciones
          leyenda="Sede"
          opciones={modulos.data.map((m) => ({
            valor: m.moduloId,
            etiqueta: m.nombre,
            detalle: (
              <>
                {m.direccion}
                {m.horarioAtencion && (
                  <>
                    <br />
                    {m.horarioAtencion}
                  </>
                )}
              </>
            ),
          }))}
          seleccion={seleccion.modulo}
          alElegir={(modulo) => ir({ modulo, oferta: null, fecha: null, cupo: null })}
        />
      )}

      {seleccion.modulo !== null && ofertas.isPending && <Cargando texto="Cargando trámites…" />}
      {ofertas.data && (
        <GrupoOpciones
          leyenda="Trámite"
          opciones={ofertas.data.map((o) => ({
            valor: o.ofertaId,
            etiqueta: o.nombre,
            detalle: [formatearCosto(o.costo), o.notasSede].filter(Boolean).join(' · '),
          }))}
          seleccion={seleccion.oferta}
          alElegir={(oferta) => ir({ oferta, fecha: null, cupo: null })}
        />
      )}

      <div>
        <Boton disabled={seleccion.oferta === null} onClick={() => ir({ paso: 'fecha' })}>
          Continuar
        </Boton>
      </div>
    </section>
  );
}
