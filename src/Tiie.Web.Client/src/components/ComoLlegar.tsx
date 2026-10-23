import { estiloBoton } from '@/components/ui/Boton';
import { Icono } from '@/components/ui/Icono';
import { urlComoLlegar } from '@/features/publico/comun/formatos';

/** Abre el mapa del dispositivo con las coordenadas de la sede, en una pestaña nueva. */
export function ComoLlegar({
  latitud,
  longitud,
  sede,
  comoBoton = false,
}: {
  latitud: number | null;
  longitud: number | null;
  sede: string;
  /** Botón secundario en lugar de enlace de texto. */
  comoBoton?: boolean;
}) {
  const url = urlComoLlegar(latitud, longitud);
  if (!url) return null;
  return (
    <a
      href={url}
      target="_blank"
      rel="noopener noreferrer"
      aria-label={`Cómo llegar a ${sede} (abre el mapa en una pestaña nueva)`}
      className={
        comoBoton
          ? estiloBoton({ variante: 'secundario' })
          : 'inline-flex min-h-11 items-center gap-1.5 font-semibold text-guinda-700 underline-offset-4 hover:underline'
      }
    >
      <Icono nombre="ubicacion" className="size-5" />
      Cómo llegar
      <Icono nombre="externo" className="size-4 opacity-70" />
    </a>
  );
}
