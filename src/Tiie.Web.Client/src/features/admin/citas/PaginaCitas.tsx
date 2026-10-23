import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { Link } from 'react-router';
import { api, datos } from '@/api/cliente';
import type { components } from '@/api/esquema';
import { Boton, estiloBoton } from '@/components/ui/Boton';
import { Campo } from '@/components/ui/Campo';
import { Cargando } from '@/components/ui/Encabezado';
import { Selector } from '@/components/ui/Selector';
import {
  celda,
  celdaEncabezado,
  EncabezadoPagina,
  ErrorPanel,
  EtiquetaEstado,
  Paginacion,
  TablaDesplazable,
} from '@/features/admin/comun/Componentes';
import { ESTADOS_CITA, TEXTO_ESTADO, TEXTO_ORIGEN } from '@/features/admin/comun/textos';
import { clavesAdmin, useModulosAdmin, useTiposTramiteAdmin } from '@/features/admin/comun/useCatalogosAdmin';
import { idOpcional, useFiltrosUrl } from '@/features/admin/comun/useFiltrosUrl';
import { formatearFechaCorta } from '@/lib/fechas';
import { Icono } from '@/components/ui/Icono';

type EstadoCita = components['schemas']['EstadoCita'];

const TAMANO = 20;
const FILTROS = ['busqueda', 'desde', 'hasta', 'moduloId', 'tipoTramiteId', 'estado'] as const;

/** Búsqueda de citas por folio, folio anterior o CURP, con filtros y paginación (RF-10). */
export function PaginaCitas() {
  const { filtros, pagina, aplicar, irAPagina, firma } = useFiltrosUrl(FILTROS);
  const modulos = useModulosAdmin();
  const tipos = useTiposTramiteAdmin();
  const estado = ESTADOS_CITA.find((e) => e === filtros.estado) as EstadoCita | undefined;
  const consulta = {
    busqueda: filtros.busqueda || undefined,
    desde: filtros.desde || undefined,
    hasta: filtros.hasta || undefined,
    moduloId: idOpcional(filtros.moduloId),
    tipoTramiteId: idOpcional(filtros.tipoTramiteId),
    estado,
    pagina,
    tamano: TAMANO,
  };

  const citas = useQuery({
    queryKey: clavesAdmin.citas(consulta),
    queryFn: async () => datos(await api.GET('/api/admin/citas', { params: { query: consulta } })),
    placeholderData: keepPreviousData,
  });

  return (
    <section className="flex flex-col gap-4">
      <EncabezadoPagina
        titulo="Citas"
        descripcion="Busca por folio, folio anterior o CURP, y filtra por fechas, módulo, trámite y estado."
        acciones={
          <Link to="/admin/citas/nueva" className={estiloBoton()}>
            <Icono nombre="ventanilla" />
            Registrar en ventanilla
          </Link>
        }
      />

      <form
        // Se vuelve a montar al cambiar la URL o al llegar las listas, para que los valores por omisión coincidan.
        key={`${firma}|${modulos.isSuccess}|${tipos.isSuccess}`}
        role="search"
        aria-label="Filtros de citas"
        className="grid gap-3 rounded-xl border border-slate-200 bg-white p-4 shadow-sm sm:p-5 sm:grid-cols-2 lg:grid-cols-3"
        onSubmit={(e) => {
          e.preventDefault();
          aplicar(e.currentTarget);
        }}
      >
        <Campo
          etiqueta="Folio o CURP"
          name="busqueda"
          defaultValue={filtros.busqueda}
          autoComplete="off"
          spellCheck={false}
          ayuda="También busca por el folio anterior de una cita reprogramada."
        />
        <Campo etiqueta="Desde" name="desde" type="date" defaultValue={filtros.desde} />
        <Campo etiqueta="Hasta" name="hasta" type="date" defaultValue={filtros.hasta} />
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
          <Boton type="submit">Buscar</Boton>
        </div>
      </form>

      {citas.isPending && <Cargando texto="Buscando citas…" />}
      {citas.isError && <ErrorPanel error={citas.error} />}
      {citas.data && (
        <>
          {citas.data.elementos.length > 0 && (
            <TablaDesplazable etiqueta="Citas encontradas">
              <thead>
                <tr>
                  <th scope="col" className={celdaEncabezado}>
                    Folio
                  </th>
                  <th scope="col" className={celdaEncabezado}>
                    Fecha y hora
                  </th>
                  <th scope="col" className={celdaEncabezado}>
                    Persona
                  </th>
                  <th scope="col" className={celdaEncabezado}>
                    Módulo y trámite
                  </th>
                  <th scope="col" className={celdaEncabezado}>
                    Estado
                  </th>
                </tr>
              </thead>
              <tbody>
                {citas.data.elementos.map((c) => (
                  <tr key={c.citaId}>
                    <td className={celda}>
                      <Link to={`/admin/citas/${c.citaId}`} className="font-medium text-guinda-700 underline">
                        {c.folio}
                      </Link>
                      {c.folioAnterior && (
                        <div className="text-xs text-slate-600">Antes: {c.folioAnterior}</div>
                      )}
                    </td>
                    <td className={celda}>
                      {formatearFechaCorta(c.fecha)} · {c.hora}
                    </td>
                    <td className={celda}>
                      {c.nombreCompleto}
                      <div className="font-mono text-xs text-slate-600">{c.curp}</div>
                    </td>
                    <td className={celda}>
                      {c.modulo}
                      <div className="text-xs text-slate-600">
                        {c.tramite} · {TEXTO_ORIGEN[c.origen]}
                      </div>
                    </td>
                    <td className={celda}>
                      <EtiquetaEstado estado={c.estado} />
                    </td>
                  </tr>
                ))}
              </tbody>
            </TablaDesplazable>
          )}
          <Paginacion
            pagina={citas.data.pagina}
            tamano={citas.data.tamano}
            total={citas.data.total}
            alCambiar={irAPagina}
          />
        </>
      )}
    </section>
  );
}
