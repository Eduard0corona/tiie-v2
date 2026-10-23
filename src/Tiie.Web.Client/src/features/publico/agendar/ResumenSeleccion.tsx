import { Icono, type NombreIcono } from '@/components/ui/Icono';
import { formatearFechaLarga } from '@/lib/fechas';
import { formatearCosto } from '../comun/formatos';
import { useCupos, useModulosDe, useOfertas } from '../comun/usePublico';
import type { Seleccion } from './estadoAsistente';

interface Fila {
  etiqueta: string;
  icono: NombreIcono;
  valor?: string | undefined;
  detalle?: string | undefined;
}

/**
 * Lo elegido hasta ahora, con los nombres de las consultas ya cargadas. Compacto dentro de cada paso (celular)
 * o como tarjeta lateral fija (escritorio), donde lo que falta aparece «Por elegir».
 */
export function ResumenSeleccion({
  seleccion,
  lateral = false,
}: {
  seleccion: Seleccion;
  lateral?: boolean;
}) {
  const modulos = useModulosDe(seleccion.municipio);
  const ofertas = useOfertas(seleccion.modulo);
  const cupos = useCupos(seleccion.cupo ? seleccion.oferta : null, seleccion.fecha);
  const modulo = modulos.data?.find((m) => m.moduloId === seleccion.modulo);
  const oferta = ofertas.data?.find((o) => o.ofertaId === seleccion.oferta);
  const hora = cupos.data?.find((c) => c.cupoId === seleccion.cupo)?.hora;

  const filas: Fila[] = [
    { etiqueta: 'Sede', icono: 'sede', valor: modulo?.nombre, detalle: modulo?.direccion },
    {
      etiqueta: 'Trámite',
      icono: 'tarjeta',
      valor: oferta?.nombre,
      detalle: oferta ? formatearCosto(oferta.costo) : undefined,
    },
    {
      etiqueta: 'Fecha',
      icono: 'calendario',
      valor: seleccion.fecha ? formatearFechaLarga(seleccion.fecha) : undefined,
    },
    { etiqueta: 'Hora', icono: 'reloj', valor: hora },
  ];

  if (lateral) {
    return (
      <section
        aria-labelledby="titulo-resumen"
        className="rounded-2xl border border-slate-200 bg-white p-5 shadow-tarjeta"
      >
        <h2 id="titulo-resumen" className="text-lg font-bold text-slate-950">
          Resumen de tu cita
        </h2>
        <ul className="mt-4 flex flex-col gap-4">
          {filas.map((fila) => (
            <li key={fila.etiqueta} className="flex gap-3">
              <span
                className={`grid size-9 shrink-0 place-items-center rounded-lg ${
                  fila.valor ? 'bg-guinda-50 text-guinda-700' : 'bg-slate-100 text-slate-400'
                }`}
              >
                <Icono nombre={fila.icono} className="size-4.5" />
              </span>
              <div className="min-w-0">
                <p className="text-xs font-semibold tracking-wide text-slate-500 uppercase">
                  {fila.etiqueta}
                </p>
                <p
                  className={
                    fila.valor ? 'font-semibold text-slate-900 first-letter:uppercase' : 'text-slate-500'
                  }
                >
                  {fila.valor ?? 'Por elegir'}
                </p>
                {fila.valor && fila.detalle && <p className="text-sm text-slate-600">{fila.detalle}</p>}
              </div>
            </li>
          ))}
        </ul>
        <p className="mt-5 flex gap-2 border-t border-slate-100 pt-4 text-sm text-slate-600">
          <Icono nombre="correo" className="mt-0.5 size-4" />
          Al terminar te enviamos el comprobante y el enlace de tu cita por correo.
        </p>
      </section>
    );
  }

  const visibles = filas.filter((fila) => fila.valor);
  if (visibles.length === 0) return null;
  return (
    <ul
      aria-label="Lo que elegiste"
      className="flex flex-col gap-2 rounded-xl border border-slate-200 bg-white p-4 text-sm shadow-sm lg:hidden"
    >
      {visibles.map((fila) => (
        <li key={fila.etiqueta} className="flex items-start gap-2.5">
          <Icono nombre={fila.icono} className="mt-0.5 size-4 text-guinda-700" />
          <span className="font-medium text-slate-900">
            <span className="sr-only">{fila.etiqueta}: </span>
            <span className="inline-block first-letter:uppercase">{fila.valor}</span>
            {fila.etiqueta === 'Sede' && fila.detalle && (
              <span className="block font-normal text-slate-600">{fila.detalle}</span>
            )}
          </span>
        </li>
      ))}
    </ul>
  );
}
