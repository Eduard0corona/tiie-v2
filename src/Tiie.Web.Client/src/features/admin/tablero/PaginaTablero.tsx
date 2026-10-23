import { useQuery } from '@tanstack/react-query';
import { useSearchParams } from 'react-router';
import { api, datos } from '@/api/cliente';
import type { components } from '@/api/esquema';
import { Cargando } from '@/components/ui/Encabezado';
import { Icono, type NombreIcono } from '@/components/ui/Icono';
import { Selector } from '@/components/ui/Selector';
import {
  celda,
  celdaEncabezado,
  EncabezadoPagina,
  ErrorPanel,
  estiloPanel,
  Panel,
  SinResultados,
  TablaDesplazable,
} from '@/features/admin/comun/Componentes';
import { TEXTO_ESTADO } from '@/features/admin/comun/textos';
import { clavesAdmin, useModulosAdmin } from '@/features/admin/comun/useCatalogosAdmin';
import { idOpcional } from '@/features/admin/comun/useFiltrosUrl';
import { formatearDiaSemana } from '@/lib/fechas';

type EstadoCita = components['schemas']['EstadoCita'];

/**
 * Medidor de una proporción contra su límite: el riel es un paso claro del mismo tono que el relleno. El número
 * va como texto al lado, así que el medidor es decorativo.
 */
function Medidor({ valor }: { valor: number }) {
  const ancho = Math.max(0, Math.min(100, valor));
  return (
    <div aria-hidden="true" className="h-2.5 w-full min-w-24 overflow-hidden rounded-full bg-guinda-100">
      <div className="h-full rounded-full bg-guinda-700" style={{ width: `${ancho}%` }} />
    </div>
  );
}

const porcentaje = (parte: number, total: number) => (total > 0 ? Math.round((100 * parte) / total) : 0);

/** Los estados se leen por su ícono y su texto; el color solo acompaña, y solo cuando significa algo. */
const ESTADOS_HOY: readonly { estado: EstadoCita; icono: NombreIcono; tono: string }[] = [
  { estado: 'Agendada', icono: 'reloj', tono: 'bg-blue-50 text-blue-700' },
  { estado: 'Atendida', icono: 'check', tono: 'bg-emerald-50 text-emerald-700' },
  { estado: 'NoAsistio', icono: 'prohibido', tono: 'bg-amber-50 text-amber-700' },
  { estado: 'Cancelada', icono: 'cerrar', tono: 'bg-slate-100 text-slate-600' },
  { estado: 'Reprogramada', icono: 'cambiar', tono: 'bg-slate-100 text-slate-600' },
];

/** Indicadores: citas de hoy por estado, ocupación de los próximos 7 días e inasistencia de 30 días (RF-18). */
export function PaginaTablero() {
  const [parametros, setParametros] = useSearchParams();
  const moduloId = idOpcional(parametros.get('moduloId'));
  const modulos = useModulosAdmin();

  const tablero = useQuery({
    queryKey: clavesAdmin.tablero(moduloId),
    queryFn: async () => datos(await api.GET('/api/admin/tablero', { params: { query: { moduloId } } })),
  });

  const citasHoy = (estado: EstadoCita) => tablero.data?.hoy.find((t) => t.estado === estado)?.citas ?? 0;
  const totalHoy = tablero.data?.hoy.reduce((suma, t) => suma + t.citas, 0) ?? 0;
  // Las canceladas y reprogramadas ya no se atienden hoy: no cuentan para la asistencia registrada.
  const programadasHoy = totalHoy - citasHoy('Cancelada') - citasHoy('Reprogramada');
  const registradasHoy = citasHoy('Atendida') + citasHoy('NoAsistio');
  const semana = tablero.data?.proximos7Dias.reduce(
    (suma, d) => ({ ocupados: suma.ocupados + d.ocupados, capacidad: suma.capacidad + d.capacidad }),
    { ocupados: 0, capacidad: 0 },
  );

  return (
    <section className="flex flex-col gap-6">
      <EncabezadoPagina
        titulo="Tablero"
        descripcion="Cómo va hoy, cuánto se ha llenado la próxima semana y dónde falta más gente a su cita."
      />
      <div className={`${estiloPanel} flex flex-wrap items-end gap-4`}>
        <Selector
          etiqueta="Módulo"
          className="w-full max-w-md"
          value={moduloId ?? ''}
          onChange={(e) =>
            setParametros(e.target.value ? { moduloId: e.target.value } : {}, { replace: true })
          }
        >
          <option value="">Todos mis módulos</option>
          {modulos.data?.map((m) => (
            <option key={m.moduloId} value={m.moduloId}>
              {m.nombre}
            </option>
          ))}
        </Selector>
      </div>

      {tablero.isPending && <Cargando texto="Cargando indicadores…" />}
      {tablero.isError && <ErrorPanel error={tablero.error} />}
      {tablero.data && semana && (
        <>
          <section aria-labelledby="titulo-hoy" className="flex flex-col gap-3">
            <h2 id="titulo-hoy" className="text-lg font-bold text-slate-950">
              Citas de hoy
            </h2>
            {totalHoy === 0 ? (
              <SinResultados titulo="No hay citas hoy." />
            ) : (
              <div className="grid gap-3 lg:grid-cols-[minmax(0,16rem)_minmax(0,1fr)]">
                <div className={`${estiloPanel} flex flex-col justify-between gap-2`}>
                  <p className="text-sm font-semibold text-slate-600">Total</p>
                  <p className="text-5xl font-bold tracking-tight text-slate-950">{totalHoy}</p>
                  <p className="text-sm text-slate-600">
                    {registradasHoy} de {programadasHoy} con asistencia registrada
                  </p>
                </div>
                <ul className="grid grid-cols-2 gap-3 sm:grid-cols-3 xl:grid-cols-5">
                  {ESTADOS_HOY.map(({ estado, icono, tono }) => (
                    <li key={estado} className={`${estiloPanel} flex flex-col gap-2`}>
                      <div className="flex items-center justify-between gap-2">
                        <span className="text-sm font-semibold text-slate-600">{TEXTO_ESTADO[estado]}</span>
                        <span className={`grid size-8 place-items-center rounded-lg ${tono}`}>
                          <Icono nombre={icono} className="size-4.5" />
                        </span>
                      </div>
                      <span className="text-3xl font-bold text-slate-950">{citasHoy(estado)}</span>
                    </li>
                  ))}
                </ul>
              </div>
            )}
          </section>

          <Panel
            titulo="Ocupación de los próximos 7 días"
            icono="calendario"
            descripcion={
              semana.capacidad > 0
                ? `${semana.ocupados} de ${semana.capacidad} lugares ocupados en la semana (${porcentaje(semana.ocupados, semana.capacidad)} %).`
                : undefined
            }
          >
            <TablaDesplazable etiqueta="Ocupación de los próximos 7 días">
              <thead>
                <tr>
                  <th scope="col" className={celdaEncabezado}>
                    Día
                  </th>
                  <th scope="col" className={celdaEncabezado}>
                    Ocupados de capacidad
                  </th>
                  <th scope="col" className={`${celdaEncabezado} w-2/5`}>
                    Ocupación
                  </th>
                </tr>
              </thead>
              <tbody>
                {tablero.data.proximos7Dias.map((d) => {
                  const ocupacion = porcentaje(d.ocupados, d.capacidad);
                  return (
                    <tr key={d.fecha}>
                      <td className={`${celda} font-semibold text-slate-900`}>
                        <span className="inline-block first-letter:uppercase">
                          {formatearDiaSemana(d.fecha)}
                        </span>
                      </td>
                      <td className={`${celda} tabular-nums`}>
                        {d.capacidad > 0 ? `${d.ocupados} de ${d.capacidad}` : 'Sin servicio'}
                      </td>
                      <td className={celda}>
                        {d.capacidad > 0 && (
                          <div className="flex items-center gap-3">
                            <Medidor valor={ocupacion} />
                            <span className="w-14 shrink-0 text-right font-semibold whitespace-nowrap text-slate-900 tabular-nums">
                              {ocupacion} %
                            </span>
                          </div>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </TablaDesplazable>
          </Panel>

          <Panel
            titulo="Inasistencia de los últimos 30 días"
            icono="prohibido"
            descripcion="Citas que no se atendieron entre las que debían atenderse, por módulo. De mayor a menor."
          >
            {tablero.data.inasistencia30Dias.length === 0 ? (
              <p className="text-slate-700">Sin citas atendidas o perdidas en ese periodo.</p>
            ) : (
              <TablaDesplazable etiqueta="Inasistencia de los últimos 30 días por módulo">
                <thead>
                  <tr>
                    <th scope="col" className={celdaEncabezado}>
                      Módulo
                    </th>
                    <th scope="col" className={celdaEncabezado}>
                      Inasistencias de citas
                    </th>
                    <th scope="col" className={`${celdaEncabezado} w-2/5`}>
                      Inasistencia
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {[...tablero.data.inasistencia30Dias]
                    .sort((a, b) => b.porcentaje - a.porcentaje)
                    .map((m) => (
                      <tr key={m.moduloId}>
                        <td className={`${celda} font-semibold text-slate-900`}>{m.modulo}</td>
                        <td className={`${celda} tabular-nums`}>
                          {m.inasistencias} de {m.citas}
                        </td>
                        <td className={celda}>
                          <div className="flex items-center gap-3">
                            <Medidor valor={m.porcentaje} />
                            <span className="w-16 shrink-0 text-right font-semibold whitespace-nowrap text-slate-900 tabular-nums">
                              {m.porcentaje.toLocaleString('es-MX')} %
                            </span>
                          </div>
                        </td>
                      </tr>
                    ))}
                </tbody>
              </TablaDesplazable>
            )}
          </Panel>
        </>
      )}
    </section>
  );
}
