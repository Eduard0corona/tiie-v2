import { Link, useSearchParams } from 'react-router';
import { Cargando } from '@/components/ui/Encabezado';
import { Selector } from '@/components/ui/Selector';
import {
  celda,
  celdaEncabezado,
  EncabezadoPagina,
  ErrorPanel,
  TablaDesplazable,
} from '@/features/admin/comun/Componentes';
import { usePermisos } from '@/features/admin/comun/permisos';
import { useModulosAdmin } from '@/features/admin/comun/useCatalogosAdmin';
import { Icono } from '@/components/ui/Icono';
import { estiloBoton } from '@/components/ui/Boton';

/** Módulos visibles para el usuario; el administrador da de alta y edita (RF-11). */
export function PaginaModulos() {
  const { administrador } = usePermisos();
  const [parametros, setParametros] = useSearchParams();
  const filtro = parametros.get('activo');
  const activo = filtro === 'true' ? true : filtro === 'false' ? false : undefined;
  const modulos = useModulosAdmin(activo);

  return (
    <section className="flex flex-col gap-4">
      <EncabezadoPagina
        titulo="Módulos"
        descripcion="Sedes de atención: la dirección, el horario y el contacto que ve el ciudadano."
        acciones={
          administrador && (
            <Link to="/admin/modulos/nuevo" className={estiloBoton()}>
              <Icono nombre="mas" />
              Nuevo módulo
            </Link>
          )
        }
      />
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
