import { useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { ErrorApi, mensajeDeError } from '@/api/errores';
import { CalendarioDisponibilidad } from '@/components/CalendarioDisponibilidad';
import { SelectorHora } from '@/components/SelectorHora';
import { Alerta, RegionAnuncios } from '@/components/ui/Alerta';
import { Boton } from '@/components/ui/Boton';
import { Cargando } from '@/components/ui/Encabezado';
import { formatearFechaLarga } from '@/lib/fechas';
import { clavesPublicas, useCupos, useDisponibilidad } from '../comun/usePublico';
import { claveMiCita, useReprogramarMiCita } from './useMiCita';

export interface ReprogramarCitaProps {
  ofertaId: number;
  alTerminar: () => void;
  alCancelar: () => void;
}

/** RN-17: otro día y hora del mismo trámite y sede. */
export function ReprogramarCita({ ofertaId, alTerminar, alCancelar }: ReprogramarCitaProps) {
  const [fecha, setFecha] = useState<string | null>(null);
  const [cupo, setCupo] = useState<number | null>(null);
  const [aviso, setAviso] = useState<{ texto: string; traza?: string | null } | null>(null);
  const disponibilidad = useDisponibilidad(ofertaId);
  const cupos = useCupos(ofertaId, fecha);
  const reprogramar = useReprogramarMiCita();
  const cliente = useQueryClient();

  const confirmar = () => {
    if (cupo === null) return;
    setAviso(null);
    reprogramar.mutate(cupo, {
      onSuccess: alTerminar,
      onError: (error) => {
        if (!(error instanceof ErrorApi)) {
          setAviso({ texto: mensajeDeError(error) });
          return;
        }
        switch (error.codigo) {
          case 'SinCupo':
            setCupo(null);
            void cupos.refetch();
            break;
          case 'FueraDeVentana':
          case 'CupoCerrado':
          case 'OfertaInactiva':
            setFecha(null);
            setCupo(null);
            void cliente.invalidateQueries({ queryKey: clavesPublicas.disponibilidad(ofertaId) });
            break;
          case 'CitaNoModificable':
            void cliente.invalidateQueries({ queryKey: claveMiCita });
            break;
        }
        setAviso({ texto: error.errors['cupoId']?.join(' ') ?? error.mensaje, traza: error.traceId });
      },
    });
  };

  return (
    <section
      aria-labelledby="titulo-reprogramar"
      className="flex flex-col gap-4 rounded-lg border border-slate-200 bg-white p-4"
    >
      <h2 id="titulo-reprogramar" className="text-lg font-semibold">
        Elige la nueva fecha y hora
      </h2>
      {disponibilidad.isPending && <Cargando texto="Cargando días disponibles…" />}
      {disponibilidad.isError && <Alerta tono="error">{mensajeDeError(disponibilidad.error)}</Alerta>}
      {disponibilidad.data && (
        <CalendarioDisponibilidad
          dias={disponibilidad.data.dias}
          desde={disponibilidad.data.desde}
          hasta={disponibilidad.data.hasta}
          seleccion={fecha}
          alElegir={(nueva) => {
            setFecha(nueva);
            setCupo(null);
          }}
        />
      )}
      {fecha && cupos.isPending && <Cargando texto="Cargando horas…" />}
      {fecha && cupos.data?.length === 0 && (
        <Alerta>Ya no quedan horas con lugar el {formatearFechaLarga(fecha)}. Elige otro día.</Alerta>
      )}
      {fecha && cupos.data && cupos.data.length > 0 && (
        <SelectorHora
          leyenda={`Horas con lugar el ${formatearFechaLarga(fecha)}`}
          cupos={cupos.data}
          seleccion={cupo}
          alElegir={setCupo}
        />
      )}
      <RegionAnuncios>
        {aviso && (
          <Alerta tono="error" traza={aviso.traza ?? null}>
            {aviso.texto}
          </Alerta>
        )}
      </RegionAnuncios>
      <div className="flex flex-wrap gap-3">
        <Boton
          disabled={cupo === null}
          ocupado={reprogramar.isPending}
          textoOcupado="Cambiando…"
          onClick={confirmar}
        >
          Confirmar el cambio
        </Boton>
        <Boton variante="secundario" onClick={alCancelar}>
          Conservar mi cita
        </Boton>
      </div>
    </section>
  );
}
