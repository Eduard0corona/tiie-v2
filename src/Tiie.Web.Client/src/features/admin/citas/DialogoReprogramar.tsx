import { useState } from 'react';
import { ErrorApi } from '@/api/errores';
import { SelectorHora } from '@/components/SelectorHora';
import { Boton } from '@/components/ui/Boton';
import { Campo } from '@/components/ui/Campo';
import { Dialogo } from '@/components/ui/Dialogo';
import { Cargando } from '@/components/ui/Encabezado';
import { AreaTexto } from '@/components/ui/Selector';
import { ErrorPanel } from '@/features/admin/comun/Componentes';
import { esFechaIso, formatearFechaLarga, hoyLocal } from '@/lib/fechas';
import { useCuposDelDia } from './useCuposAdmin';

export interface DialogoReprogramarProps {
  ofertaId: number;
  folio: string;
  ocupado: boolean;
  error: unknown;
  alConfirmar: (cupoId: number, motivo: string) => void;
  alCerrar: () => void;
}

/** Elige una nueva fecha y hora de la misma oferta, con motivo obligatorio (RF-07). */
export function DialogoReprogramar({
  ofertaId,
  folio,
  ocupado,
  error,
  alConfirmar,
  alCerrar,
}: DialogoReprogramarProps) {
  const hoy = hoyLocal();
  const [fecha, setFecha] = useState<string>(hoy);
  const [cupoId, setCupoId] = useState<number | null>(null);
  const [motivo, setMotivo] = useState('');
  const [faltante, setFaltante] = useState<string | null>(null);
  const cupos = useCuposDelDia(ofertaId, esFechaIso(fecha) ? fecha : null);

  const confirmar = () => {
    if (cupoId === null) {
      setFaltante('Elige una hora.');
      return;
    }
    if (motivo.trim() === '') {
      setFaltante('Escribe el motivo.');
      return;
    }
    alConfirmar(cupoId, motivo.trim());
  };

  const errorMotivo =
    faltante === 'Escribe el motivo.'
      ? faltante
      : error instanceof ErrorApi
        ? error.errors['motivo']?.join(' ')
        : undefined;

  return (
    <Dialogo titulo={`Reprogramar la cita ${folio}`} alCerrar={alCerrar}>
      <Campo
        etiqueta="Nueva fecha"
        type="date"
        min={hoy}
        value={fecha}
        onChange={(e) => {
          setFecha(e.target.value);
          setCupoId(null);
        }}
      />
      {esFechaIso(fecha) && cupos.isPending && <Cargando texto="Buscando horas…" />}
      {cupos.isError && <ErrorPanel error={cupos.error} />}
      {cupos.data && cupos.data.length === 0 && (
        <p className="text-slate-700">No hay horas con lugar ese día. Elige otra fecha.</p>
      )}
      {cupos.data && cupos.data.length > 0 && (
        <SelectorHora
          leyenda={`Horas con lugar el ${formatearFechaLarga(fecha)}`}
          cupos={cupos.data}
          seleccion={cupoId}
          alElegir={(id) => {
            setCupoId(id);
            setFaltante(null);
          }}
        />
      )}
      {faltante === 'Elige una hora.' && <p className="font-medium text-red-800">{faltante}</p>}
      <AreaTexto
        etiqueta="Motivo"
        maxLength={300}
        value={motivo}
        error={errorMotivo}
        onChange={(e) => {
          setMotivo(e.target.value);
          setFaltante(null);
        }}
      />
      {error !== null && error !== undefined && !(error instanceof ErrorApi && error.errors['motivo']) && (
        <div aria-live="polite">
          <ErrorPanel error={error} />
        </div>
      )}
      <div className="flex flex-wrap justify-end gap-2">
        <Boton variante="secundario" onClick={alCerrar}>
          Volver
        </Boton>
        <Boton ocupado={ocupado} textoOcupado="Reprogramando…" onClick={confirmar}>
          Reprogramar
        </Boton>
      </div>
    </Dialogo>
  );
}
