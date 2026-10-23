import { useQueryClient } from '@tanstack/react-query';
import { useEffect, useRef, useState } from 'react';
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
  const titulo = useRef<HTMLHeadingElement>(null);
  // El panel aparece debajo de los botones: el foco (y la vista) van a su título.
  useEffect(() => titulo.current?.focus(), []);

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
      className="flex flex-col gap-6 rounded-2xl border border-guinda-200 bg-white p-5 shadow-tarjeta sm:p-6"
    >
      <div className="flex flex-col gap-1">
        <h2
          ref={titulo}
          id="titulo-reprogramar"
          tabIndex={-1}
          className="text-xl font-bold text-slate-950 focus:outline-none"
        >
          Elige la nueva fecha y hora
        </h2>
        <p className="text-slate-600">Tu cita actual se conserva hasta que confirmes el cambio.</p>
      </div>
      {disponibilidad.isPending && <Cargando texto="Cargando días disponibles…" />}
      {disponibilidad.isError && <Alerta tono="error">{mensajeDeError(disponibilidad.error)}</Alerta>}
      {disponibilidad.data && (
        <CalendarioDisponibilidad
          dias={disponibilidad.data.dias}
          desde={disponibilidad.data.desde}
          hasta={disponibilidad.data.hasta}
          seleccion={fecha}
          nivelTitulos={3}
          alElegir={(nueva) => {
            setFecha(nueva);
            setCupo(null);
          }}
        />
      )}
      {fecha && cupos.isPending && <Cargando texto="Cargando horas…" />}
      {fecha && cupos.data?.length === 0 && (
        <Alerta tono="aviso">
          Ya no quedan horas con lugar el {formatearFechaLarga(fecha)}. Elige otro día.
        </Alerta>
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
      <div className="flex flex-col gap-2 sm:flex-row sm:gap-3">
        <Boton
          bloque
          disabled={cupo === null}
          ocupado={reprogramar.isPending}
          textoOcupado="Cambiando…"
          onClick={confirmar}
        >
          Confirmar el cambio
        </Boton>
        <Boton variante="secundario" bloque onClick={alCancelar}>
          Conservar mi cita
        </Boton>
      </div>
    </section>
  );
}
