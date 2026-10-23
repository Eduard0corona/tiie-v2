import { useState, type ReactNode } from 'react';
import { Link } from 'react-router';
import { ErrorApi, mensajeDeError } from '@/api/errores';
import type { components } from '@/api/esquema';
import { Alerta } from '@/components/ui/Alerta';
import { Boton } from '@/components/ui/Boton';
import { Dialogo } from '@/components/ui/Dialogo';
import { EncabezadoPaso } from '@/components/ui/Encabezado';
import { Icono, type NombreIcono } from '@/components/ui/Icono';
import { AreaTexto } from '@/components/ui/Selector';
import { TEXTO_ESTADO } from './textos';

/** Tarjeta blanca de una sección o de un formulario de filtros del panel. */
export const estiloPanel = 'rounded-xl border border-slate-200 bg-white p-4 shadow-sm sm:p-5';

/**
 * Encabezado de cada pantalla del panel: enlace para volver (en detalles), título que recibe el foco, una frase
 * de para qué sirve y las acciones principales a la derecha.
 */
export function EncabezadoPagina({
  titulo,
  descripcion,
  volver,
  acciones,
  children,
}: {
  titulo: ReactNode;
  descripcion?: ReactNode;
  volver?: { a: string; texto: string };
  acciones?: ReactNode;
  /** Algo bajo el título, por ejemplo el estado de una cita. */
  children?: ReactNode;
}) {
  return (
    <header className="flex flex-col gap-3">
      {volver && (
        <Link
          to={volver.a}
          className="inline-flex min-h-11 items-center gap-1.5 self-start text-sm font-semibold text-slate-600 hover:text-guinda-800"
        >
          <Icono nombre="flechaIzquierda" className="size-4" />
          {volver.texto}
        </Link>
      )}
      <div className="flex flex-wrap items-start justify-between gap-x-6 gap-y-3">
        <div className="flex min-w-0 flex-col gap-1.5">
          <EncabezadoPaso className="text-2xl tracking-tight text-slate-950 sm:text-3xl">
            {titulo}
          </EncabezadoPaso>
          {descripcion && <p className="max-w-3xl text-slate-600">{descripcion}</p>}
          {children}
        </div>
        {acciones && <div className="flex flex-wrap items-center gap-2">{acciones}</div>}
      </div>
    </header>
  );
}

/** Sección con título dentro de una pantalla del panel. */
export function Panel({
  titulo,
  descripcion,
  acciones,
  icono,
  children,
  className = '',
}: {
  titulo?: string;
  descripcion?: ReactNode;
  acciones?: ReactNode;
  icono?: NombreIcono;
  children: ReactNode;
  className?: string;
}) {
  return (
    <section className={`${estiloPanel} flex flex-col gap-4 ${className}`}>
      {(titulo || acciones) && (
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div className="flex flex-col gap-1">
            {titulo && (
              <h2 className="flex items-center gap-2 text-lg font-bold text-slate-950">
                {icono && <Icono nombre={icono} className="text-guinda-700" />}
                {titulo}
              </h2>
            )}
            {descripcion && <p className="text-sm text-slate-600">{descripcion}</p>}
          </div>
          {acciones}
        </div>
      )}
      {children}
    </section>
  );
}

/** Lista o búsqueda sin resultados: dice qué pasó y, si aplica, qué hacer. */
export function SinResultados({ titulo, children }: { titulo: string; children?: ReactNode }) {
  return (
    <div className="flex flex-col items-center gap-2 rounded-xl border border-dashed border-slate-300 bg-white px-6 py-10 text-center">
      <span className="grid size-11 place-items-center rounded-full bg-slate-100 text-slate-500">
        <Icono nombre="buscar" />
      </span>
      <p className="font-semibold text-slate-900">{titulo}</p>
      {children && <div className="max-w-md text-sm text-slate-600">{children}</div>}
    </div>
  );
}

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

const coloresEstado: Record<EstadoCita, { caja: string; punto: string }> = {
  Agendada: { caja: 'bg-blue-50 text-blue-900 ring-blue-200', punto: 'bg-blue-600' },
  Atendida: { caja: 'bg-emerald-50 text-emerald-900 ring-emerald-200', punto: 'bg-emerald-600' },
  NoAsistio: { caja: 'bg-amber-50 text-amber-900 ring-amber-200', punto: 'bg-amber-500' },
  Cancelada: { caja: 'bg-slate-100 text-slate-800 ring-slate-200', punto: 'bg-slate-400' },
  Reprogramada: { caja: 'bg-violet-50 text-violet-900 ring-violet-200', punto: 'bg-violet-500' },
};

/** Estado de la cita con texto: el color y el punto solo acompañan. */
export function EtiquetaEstado({ estado }: { estado: EstadoCita }) {
  const color = coloresEstado[estado];
  return (
    <span
      className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-0.5 text-sm font-medium whitespace-nowrap ring-1 ring-inset ${color.caja}`}
    >
      <span aria-hidden="true" className={`size-1.5 rounded-full ${color.punto}`} />
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
      <p className="text-sm text-slate-600">
        {total === 0 ? (
          'Sin resultados'
        ) : (
          <>
            <span className="font-semibold text-slate-900">
              {desde} a {hasta}
            </span>{' '}
            de {total}
          </>
        )}
      </p>
      <div className="flex gap-2">
        <Boton variante="secundario" disabled={pagina <= 1} onClick={() => alCambiar(pagina - 1)}>
          <Icono nombre="chevronIzquierda" className="size-4" />
          Anterior
        </Boton>
        <Boton variante="secundario" disabled={pagina >= paginas} onClick={() => alCambiar(pagina + 1)}>
          Siguiente
          <Icono nombre="chevronDerecha" className="size-4" />
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
    <div className="relative overflow-x-auto rounded-xl border border-slate-200 bg-white shadow-sm">
      <table className="w-full min-w-[40rem] border-collapse text-left text-sm [&_tbody_tr]:transition-colors [&_tbody_tr:hover]:bg-slate-50/80">
        <caption className="sr-only">{etiqueta}</caption>
        {children}
      </table>
    </div>
  );
}

export const celdaEncabezado =
  'border-b border-slate-200 bg-slate-50 px-4 py-3 text-xs font-semibold tracking-wide text-slate-600 uppercase';
export const celda = 'border-b border-slate-100 px-4 py-3 align-top';
