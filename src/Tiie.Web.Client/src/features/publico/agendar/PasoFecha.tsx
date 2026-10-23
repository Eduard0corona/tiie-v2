import { mensajeDeError } from '@/api/errores';
import { CalendarioDisponibilidad } from '@/components/CalendarioDisponibilidad';
import { Alerta } from '@/components/ui/Alerta';
import { Boton } from '@/components/ui/Boton';
import { Cargando, EncabezadoPaso } from '@/components/ui/Encabezado';
import { Icono } from '@/components/ui/Icono';
import { formatearDiaSemana } from '@/lib/fechas';
import { useDisponibilidad } from '../comun/usePublico';
import type { useEstadoAsistente } from './estadoAsistente';
import { ResumenSeleccion } from './ResumenSeleccion';

type Asistente = ReturnType<typeof useEstadoAsistente>;

/** Paso 2: día dentro de la ventana de reserva (RF-02, RN-03). Elegir un día lleva directo a la hora. */
export function PasoFecha({ seleccion, ir, aviso }: Asistente) {
  const disponibilidad = useDisponibilidad(seleccion.oferta);
  const hayDias = disponibilidad.data?.dias.some((d) => d.estado === 'Alta' || d.estado === 'Poca');

  return (
    <section className="flex flex-col gap-6">
      <div className="flex flex-col gap-2">
        <EncabezadoPaso className="text-3xl tracking-tight sm:text-4xl">Elige el día</EncabezadoPaso>
        {disponibilidad.data && (
          <p className="text-lg text-slate-600">
            Puedes agendar del {formatearDiaSemana(disponibilidad.data.desde)} al{' '}
            {formatearDiaSemana(disponibilidad.data.hasta)}.
          </p>
        )}
      </div>
      <ResumenSeleccion seleccion={seleccion} />
      {aviso && <Alerta tono="aviso">{aviso}</Alerta>}
      {disponibilidad.isPending && <Cargando texto="Cargando días disponibles…" />}
      {disponibilidad.isError && <Alerta tono="error">{mensajeDeError(disponibilidad.error)}</Alerta>}
      {disponibilidad.data && !hayDias && (
        <Alerta tono="aviso" titulo="Sin días con lugar">
          Por ahora no hay días con lugar para este trámite en esta sede. Prueba otra sede.
        </Alerta>
      )}
      {disponibilidad.data && hayDias && (
        <CalendarioDisponibilidad
          dias={disponibilidad.data.dias}
          desde={disponibilidad.data.desde}
          hasta={disponibilidad.data.hasta}
          seleccion={seleccion.fecha}
          alElegir={(fecha) => ir({ paso: 'hora', fecha, cupo: null })}
        />
      )}
      <div>
        <Boton variante="secundario" onClick={() => ir({ paso: 'sede' })}>
          <Icono nombre="flechaIzquierda" />
          Cambiar sede o trámite
        </Boton>
      </div>
    </section>
  );
}
