import { useState, type ReactNode } from 'react';
import { ErrorApi, mensajeDeError } from '@/api/errores';
import type { components } from '@/api/esquema';
import { Alerta } from '@/components/ui/Alerta';
import { Boton } from '@/components/ui/Boton';
import { Dialogo } from '@/components/ui/Dialogo';
import { AreaTexto } from '@/components/ui/Selector';
import { TEXTO_ESTADO } from './textos';

type EstadoCita = components['schemas']['EstadoCita'];

/**
 * Error de una consulta o una acción del panel. Un 409 Conflicto ofrece recargar; los demás muestran su
 * mensaje, los errores por campo que ningún formulario mostró y la referencia para soporte. Un recurso de
 * otro módulo llega como 404, igual que uno que no existe.
 */
export function ErrorPanel({ error, alRecargar }: { error: unknown; alRecargar?: () => void }) {
  const conflicto = error instanceof ErrorApi && error.codigo === 'Conflicto';
  const detalles = error instanceof ErrorApi ? Object.values(error.errors).flat() : [];
  return (
    <Alerta tono="error" traza={error instanceof ErrorApi ? error.traceId : null}>
      <p>{mensajeDeError(error)}</p>
      {detalles.length > 0 && (
        <ul className="mt-1 list-disc pl-5">
          {detalles.map((d) => (
            <li key={d}>{d}</li>
          ))}
        </ul>
      )}
      {conflicto && alRecargar && (
        <Boton variante="secundario" className="mt-2" onClick={alRecargar}>
          Recargar
        </Boton>
      )}
    </Alerta>
  );
}

const coloresEstado: Record<EstadoCita, string> = {
  Agendada: 'bg-blue-50 text-blue-900 border-blue-300',
  Atendida: 'bg-green-50 text-green-900 border-green-300',
  NoAsistio: 'bg-amber-50 text-amber-900 border-amber-300',
  Cancelada: 'bg-slate-100 text-slate-800 border-slate-300',
  Reprogramada: 'bg-slate-100 text-slate-800 border-slate-300',
};

/** Estado de la cita con texto: el color solo acompaña. */
export function EtiquetaEstado({ estado }: { estado: EstadoCita }) {
  return (
    <span className={`inline-block rounded border px-2 py-0.5 text-sm font-medium ${coloresEstado[estado]}`}>
      {TEXTO_ESTADO[estado]}
    </span>
  );
}

export interface PaginacionProps {
  pagina: number;
  tamano: number;
  total: number;
  alCambiar: (pagina: number) => void;
}

/** Controles de página anterior y siguiente con el rango que se muestra. */
export function Paginacion({ pagina, tamano, total, alCambiar }: PaginacionProps) {
  const paginas = Math.max(1, Math.ceil(total / tamano));
  const desde = total === 0 ? 0 : (pagina - 1) * tamano + 1;
  const hasta = Math.min(total, pagina * tamano);
  return (
    <nav aria-label="Páginas" className="flex flex-wrap items-center justify-between gap-2">
      <p className="text-sm text-slate-700">
        {total === 0 ? 'Sin resultados' : `${desde} a ${hasta} de ${total}`}
      </p>
      <div className="flex gap-2">
        <Boton variante="secundario" disabled={pagina <= 1} onClick={() => alCambiar(pagina - 1)}>
          Anterior
        </Boton>
        <Boton variante="secundario" disabled={pagina >= paginas} onClick={() => alCambiar(pagina + 1)}>
          Siguiente
        </Boton>
      </div>
    </nav>
  );
}

export interface DialogoConfirmarProps {
  titulo: string;
  descripcion: ReactNode;
  textoConfirmar: string;
  /** Si se indica, pide un motivo; `true` lo hace obligatorio. */
  motivo?: { etiqueta: string; obligatorio: boolean };
  ocupado?: boolean;
  error?: unknown;
  alConfirmar: (motivo: string) => void;
  alCerrar: () => void;
}

/** Confirmación de una acción importante, con motivo opcional u obligatorio (máximo 300 caracteres). */
export function DialogoConfirmar({
  titulo,
  descripcion,
  textoConfirmar,
  motivo,
  ocupado,
  error,
  alConfirmar,
  alCerrar,
}: DialogoConfirmarProps) {
  const [texto, setTexto] = useState('');
  const [faltaMotivo, setFaltaMotivo] = useState(false);

  const confirmar = () => {
    if (motivo?.obligatorio && texto.trim() === '') {
      setFaltaMotivo(true);
      return;
    }
    alConfirmar(texto.trim());
  };

  return (
    <Dialogo titulo={titulo} alCerrar={alCerrar}>
      <div className="text-slate-700">{descripcion}</div>
      {motivo && (
        <AreaTexto
          etiqueta={motivo.obligatorio ? motivo.etiqueta : `${motivo.etiqueta} (opcional)`}
          maxLength={300}
          value={texto}
          error={
            faltaMotivo
              ? 'Escribe el motivo.'
              : error instanceof ErrorApi
                ? error.errors['motivo']?.join(' ')
                : undefined
          }
          onChange={(e) => {
            setTexto(e.target.value);
            setFaltaMotivo(false);
          }}
        />
      )}
      {error !== undefined && error !== null && !(error instanceof ErrorApi && error.errors['motivo']) && (
        <div aria-live="polite">
          <ErrorPanel error={error} />
        </div>
      )}
      <div className="flex flex-wrap justify-end gap-2">
        <Boton variante="secundario" onClick={alCerrar}>
          Volver
        </Boton>
        <Boton ocupado={ocupado} textoOcupado="Espera…" onClick={confirmar}>
          {textoConfirmar}
        </Boton>
      </div>
    </Dialogo>
  );
}

/** Tabla con desplazamiento horizontal en celular; las celdas se escriben con encabezados de columna. */
export function TablaDesplazable({ etiqueta, children }: { etiqueta: string; children: ReactNode }) {
  return (
    // relative: el texto sr-only de los encabezados es absoluto y, sin esto, se sale del desplazamiento en celular.
    <div className="relative overflow-x-auto rounded-md border border-slate-200 bg-white">
      <table className="w-full min-w-[40rem] border-collapse text-left text-sm">
        <caption className="sr-only">{etiqueta}</caption>
        {children}
      </table>
    </div>
  );
}

export const celdaEncabezado = 'border-b border-slate-200 bg-slate-50 px-3 py-2 font-semibold text-slate-800';
export const celda = 'border-b border-slate-100 px-3 py-2 align-top';
