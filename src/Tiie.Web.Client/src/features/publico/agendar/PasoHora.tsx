import { mensajeDeError } from '@/api/errores';
import { SelectorHora } from '@/components/SelectorHora';
import { Alerta } from '@/components/ui/Alerta';
import { Boton } from '@/components/ui/Boton';
import { Cargando, EncabezadoPaso } from '@/components/ui/Encabezado';
import { Icono } from '@/components/ui/Icono';
import { formatearFechaLarga } from '@/lib/fechas';
import { useCupos } from '../comun/usePublico';
import { BarraAcciones } from './BarraAcciones';
import type { useEstadoAsistente } from './estadoAsistente';
import { ResumenSeleccion } from './ResumenSeleccion';

type Asistente = ReturnType<typeof useEstadoAsistente>;

/** Paso 3: hora con lugar en el día elegido. */
export function PasoHora({ seleccion, ir, aviso }: Asistente) {
  const cupos = useCupos(seleccion.oferta, seleccion.fecha);
  const fecha = seleccion.fecha ?? '';
  const sinHoras = cupos.data?.length === 0;

  return (
    <section className="flex flex-col gap-6">
      <div className="flex flex-col gap-2">
        <EncabezadoPaso className="text-3xl tracking-tight sm:text-4xl">Elige la hora</EncabezadoPaso>
        <p className="text-lg text-slate-600">
          Cada hora muestra cuántos lugares quedan. Es la hora de Sinaloa.
        </p>
      </div>
      <ResumenSeleccion seleccion={{ ...seleccion, cupo: null }} />
      {aviso && <Alerta tono="aviso">{aviso}</Alerta>}
      {cupos.isPending && <Cargando texto="Cargando horas…" />}
      {cupos.isError && <Alerta tono="error">{mensajeDeError(cupos.error)}</Alerta>}
      {sinHoras && (
        <Alerta tono="aviso">
          Ya no quedan horas con lugar el {formatearFechaLarga(fecha)}. Elige otro día.
        </Alerta>
      )}
      {cupos.data && cupos.data.length > 0 && (
        <SelectorHora
          leyenda={`Horas con lugar el ${formatearFechaLarga(fecha)}`}
          cupos={cupos.data}
          seleccion={seleccion.cupo}
          alElegir={(cupo) => ir({ cupo })}
        />
      )}
      <BarraAcciones
        nota={seleccion.cupo === null && !sinHoras ? 'Elige una hora para continuar.' : undefined}
      >
        <Boton
          variante="secundario"
          tamano="grande"
          className={sinHoras ? 'flex-1 sm:flex-none' : ''}
          onClick={() => ir({ paso: 'fecha', cupo: null })}
        >
          <Icono nombre="flechaIzquierda" />
          Cambiar día
        </Boton>
        {!sinHoras && (
          <Boton
            tamano="grande"
            className="flex-1 sm:flex-none"
            disabled={seleccion.cupo === null}
            onClick={() => ir({ paso: 'datos' })}
          >
            Continuar
            <Icono nombre="flecha" />
          </Boton>
        )}
      </BarraAcciones>
    </section>
  );
}
