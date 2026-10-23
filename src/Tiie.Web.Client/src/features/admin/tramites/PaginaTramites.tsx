import { Link } from 'react-router';
import { Cargando } from '@/components/ui/Encabezado';
import {
  celda,
  celdaEncabezado,
  EncabezadoPagina,
  ErrorPanel,
  TablaDesplazable,
} from '@/features/admin/comun/Componentes';
import { usePermisos } from '@/features/admin/comun/permisos';
import { formatearMoneda } from '@/features/admin/comun/textos';
import { useTiposTramiteAdmin } from '@/features/admin/comun/useCatalogosAdmin';
import { estiloBoton } from '@/components/ui/Boton';
import { Icono } from '@/components/ui/Icono';

/** Tipos de trámite con su costo y cuántas ofertas los usan; el administrador los edita (RF-12). */
export function PaginaTramites() {
  const { administrador } = usePermisos();
  const tipos = useTiposTramiteAdmin();

  return (
    <section className="flex flex-col gap-4">
      <EncabezadoPagina
        titulo="Trámites"
        descripcion="Tipos de trámite, su costo y sus requisitos por nivel escolar."
        acciones={
          administrador && (
            <Link to="/admin/tramites/nuevo" className={estiloBoton()}>
              <Icono nombre="mas" />
              Nuevo trámite
            </Link>
          )
        }
      />
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
