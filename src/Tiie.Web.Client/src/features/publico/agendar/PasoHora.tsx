import { mensajeDeError } from '@/api/errores';
import { SelectorHora } from '@/components/SelectorHora';
import { Alerta } from '@/components/ui/Alerta';
import { Boton } from '@/components/ui/Boton';
import { Cargando, EncabezadoPaso } from '@/components/ui/Encabezado';
import { formatearFechaLarga } from '@/lib/fechas';
import { useCupos } from '../comun/usePublico';
import type { useEstadoAsistente } from './estadoAsistente';
import { ResumenSeleccion } from './ResumenSeleccion';

type Asistente = ReturnType<typeof useEstadoAsistente>;

/** Paso 3: hora con lugar en el día elegido. */
export function PasoHora({ seleccion, ir, aviso }: Asistente) {
  const cupos = useCupos(seleccion.oferta, seleccion.fecha);
  const fecha = seleccion.fecha ?? '';

  return (
    <section className="flex flex-col gap-6">
      <EncabezadoPaso>Elige la hora</EncabezadoPaso>
      <ResumenSeleccion seleccion={{ ...seleccion, cupo: null }} />
      {aviso && <Alerta tono="error">{aviso}</Alerta>}
      {cupos.isPending && <Cargando texto="Cargando horas…" />}
      {cupos.isError && <Alerta tono="error">{mensajeDeError(cupos.error)}</Alerta>}
      {cupos.data?.length === 0 && (
        <Alerta>Ya no quedan horas con lugar el {formatearFechaLarga(fecha)}. Elige otro día.</Alerta>
      )}
      {cupos.data && cupos.data.length > 0 && (
        <SelectorHora
          leyenda={`Horas con lugar el ${formatearFechaLarga(fecha)}`}
          cupos={cupos.data}
          seleccion={seleccion.cupo}
          alElegir={(cupo) => ir({ cupo })}
        />
      )}
      <div className="flex flex-wrap gap-3">
        <Boton disabled={seleccion.cupo === null} onClick={() => ir({ paso: 'datos' })}>
          Continuar
        </Boton>
        <Boton variante="secundario" onClick={() => ir({ paso: 'fecha', cupo: null })}>
          Cambiar día
        </Boton>
      </div>
    </section>
  );
}
