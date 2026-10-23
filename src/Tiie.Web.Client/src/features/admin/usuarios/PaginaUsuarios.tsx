import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { Link } from 'react-router';
import { api, datos } from '@/api/cliente';
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
import { clavesAdmin, useCatalogosAdmin } from '@/features/admin/comun/useCatalogosAdmin';
import { useFiltrosUrl } from '@/features/admin/comun/useFiltrosUrl';
import { formatearInstante } from '@/lib/fechas';
import { Icono } from '@/components/ui/Icono';

const TAMANO = 20;
const FILTROS = ['busqueda', 'rol', 'activo'] as const;

/** Cuentas del personal, solo para el administrador (RF-16). */
export function PaginaUsuarios() {
  const { filtros, pagina, aplicar, irAPagina, firma } = useFiltrosUrl(FILTROS);
  const catalogos = useCatalogosAdmin();
  const consulta = {
    busqueda: filtros.busqueda || undefined,
    rol: filtros.rol || undefined,
    activo: filtros.activo === 'true' ? true : filtros.activo === 'false' ? false : undefined,
    pagina,
    tamano: TAMANO,
  };

  const usuarios = useQuery({
    queryKey: clavesAdmin.usuarios(consulta),
    queryFn: async () => datos(await api.GET('/api/admin/usuarios', { params: { query: consulta } })),
    placeholderData: keepPreviousData,
  });

  return (
    <section className="flex flex-col gap-4">
      <EncabezadoPagina
        titulo="Usuarios"
        descripcion="Cuentas del personal, su rol y los módulos en los que trabaja."
        acciones={
          <Link to="/admin/usuarios/nuevo" className={estiloBoton()}>
            <Icono nombre="mas" />
            Nuevo usuario
          </Link>
        }
      />

      <form
        key={`${firma}|${catalogos.isSuccess}`}
        role="search"
        aria-label="Filtros de usuarios"
        className="grid gap-3 rounded-xl border border-slate-200 bg-white p-4 shadow-sm sm:p-5 sm:grid-cols-2 lg:grid-cols-4"
        onSubmit={(e) => {
          e.preventDefault();
          aplicar(e.currentTarget);
        }}
      >
        <Campo
          etiqueta="Usuario, nombre o correo"
          name="busqueda"
          defaultValue={filtros.busqueda}
          autoComplete="off"
        />
        <Selector etiqueta="Rol" name="rol" defaultValue={filtros.rol}>
          <option value="">Todos</option>
          {catalogos.data?.roles.map((r) => (
            <option key={r} value={r}>
              {r}
            </option>
          ))}
        </Selector>
        <Selector etiqueta="Estado" name="activo" defaultValue={filtros.activo}>
          <option value="">Todos</option>
          <option value="true">Activos</option>
          <option value="false">Inactivos</option>
        </Selector>
        <div className="flex items-end">
          <Boton type="submit">Buscar</Boton>
        </div>
      </form>

      {usuarios.isPending && <Cargando texto="Cargando usuarios…" />}
      {usuarios.isError && <ErrorPanel error={usuarios.error} />}
      {usuarios.data && (
        <>
          {usuarios.data.elementos.length > 0 && (
            <TablaDesplazable etiqueta="Usuarios">
              <thead>
                <tr>
                  <th scope="col" className={celdaEncabezado}>
                    Usuario
                  </th>
                  <th scope="col" className={celdaEncabezado}>
                    Nombre y correo
                  </th>
                  <th scope="col" className={celdaEncabezado}>
                    Rol
                  </th>
                  <th scope="col" className={celdaEncabezado}>
                    Estado
                  </th>
                  <th scope="col" className={celdaEncabezado}>
                    Último acceso
                  </th>
                </tr>
              </thead>
              <tbody>
                {usuarios.data.elementos.map((u) => (
                  <tr key={u.usuarioId}>
                    <td className={celda}>
                      <Link
                        to={`/admin/usuarios/${u.usuarioId}`}
                        className="font-medium text-guinda-700 underline"
                      >
                        {u.usuario}
                      </Link>
                    </td>
                    <td className={celda}>
                      {u.nombreCompleto}
                      <div className="text-xs text-slate-600">{u.correo}</div>
                    </td>
                    <td className={celda}>{u.rol ?? 'Sin rol'}</td>
                    <td className={celda}>
                      {u.activo ? 'Activo' : 'Inactivo'}
                      {!u.cuentaActivada && (
                        <div className="text-xs text-slate-600">Invitación pendiente</div>
                      )}
                      {u.cuentaActivada && !u.mfaActivo && (
                        <div className="text-xs text-slate-600">Sin MFA</div>
                      )}
                    </td>
                    <td className={celda}>
                      {u.ultimoAccesoEn ? formatearInstante(u.ultimoAccesoEn) : 'Nunca'}
                    </td>
                  </tr>
                ))}
              </tbody>
            </TablaDesplazable>
          )}
          <Paginacion
            pagina={usuarios.data.pagina}
            tamano={usuarios.data.tamano}
            total={usuarios.data.total}
            alCambiar={irAPagina}
          />
        </>
      )}
    </section>
  );
}
