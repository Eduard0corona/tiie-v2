import { useState } from 'react';
import { Link } from 'react-router';
import { mensajeDeError } from '@/api/errores';
import { ComoLlegar } from '@/components/ComoLlegar';
import { Alerta } from '@/components/ui/Alerta';
import { estiloBoton } from '@/components/ui/Boton';
import { Contenedor } from '@/components/ui/Contenedor';
import { Icono } from '@/components/ui/Icono';
import { useModulos } from '../comun/usePublico';
import { EncabezadoSeccion } from './EncabezadoSeccion';
import { Esqueletos } from './Esqueletos';

/**
 * Sedes (RF-01), ordenadas y filtrables por municipio. Cada una lleva directo al asistente con la sede ya
 * elegida, y a su mapa en una pestaña nueva.
 */
export function SeccionSedes() {
  const modulos = useModulos();
  const [filtro, setFiltro] = useState<string | null>(null);
  const sedes = [...(modulos.data ?? [])].sort((a, b) => a.municipio.localeCompare(b.municipio, 'es'));
  const municipios = [...new Set(sedes.map((s) => s.municipio))];
  const conFiltro = municipios.length > 1;
  const visibles = sedes.filter((s) => filtro === null || s.municipio === filtro);

  return (
    <section id="sedes" aria-labelledby="titulo-sedes" className="scroll-mt-20 py-16 sm:py-20">
      <Contenedor ancho="amplio" className="flex flex-col gap-10">
        <EncabezadoSeccion
          id="titulo-sedes"
          antetitulo="Dónde atendemos"
          titulo="Sedes de atención"
          descripcion="Elige el módulo que te quede más cerca. Puedes agendar en él directamente desde aquí."
        />
        {modulos.isPending && <Esqueletos texto="Cargando sedes…" />}
        {modulos.isError && <Alerta tono="error">{mensajeDeError(modulos.error)}</Alerta>}
        {conFiltro && (
          <div role="group" aria-label="Filtrar sedes por municipio" className="flex flex-wrap gap-2">
            {[null, ...municipios].map((m) => (
              <button
                key={m ?? 'todas'}
                type="button"
                aria-pressed={filtro === m}
                onClick={() => setFiltro(m)}
                className={`min-h-11 rounded-full border px-4 font-medium transition-colors ${
                  filtro === m
                    ? 'border-guinda-700 bg-guinda-700 text-white'
                    : 'border-slate-300 bg-white text-slate-800 hover:border-slate-400'
                }`}
              >
                {m ?? 'Todas'}
              </button>
            ))}
          </div>
        )}
        {visibles.length > 0 && (
          <ul className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
            {visibles.map((sede) => (
              <li
                key={sede.moduloId}
                className="flex flex-col rounded-2xl border border-slate-200 bg-white p-5 shadow-tarjeta"
              >
                <div className="flex items-start gap-3">
                  <span className="grid size-10 shrink-0 place-items-center rounded-xl bg-guinda-50 text-guinda-700">
                    <Icono nombre="sede" />
                  </span>
                  <div className="min-w-0">
                    <p className="text-sm font-semibold text-guinda-700">{sede.municipio}</p>
                    <h3 className="font-bold text-slate-950">{sede.nombre}</h3>
                    <p className="mt-1 text-slate-700">{sede.direccion}</p>
                  </div>
                </div>
                {sede.horarioAtencion && (
                  <p className="mt-3 flex items-start gap-2 text-sm text-slate-600">
                    <Icono nombre="reloj" className="mt-0.5 size-4" />
                    {sede.horarioAtencion}
                  </p>
                )}
                <div className="mt-auto flex flex-wrap items-center gap-x-4 gap-y-2 pt-5">
                  <Link
                    to={`/agendar?municipio=${sede.municipioId}&modulo=${sede.moduloId}`}
                    className={estiloBoton()}
                  >
                    Agendar aquí <span className="sr-only">en {sede.nombre}</span>
                  </Link>
                  <ComoLlegar latitud={sede.latitud} longitud={sede.longitud} sede={sede.nombre} />
                </div>
              </li>
            ))}
          </ul>
        )}
      </Contenedor>
    </section>
  );
}
