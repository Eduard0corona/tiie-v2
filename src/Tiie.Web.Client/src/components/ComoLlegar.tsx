import { urlComoLlegar } from '@/features/publico/comun/formatos';

/** Abre el mapa del dispositivo con las coordenadas de la sede, en una pestaña nueva. */
export function ComoLlegar({
  latitud,
  longitud,
  sede,
}: {
  latitud: number | null;
  longitud: number | null;
  sede: string;
}) {
  const url = urlComoLlegar(latitud, longitud);
  if (!url) return null;
  return (
    <a
      href={url}
      target="_blank"
      rel="noopener noreferrer"
      aria-label={`Cómo llegar a ${sede} (abre el mapa en una pestaña nueva)`}
      className="inline-flex min-h-11 items-center text-guinda-700 underline underline-offset-4"
    >
      Cómo llegar
    </a>
  );
}
