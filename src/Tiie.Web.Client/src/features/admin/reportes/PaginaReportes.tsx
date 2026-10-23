import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { api, datos } from '@/api/cliente';
import type { components } from '@/api/esquema';
import { Alerta } from '@/components/ui/Alerta';
import { Boton, estiloBoton } from '@/components/ui/Boton';
import { Campo } from '@/components/ui/Campo';
import { Cargando } from '@/components/ui/Encabezado';
import { Selector } from '@/components/ui/Selector';
import {
  celda,
  celdaEncabezado,
  EncabezadoPagina,
  ErrorPanel,
  Paginacion,
  TablaDesplazable,
} from '@/features/admin/comun/Componentes';
import { ESTADOS_CITA, TEXTO_ESTADO, TEXTO_ORIGEN } from '@/features/admin/comun/textos';
import { clavesAdmin, useModulosAdmin, useTiposTramiteAdmin } from '@/features/admin/comun/useCatalogosAdmin';
import { idOpcional, useFiltrosUrl } from '@/features/admin/comun/useFiltrosUrl';
import { esFechaIso, formatearFechaCorta, hoyLocal, inicioDeMes } from '@/lib/fechas';

type EstadoCita = components['schemas']['EstadoCita'];

const FILTROS = ['desde', 'hasta', 'moduloId', 'tipoTramiteId', 'estado'] as const;
const FILAS_POR_PAGINA = 50;

interface ConsultaReporte {
  desde: string;
  hasta: string;
  moduloId?: number | undefined;
  tipoTramiteId?: number | undefined;
  estado?: EstadoCita | undefined;
}

/** Enlace de descarga con los mismos filtros que la tabla; el servidor arma el archivo. */
export function urlDescarga(consulta: ConsultaReporte, formato: 'xlsx' | 'pdf'): string {
  const parametros = new URLSearchParams({ desde: consulta.desde, hasta: consulta.hasta });
  if (consulta.moduloId) parametros.set('moduloId', String(consulta.moduloId));
  if (consulta.tipoTramiteId) parametros.set('tipoTramiteId', String(consulta.tipoTramiteId));
  if (consulta.estado) parametros.set('estado', consulta.estado);
  parametros.set('formato', formato);
  return `/api/admin/reportes/citas?${parametros.toString()}`;
}

/** Reporte de citas por rango, módulo, trámite y estado, con descarga en Excel y PDF (RF-17). */
export function PaginaReportes() {
  const { filtros, aplicar, firma } = useFiltrosUrl(FILTROS);
  const modulos = useModulosAdmin();
  const tipos = useTiposTramiteAdmin();
  const [pagina, setPagina] = useState(1);
  const hoy = hoyLocal();
  const consulta: ConsultaReporte = {
    desde: esFechaIso(filtros.desde) ? filtros.desde : inicioDeMes(hoy),
    hasta: esFechaIso(filtros.hasta) ? filtros.hasta : hoy,
    moduloId: idOpcional(filtros.moduloId),
    tipoTramiteId: idOpcional(filtros.tipoTramiteId),
    estado: ESTADOS_CITA.find((e) => e === filtros.estado),
  };
  const rangoValido = consulta.desde <= consulta.hasta;

  const reporte = useQuery({
    queryKey: clavesAdmin.reporte(consulta),
    queryFn: async () => datos(await api.GET('/api/admin/reportes/citas', { params: { query: consulta } })),
    enabled: rangoValido,
  });

  const filas = reporte.data?.filas ?? [];
  const visibles = filas.slice((pagina - 1) * FILAS_POR_PAGINA, pagina * FILAS_POR_PAGINA);
  const total = reporte.data?.totales.reduce((suma, t) => suma + t.citas, 0) ?? 0;

  return (
    <section className="flex flex-col gap-4">
      <EncabezadoPagina
        titulo="Reportes"
        descripcion="Citas por rango de fechas, módulo, trámite y estado, con exportación a Excel y PDF."
      />
      <form
        key={`${firma}|${modulos.isSuccess}|${tipos.isSuccess}`}
        aria-label="Filtros del reporte"
        className="grid gap-3 rounded-xl border border-slate-200 bg-white p-4 shadow-sm sm:p-5 sm:grid-cols-2 lg:grid-cols-3"
        onSubmit={(e) => {
          e.preventDefault();
          setPagina(1);
          aplicar(e.currentTarget);
        }}
      >
        <Campo etiqueta="Desde" name="desde" type="date" defaultValue={consulta.desde} />
        <Campo
          etiqueta="Hasta"
          name="hasta"
          type="date"
          defaultValue={consulta.hasta}
          error={rangoValido ? undefined : 'Debe ser igual o posterior a «Desde».'}
        />
        <Selector etiqueta="Módulo" name="moduloId" defaultValue={filtros.moduloId}>
          <option value="">Todos mis módulos</option>
          {modulos.data?.map((m) => (
            <option key={m.moduloId} value={m.moduloId}>
              {m.nombre}
            </option>
          ))}
        </Selector>
        <Selector etiqueta="Trámite" name="tipoTramiteId" defaultValue={filtros.tipoTramiteId}>
          <option value="">Todos</option>
          {tipos.data?.map((t) => (
            <option key={t.tipoTramiteId} value={t.tipoTramiteId}>
              {t.nombre}
            </option>
          ))}
        </Selector>
        <Selector etiqueta="Estado" name="estado" defaultValue={filtros.estado}>
          <option value="">Todos</option>
          {ESTADOS_CITA.map((e) => (
            <option key={e} value={e}>
              {TEXTO_ESTADO[e]}
            </option>
          ))}
        </Selector>
        <div className="flex items-end">
          <Boton type="submit">Generar</Boton>
        </div>
      </form>

      {rangoValido && (
        <div className="flex flex-wrap gap-2">
          <a href={urlDescarga(consulta, 'xlsx')} className={estiloBoton({ variante: 'secundario' })}>
            Descargar Excel
          </a>
          <a href={urlDescarga(consulta, 'pdf')} className={estiloBoton({ variante: 'secundario' })}>
            Descargar PDF
          </a>
        </div>
      )}

      {reporte.isPending && rangoValido && <Cargando texto="Generando el reporte…" />}
      {reporte.isError && <ErrorPanel error={reporte.error} />}
      {reporte.data && (
        <>
          {reporte.data.truncado && (
            <Alerta tono="info" titulo="El reporte llegó al límite de filas.">
              Se incluyen las primeras {filas.length.toLocaleString('es-MX')} citas, también en los archivos.
              Acota las fechas o los filtros para ver el resto.
            </Alerta>
          )}
          <div className="flex flex-col gap-2">
            <h2 className="text-lg font-semibold">Totales por estado</h2>
            <ul className="flex flex-wrap gap-3">
              {reporte.data.totales.map((t) => (
                <li
                  key={t.estado}
                  className="rounded-xl border border-slate-200 bg-white px-4 py-2 shadow-sm"
                >
                  <span className="block text-sm text-slate-600">{TEXTO_ESTADO[t.estado]}</span>
                  <span className="text-xl font-bold">{t.citas.toLocaleString('es-MX')}</span>
                </li>
              ))}
              <li className="rounded-xl border border-slate-300 bg-slate-100 px-4 py-2">
                <span className="block text-sm text-slate-600">Total</span>
                <span className="text-xl font-bold">{total.toLocaleString('es-MX')}</span>
              </li>
            </ul>
          </div>
          {filas.length === 0 ? (
            <p className="text-slate-700">No hay citas con esos filtros.</p>
          ) : (
            <>
              <TablaDesplazable etiqueta="Citas del reporte">
                <thead>
                  <tr>
                    <th scope="col" className={celdaEncabezado}>
                      Folio
                    </th>
                    <th scope="col" className={celdaEncabezado}>
                      Fecha y hora
                    </th>
                    <th scope="col" className={celdaEncabezado}>
                      Módulo
                    </th>
                    <th scope="col" className={celdaEncabezado}>
                      Trámite
                    </th>
                    <th scope="col" className={celdaEncabezado}>
                      Estado
                    </th>
                    <th scope="col" className={celdaEncabezado}>
                      Origen
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {visibles.map((f) => (
                    <tr key={f.folio}>
                      <td className={celda}>{f.folio}</td>
                      <td className={celda}>
                        {formatearFechaCorta(f.fecha)} · {f.hora}
                      </td>
                      <td className={celda}>{f.modulo}</td>
                      <td className={celda}>{f.tramite}</td>
                      <td className={celda}>{TEXTO_ESTADO[f.estado]}</td>
                      <td className={celda}>{TEXTO_ORIGEN[f.origen]}</td>
                    </tr>
                  ))}
                </tbody>
              </TablaDesplazable>
              <Paginacion
                pagina={pagina}
                tamano={FILAS_POR_PAGINA}
                total={filas.length}
                alCambiar={setPagina}
              />
            </>
          )}
        </>
      )}
    </section>
  );
}
