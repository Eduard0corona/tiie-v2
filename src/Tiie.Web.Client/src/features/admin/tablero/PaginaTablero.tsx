import { useQuery } from '@tanstack/react-query';
import { useSearchParams } from 'react-router';
import { api, datos } from '@/api/cliente';
import { Cargando, EncabezadoPaso } from '@/components/ui/Encabezado';
import { Selector } from '@/components/ui/Selector';
import { celda, celdaEncabezado, ErrorPanel, TablaDesplazable } from '@/features/admin/comun/Componentes';
import { TEXTO_ESTADO } from '@/features/admin/comun/textos';
import { clavesAdmin, useModulosAdmin } from '@/features/admin/comun/useCatalogosAdmin';
import { idOpcional } from '@/features/admin/comun/useFiltrosUrl';
import { formatearDiaSemana } from '@/lib/fechas';

/** Barra de proporción dentro de una celda; el número va como texto al lado, así que la barra es decorativa. */
function Barra({ valor }: { valor: number }) {
  const ancho = Math.max(0, Math.min(100, valor));
  return (
    <div aria-hidden="true" className="h-3 w-full min-w-24 rounded-sm bg-slate-100">
      <div className="h-3 rounded-sm bg-guinda-700" style={{ width: `${ancho}%` }} />
    </div>
  );
}

const porcentaje = (parte: number, total: number) => (total > 0 ? Math.round((100 * parte) / total) : 0);

/** Indicadores: citas de hoy por estado, ocupación de los próximos 7 días e inasistencia de 30 días (RF-18). */
export function PaginaTablero() {
  const [parametros, setParametros] = useSearchParams();
  const moduloId = idOpcional(parametros.get('moduloId'));
  const modulos = useModulosAdmin();

  const tablero = useQuery({
    queryKey: clavesAdmin.tablero(moduloId),
    queryFn: async () => datos(await api.GET('/api/admin/tablero', { params: { query: { moduloId } } })),
  });

  return (
    <section className="flex flex-col gap-6">
      <EncabezadoPaso>Tablero</EncabezadoPaso>
      <Selector
        etiqueta="Módulo"
        className="max-w-md"
        value={moduloId ?? ''}
        onChange={(e) => setParametros(e.target.value ? { moduloId: e.target.value } : {}, { replace: true })}
      >
        <option value="">Todos mis módulos</option>
        {modulos.data?.map((m) => (
          <option key={m.moduloId} value={m.moduloId}>
            {m.nombre}
          </option>
        ))}
      </Selector>

      {tablero.isPending && <Cargando texto="Cargando indicadores…" />}
      {tablero.isError && <ErrorPanel error={tablero.error} />}
      {tablero.data && (
        <>
          <div className="flex flex-col gap-2">
            <h2 className="text-lg font-semibold">Citas de hoy</h2>
            {tablero.data.hoy.length === 0 ? (
              <p className="text-slate-700">No hay citas hoy.</p>
            ) : (
              <ul className="grid grid-cols-2 gap-3 sm:grid-cols-5">
                {tablero.data.hoy.map((t) => (
                  <li key={t.estado} className="rounded-md border border-slate-200 bg-white p-3">
                    <span className="block text-sm text-slate-600">{TEXTO_ESTADO[t.estado]}</span>
                    <span className="text-2xl font-bold">{t.citas}</span>
                  </li>
                ))}
              </ul>
            )}
          </div>

          <div className="flex flex-col gap-2">
            <h2 className="text-lg font-semibold">Ocupación de los próximos 7 días</h2>
            <TablaDesplazable etiqueta="Ocupación de los próximos 7 días">
              <thead>
                <tr>
                  <th scope="col" className={celdaEncabezado}>
                    Día
                  </th>
                  <th scope="col" className={celdaEncabezado}>
                    Ocupados de capacidad
                  </th>
                  <th scope="col" className={`${celdaEncabezado} w-1/3`}>
                    Ocupación
                  </th>
                </tr>
              </thead>
              <tbody>
                {tablero.data.proximos7Dias.map((d) => {
                  const ocupacion = porcentaje(d.ocupados, d.capacidad);
                  return (
                    <tr key={d.fecha}>
                      <td className={celda}>
                        <span className="first-letter:uppercase">{formatearDiaSemana(d.fecha)}</span>
                      </td>
                      <td className={celda}>
                        {d.ocupados} de {d.capacidad}
                      </td>
                      <td className={celda}>
                        <div className="flex items-center gap-2">
                          <Barra valor={ocupacion} />
                          <span className="w-12 text-right">{ocupacion} %</span>
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </TablaDesplazable>
          </div>

          <div className="flex flex-col gap-2">
            <h2 className="text-lg font-semibold">Inasistencia de los últimos 30 días</h2>
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
                    <th scope="col" className={`${celdaEncabezado} w-1/3`}>
                      Inasistencia
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {tablero.data.inasistencia30Dias.map((m) => (
                    <tr key={m.moduloId}>
                      <td className={celda}>{m.modulo}</td>
                      <td className={celda}>
                        {m.inasistencias} de {m.citas}
                      </td>
                      <td className={celda}>
                        <div className="flex items-center gap-2">
                          <Barra valor={m.porcentaje} />
                          <span className="w-14 text-right">{m.porcentaje.toLocaleString('es-MX')} %</span>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </TablaDesplazable>
            )}
          </div>
        </>
      )}
    </section>
  );
}
