import { useId, useState } from 'react';
import { ErrorApi, mensajeDeError } from '@/api/errores';
import { Alerta, RegionAnuncios } from '@/components/ui/Alerta';
import { Boton } from '@/components/ui/Boton';
import { Dialogo } from '@/components/ui/Dialogo';
import { useCancelarMiCita } from './useMiCita';

const MAXIMO_MOTIVO = 300;

/** Confirmación de la cancelación, con motivo opcional (RN-13). */
export function DialogoCancelar({ alCerrar, alCancelar }: { alCerrar: () => void; alCancelar: () => void }) {
  const [motivo, setMotivo] = useState('');
  const cancelar = useCancelarMiCita();
  const idMotivo = useId();

  const confirmar = () =>
    cancelar.mutate(motivo.trim() || null, {
      onSuccess: alCancelar,
    });

  return (
    <Dialogo titulo="¿Cancelar tu cita?" alCerrar={alCerrar}>
      <p className="text-slate-700">
        Liberaremos tu lugar y te enviaremos un correo de confirmación. No se puede deshacer.
      </p>
      <div className="flex flex-col gap-1">
        <label htmlFor={idMotivo} className="font-medium text-slate-800">
          Motivo (opcional)
        </label>
        <textarea
          id={idMotivo}
          value={motivo}
          maxLength={MAXIMO_MOTIVO}
          rows={3}
          onChange={(e) => setMotivo(e.target.value)}
          className="rounded-md border border-slate-400 bg-white px-3 py-2 text-base"
        />
      </div>
      <RegionAnuncios>
        {cancelar.isError && (
          <Alerta tono="error" traza={cancelar.error instanceof ErrorApi ? cancelar.error.traceId : null}>
            {mensajeDeError(cancelar.error)}
          </Alerta>
        )}
      </RegionAnuncios>
      <div className="flex flex-wrap gap-3">
        <Boton ocupado={cancelar.isPending} textoOcupado="Cancelando…" onClick={confirmar}>
          Sí, cancelar mi cita
        </Boton>
        <Boton variante="secundario" onClick={alCerrar}>
          No, conservarla
        </Boton>
      </div>
    </Dialogo>
  );
}
