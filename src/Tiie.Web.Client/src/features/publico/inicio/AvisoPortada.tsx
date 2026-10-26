import type { components } from '@/api/esquema';
import { TextoConEnlaces } from '@/components/TextoConEnlaces';
import { Contenedor } from '@/components/ui/Contenedor';
import { Icono } from '@/components/ui/Icono';

type Aviso = components['schemas']['AvisoPortada'];

const estilos = {
  Informativo: {
    franja: 'border-guinda-100 bg-guinda-50 text-guinda-950',
    icono: 'info',
    color: 'text-guinda-700',
    nombre: 'Aviso',
  },
  Importante: {
    franja: 'border-amber-200 bg-amber-50 text-amber-950',
    icono: 'alerta',
    color: 'text-amber-700',
    nombre: 'Aviso importante',
  },
} as const;

/**
 * Aviso que publica el administrador sobre la portada: cierres, temporadas, cambios de requisitos. La API solo
 * lo envía mientras está vigente.
 */
export function AvisoPortada({ aviso }: { aviso: Aviso }) {
  const estilo = estilos[aviso.tono];
  return (
    <aside aria-label={estilo.nombre} className={`border-b ${estilo.franja}`}>
      <Contenedor ancho="amplio" className="flex items-start gap-3 py-3 sm:items-center">
        <Icono nombre={estilo.icono} className={`mt-0.5 size-5 shrink-0 sm:mt-0 ${estilo.color}`} />
        <p className="font-medium whitespace-pre-line">
          <TextoConEnlaces
            texto={aviso.texto}
            claseEnlace="font-semibold underline underline-offset-4 hover:no-underline"
          />
        </p>
      </Contenedor>
    </aside>
  );
}
