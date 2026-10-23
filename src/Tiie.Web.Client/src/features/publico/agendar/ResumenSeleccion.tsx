import { formatearFechaLarga } from '@/lib/fechas';
import { useCupos, useModulosDe, useOfertas } from '../comun/usePublico';
import type { Seleccion } from './estadoAsistente';

/** Lo elegido hasta ahora, con los nombres de las consultas ya cargadas. */
export function ResumenSeleccion({ seleccion }: { seleccion: Seleccion }) {
  const modulos = useModulosDe(seleccion.municipio);
  const ofertas = useOfertas(seleccion.modulo);
  const cupos = useCupos(seleccion.cupo ? seleccion.oferta : null, seleccion.fecha);
  const modulo = modulos.data?.find((m) => m.moduloId === seleccion.modulo);
  const oferta = ofertas.data?.find((o) => o.ofertaId === seleccion.oferta);
  const hora = cupos.data?.find((c) => c.cupoId === seleccion.cupo)?.hora;

  const filas: [string, string | undefined][] = [
    ['Sede', modulo ? `${modulo.nombre}, ${modulo.direccion}` : undefined],
    ['Trámite', oferta?.nombre],
    ['Fecha', seleccion.fecha ? formatearFechaLarga(seleccion.fecha) : undefined],
    ['Hora', hora],
  ];
  const visibles = filas.filter(([, valor]) => valor);
  if (visibles.length === 0) return null;
  return (
    <dl className="grid grid-cols-[auto_1fr] gap-x-3 gap-y-1 rounded-md bg-white p-3 text-sm">
      {visibles.map(([etiqueta, valor]) => (
        <div key={etiqueta} className="contents">
          <dt className="text-slate-600">{etiqueta}</dt>
          <dd className="font-medium">{valor}</dd>
        </div>
      ))}
    </dl>
  );
}
