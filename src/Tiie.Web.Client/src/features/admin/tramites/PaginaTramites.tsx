import { Link } from 'react-router';
import { Cargando, EncabezadoPaso } from '@/components/ui/Encabezado';
import { celda, celdaEncabezado, ErrorPanel, TablaDesplazable } from '@/features/admin/comun/Componentes';
import { usePermisos } from '@/features/admin/comun/permisos';
import { formatearMoneda } from '@/features/admin/comun/textos';
import { useTiposTramiteAdmin } from '@/features/admin/comun/useCatalogosAdmin';

/** Tipos de trámite con su costo y cuántas ofertas los usan; el administrador los edita (RF-12). */
export function PaginaTramites() {
  const { administrador } = usePermisos();
  const tipos = useTiposTramiteAdmin();

  return (
    <section className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <EncabezadoPaso>Trámites</EncabezadoPaso>
        {administrador && (
          <Link
            to="/admin/tramites/nuevo"
            className="inline-flex min-h-11 items-center rounded-md bg-guinda-700 px-4 font-medium text-white hover:bg-guinda-800"
          >
            Nuevo trámite
          </Link>
        )}
      </div>
      {tipos.isPending && <Cargando texto="Cargando trámites…" />}
      {tipos.isError && <ErrorPanel error={tipos.error} />}
      {tipos.data?.length === 0 && <p className="text-slate-700">No hay trámites.</p>}
      {tipos.data && tipos.data.length > 0 && (
        <TablaDesplazable etiqueta="Tipos de trámite">
          <thead>
            <tr>
              <th scope="col" className={celdaEncabezado}>
                Trámite
              </th>
              <th scope="col" className={celdaEncabezado}>
                Costo
              </th>
              <th scope="col" className={celdaEncabezado}>
                Ofertas
              </th>
              <th scope="col" className={celdaEncabezado}>
                Estado
              </th>
            </tr>
          </thead>
          <tbody>
            {tipos.data.map((t) => (
              <tr key={t.tipoTramiteId}>
                <td className={celda}>
                  <Link
                    to={`/admin/tramites/${t.tipoTramiteId}`}
                    className="font-medium text-guinda-700 underline"
                  >
                    {t.nombre}
                  </Link>
                </td>
                <td className={celda}>{formatearMoneda(t.costo)}</td>
                <td className={celda}>{t.ofertas}</td>
                <td className={celda}>{t.activo ? 'Activo' : 'Inactivo'}</td>
              </tr>
            ))}
          </tbody>
        </TablaDesplazable>
      )}
    </section>
  );
}
