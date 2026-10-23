import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { api, datos } from '@/api/cliente';
import { Boton } from '@/components/ui/Boton';
import { Campo } from '@/components/ui/Campo';
import { Cargando, EncabezadoPaso } from '@/components/ui/Encabezado';
import {
  celda,
  celdaEncabezado,
  ErrorPanel,
  Paginacion,
  TablaDesplazable,
} from '@/features/admin/comun/Componentes';
import { clavesAdmin } from '@/features/admin/comun/useCatalogosAdmin';
import { idOpcional, useFiltrosUrl } from '@/features/admin/comun/useFiltrosUrl';
import { formatearInstante } from '@/lib/fechas';

const TAMANO = 50;
const FILTROS = ['desde', 'hasta', 'usuarioId', 'accion', 'entidad'] as const;

/** Bitácora de cambios y estado de los procesos programados, solo para el administrador (RF-19). */
export function PaginaAuditoria() {
  const { filtros, pagina, aplicar, irAPagina, firma } = useFiltrosUrl(FILTROS);
  const consulta = {
    desde: filtros.desde || undefined,
    hasta: filtros.hasta || undefined,
    usuarioId: idOpcional(filtros.usuarioId),
    accion: filtros.accion || undefined,
    entidad: filtros.entidad || undefined,
    pagina,
    tamano: TAMANO,
  };

  const bitacora = useQuery({
    queryKey: clavesAdmin.auditoria(consulta),
    queryFn: async () => datos(await api.GET('/api/admin/auditoria', { params: { query: consulta } })),
    placeholderData: keepPreviousData,
  });
  const procesos = useQuery({
    queryKey: clavesAdmin.procesos,
    queryFn: async () => datos(await api.GET('/api/admin/procesos')),
  });

  return (
    <section className="flex flex-col gap-6">
      <EncabezadoPaso>Auditoría</EncabezadoPaso>

      <div className="flex flex-col gap-2">
        <h2 className="text-lg font-semibold">Procesos programados</h2>
        {procesos.isPending && <Cargando texto="Cargando procesos…" />}
        {procesos.isError && <ErrorPanel error={procesos.error} />}
        {procesos.data && (
          <TablaDesplazable etiqueta="Última ejecución de cada proceso programado">
            <thead>
              <tr>
                <th scope="col" className={celdaEncabezado}>
                  Proceso
                </th>
                <th scope="col" className={celdaEncabezado}>
                  Último inicio
                </th>
                <th scope="col" className={celdaEncabezado}>
                  Último fin
                </th>
                <th scope="col" className={celdaEncabezado}>
                  Resultado
                </th>
              </tr>
            </thead>
            <tbody>
              {procesos.data.map((p) => (
                <tr key={p.nombre}>
                  <td className={`${celda} font-medium`}>{p.nombre}</td>
                  <td className={celda}>{p.ultimoInicio ? formatearInstante(p.ultimoInicio) : 'Nunca'}</td>
                  <td className={celda}>{p.ultimoFin ? formatearInstante(p.ultimoFin) : '—'}</td>
                  <td className={celda}>
                    {p.ultimoResultado ?? '—'}
                    {p.detalle && <div className="text-xs text-slate-600">{p.detalle}</div>}
                  </td>
                </tr>
              ))}
            </tbody>
          </TablaDesplazable>
        )}
      </div>

      <div className="flex flex-col gap-4">
        <h2 className="text-lg font-semibold">Bitácora</h2>
        <form
          key={firma}
          role="search"
          aria-label="Filtros de la bitácora"
          className="grid gap-3 rounded-md border border-slate-200 bg-white p-4 sm:grid-cols-2 lg:grid-cols-3"
          onSubmit={(e) => {
            e.preventDefault();
            aplicar(e.currentTarget);
          }}
        >
          <Campo etiqueta="Desde" name="desde" type="date" defaultValue={filtros.desde} />
          <Campo etiqueta="Hasta" name="hasta" type="date" defaultValue={filtros.hasta} />
          <Campo
            etiqueta="Número de usuario"
            name="usuarioId"
            inputMode="numeric"
            defaultValue={filtros.usuarioId}
            autoComplete="off"
          />
          <Campo etiqueta="Acción" name="accion" defaultValue={filtros.accion} autoComplete="off" />
          <Campo etiqueta="Entidad" name="entidad" defaultValue={filtros.entidad} autoComplete="off" />
          <div className="flex items-end">
            <Boton type="submit">Filtrar</Boton>
          </div>
        </form>

        {bitacora.isPending && <Cargando texto="Cargando la bitácora…" />}
        {bitacora.isError && <ErrorPanel error={bitacora.error} />}
        {bitacora.data && (
          <>
            {bitacora.data.elementos.length > 0 && (
              <TablaDesplazable etiqueta="Registros de la bitácora">
                <thead>
                  <tr>
                    <th scope="col" className={celdaEncabezado}>
                      Fecha
                    </th>
                    <th scope="col" className={celdaEncabezado}>
                      Usuario
                    </th>
                    <th scope="col" className={celdaEncabezado}>
                      Acción
                    </th>
                    <th scope="col" className={celdaEncabezado}>
                      Entidad
                    </th>
                    <th scope="col" className={celdaEncabezado}>
                      Cambios
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {bitacora.data.elementos.map((r) => (
                    <tr key={r.auditoriaId}>
                      <td className={celda}>{formatearInstante(r.fecha)}</td>
                      <td className={celda}>
                        {r.usuario ?? 'Sistema'}
                        {r.ip && <div className="text-xs text-slate-600">{r.ip}</div>}
                      </td>
                      <td className={celda}>{r.accion}</td>
                      <td className={celda}>
                        {r.entidad}
                        {r.entidadId && <span className="text-slate-600"> #{r.entidadId}</span>}
                      </td>
                      <td className={celda}>
                        {r.cambios ? (
                          <details>
                            <summary className="cursor-pointer text-guinda-700">Ver cambios</summary>
                            <pre className="mt-1 max-w-md overflow-x-auto whitespace-pre-wrap break-all text-xs">
                              {r.cambios}
                            </pre>
                          </details>
                        ) : (
                          '—'
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </TablaDesplazable>
            )}
            <Paginacion
              pagina={bitacora.data.pagina}
              tamano={bitacora.data.tamano}
              total={bitacora.data.total}
              alCambiar={irAPagina}
            />
          </>
        )}
      </div>
    </section>
  );
}
