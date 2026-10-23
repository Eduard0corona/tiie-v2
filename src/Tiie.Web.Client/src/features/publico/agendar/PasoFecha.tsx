import { mensajeDeError } from '@/api/errores';
import { CalendarioDisponibilidad } from '@/components/CalendarioDisponibilidad';
import { Alerta } from '@/components/ui/Alerta';
import { Boton } from '@/components/ui/Boton';
import { Cargando, EncabezadoPaso } from '@/components/ui/Encabezado';
import { useDisponibilidad } from '../comun/usePublico';
import type { useEstadoAsistente } from './estadoAsistente';
import { ResumenSeleccion } from './ResumenSeleccion';

type Asistente = ReturnType<typeof useEstadoAsistente>;

/** Paso 2: día dentro de la ventana de reserva (RF-02, RN-03). */
export function PasoFecha({ seleccion, ir, aviso }: Asistente) {
  const disponibilidad = useDisponibilidad(seleccion.oferta);
  const hayDias = disponibilidad.data?.dias.some((d) => d.estado === 'Alta' || d.estado === 'Poca');

  return (
    <section className="flex flex-col gap-6">
      <EncabezadoPaso>Elige el día</EncabezadoPaso>
      <ResumenSeleccion seleccion={seleccion} />
      {aviso && <Alerta tono="error">{aviso}</Alerta>}
      {disponibilidad.isPending && <Cargando texto="Cargando días disponibles…" />}
      {disponibilidad.isError && <Alerta tono="error">{mensajeDeError(disponibilidad.error)}</Alerta>}
      {disponibilidad.data && !hayDias && (
        <Alerta>Por ahora no hay días con lugar para este trámite en esta sede. Prueba otra sede.</Alerta>
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
          Cambiar sede o trámite
        </Boton>
      </div>
    </section>
  );
}
