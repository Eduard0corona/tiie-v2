import { useEffect, useState } from 'react';
import { estiloBoton } from '@/components/ui/Boton';
import { Icono } from '@/components/ui/Icono';
import { crearIcs, urlGoogleCalendar, type EventoCita } from './calendario';

/** Copia el folio para dictarlo o pegarlo; avisa con texto que se copió. */
export function CopiarFolio({ folio }: { folio: string }) {
  const [copiado, setCopiado] = useState(false);
  useEffect(() => {
    if (!copiado) return;
    const espera = setTimeout(() => setCopiado(false), 2500);
    return () => clearTimeout(espera);
  }, [copiado]);

  if (!navigator.clipboard) return null;
  return (
    <button
      type="button"
      onClick={() => void navigator.clipboard.writeText(folio).then(() => setCopiado(true))}
      className="inline-flex min-h-11 items-center gap-1.5 rounded-lg px-2 text-sm font-semibold text-guinda-50 hover:bg-white/10"
    >
      <Icono nombre={copiado ? 'check' : 'copiar'} className="size-4" />
      <span aria-live="polite">{copiado ? 'Copiado' : 'Copiar folio'}</span>
    </button>
  );
}

/** Agregar la cita al calendario del teléfono: Google Calendar en Android, .ics para Apple y Outlook. */
export function AgregarCalendario({ evento }: { evento: EventoCita }) {
  const descargar = () => {
    const archivo = new Blob([crearIcs(evento, Date.now())], { type: 'text/calendar;charset=utf-8' });
    const url = URL.createObjectURL(archivo);
    const enlace = document.createElement('a');
    enlace.href = url;
    enlace.download = `cita-tiie-${evento.folio}.ics`;
    enlace.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  };

  return (
    <div role="group" aria-labelledby="titulo-calendario" className="flex flex-col gap-2">
      <p id="titulo-calendario" className="text-sm font-semibold text-slate-700">
        Agrégala a tu calendario
      </p>
      <div className="flex flex-wrap gap-2">
        <a
          href={urlGoogleCalendar(evento)}
          target="_blank"
          rel="noopener noreferrer"
          className={estiloBoton({ variante: 'secundario' })}
        >
          <Icono nombre="calendario" />
          Google Calendar <span className="sr-only">(abre en una pestaña nueva)</span>
        </a>
        <button type="button" onClick={descargar} className={estiloBoton({ variante: 'secundario' })}>
          <Icono nombre="descargar" />
          Apple u Outlook (.ics)
        </button>
      </div>
    </div>
  );
}
