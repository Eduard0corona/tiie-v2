import { Link, useSearchParams } from 'react-router';
import { Cargando, EncabezadoPaso } from '@/components/ui/Encabezado';
import { Selector } from '@/components/ui/Selector';
import { celda, celdaEncabezado, ErrorPanel, TablaDesplazable } from '@/features/admin/comun/Componentes';
import { usePermisos } from '@/features/admin/comun/permisos';
import { useModulosAdmin } from '@/features/admin/comun/useCatalogosAdmin';

const enlaceBoton =
  'inline-flex min-h-11 items-center rounded-md bg-guinda-700 px-4 font-medium text-white hover:bg-guinda-800';

/** Módulos visibles para el usuario; el administrador da de alta y edita (RF-11). */
export function PaginaModulos() {
  const { administrador } = usePermisos();
  const [parametros, setParametros] = useSearchParams();
  const filtro = parametros.get('activo');
  const activo = filtro === 'true' ? true : filtro === 'false' ? false : undefined;
  const modulos = useModulosAdmin(activo);

  return (
    <section className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <EncabezadoPaso>Módulos</EncabezadoPaso>
        {administrador && (
          <Link to="/admin/modulos/nuevo" className={enlaceBoton}>
            Nuevo módulo
          </Link>
        )}
      </div>
      <Selector
        etiqueta="Mostrar"
        className="max-w-xs"
        value={filtro ?? ''}
        onChange={(e) => setParametros(e.target.value ? { activo: e.target.value } : {}, { replace: true })}
      >
        <option value="">Todos</option>
        <option value="true">Activos</option>
        <option value="false">Inactivos</option>
      </Selector>

      {modulos.isPending && <Cargando texto="Cargando módulos…" />}
      {modulos.isError && <ErrorPanel error={modulos.error} />}
      {modulos.data?.length === 0 && <p className="text-slate-700">No hay módulos.</p>}
      {modulos.data && modulos.data.length > 0 && (
        <TablaDesplazable etiqueta="Módulos">
          <thead>
            <tr>
              <th scope="col" className={celdaEncabezado}>
                Módulo
              </th>
              <th scope="col" className={celdaEncabezado}>
                Municipio
              </th>
              <th scope="col" className={celdaEncabezado}>
                Estado
              </th>
            </tr>
          </thead>
          <tbody>
            {modulos.data.map((m) => (
              <tr key={m.moduloId}>
                <td className={celda}>
                  <Link to={`/admin/modulos/${m.moduloId}`} className="font-medium text-guinda-700 underline">
                    {m.nombre}
                  </Link>
                </td>
                <td className={celda}>{m.municipio}</td>
                <td className={celda}>{m.activo ? 'Activo' : 'Inactivo'}</td>
              </tr>
            ))}
          </tbody>
        </TablaDesplazable>
      )}
    </section>
  );
}
