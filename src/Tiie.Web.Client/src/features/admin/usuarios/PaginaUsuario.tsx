import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { Link, useLocation, useNavigate, useParams } from 'react-router';
import { api, datos } from '@/api/cliente';
import type { components } from '@/api/esquema';
import { ErrorApi, copiarErroresAlFormulario } from '@/api/errores';
import { Alerta, RegionAnuncios } from '@/components/ui/Alerta';
import { Boton } from '@/components/ui/Boton';
import { Campo, Casilla } from '@/components/ui/Campo';
import { Cargando, EncabezadoPaso } from '@/components/ui/Encabezado';
import { Selector } from '@/components/ui/Selector';
import { DialogoConfirmar, ErrorPanel } from '@/features/admin/comun/Componentes';
import { clavesAdmin, useCatalogosAdmin, useModulosAdmin } from '@/features/admin/comun/useCatalogosAdmin';
import { formatearInstante } from '@/lib/fechas';
import {
  CAMPOS_USUARIO,
  USUARIO_VACIO,
  esquemaUsuario,
  valoresDeUsuario,
  type ValoresUsuario,
} from './esquemas';

type UsuarioDetalle = components['schemas']['UsuarioDetalle'];
type Accion = 'activacion' | 'invitacion' | 'mfa';

/** Alta o edición de una cuenta del personal, con activación, invitación y MFA (RF-16). */
export function PaginaUsuario() {
  const parametro = useParams().usuarioId;
  const nuevo = parametro === 'nuevo';
  const usuarioId = Number(parametro);
  const ubicacion = useLocation();
  const aviso = (ubicacion.state as { aviso?: string } | null)?.aviso;
  // Tras un conflicto, «Recargar» trae la versión nueva y vuelve a montar el formulario con ella.
  const [recargas, setRecargas] = useState(0);

  const usuario = useQuery({
    queryKey: clavesAdmin.usuario(usuarioId),
    queryFn: async () =>
      datos(await api.GET('/api/admin/usuarios/{usuarioId}', { params: { path: { usuarioId } } })),
    enabled: !nuevo && Number.isInteger(usuarioId) && usuarioId > 0,
  });

  return (
    <section className="flex max-w-3xl flex-col gap-4">
      <Link to="/admin/usuarios" className="text-guinda-700 underline">
        Volver a usuarios
      </Link>
      <EncabezadoPaso>{nuevo ? 'Nuevo usuario' : (usuario.data?.usuario ?? 'Usuario')}</EncabezadoPaso>
      <RegionAnuncios>{aviso && <Alerta tono="exito">{aviso}</Alerta>}</RegionAnuncios>
      {nuevo && <FormularioUsuario detalle={null} />}
      {!nuevo && usuario.isPending && <Cargando texto="Cargando el usuario…" />}
      {!nuevo && usuario.isError && <ErrorPanel error={usuario.error} />}
      {usuario.data && (
        <>
          <EstadoCuenta detalle={usuario.data} />
          <FormularioUsuario
            key={recargas}
            detalle={usuario.data}
            alRecargar={() => void usuario.refetch().then(() => setRecargas((n) => n + 1))}
          />
        </>
      )}
    </section>
  );
}

function FormularioUsuario({
  detalle,
  alRecargar,
}: {
  detalle: UsuarioDetalle | null;
  alRecargar?: () => void;
}) {
  const catalogos = useCatalogosAdmin();
  const modulos = useModulosAdmin();
  const cliente = useQueryClient();
  const navegar = useNavigate();
  const [enCampos, setEnCampos] = useState(false);
  const formulario = useForm<ValoresUsuario>({
    resolver: zodResolver(esquemaUsuario),
    defaultValues: detalle ? valoresDeUsuario(detalle) : USUARIO_VACIO,
  });
  const { register, handleSubmit, formState, setError, getValues } = formulario;
  const errores = formState.errors;

  const guardar = useMutation({
    mutationFn: async (v: ValoresUsuario) => {
      const comunes = {
        correo: v.correo.trim(),
        nombre: v.nombre.trim(),
        apellidoPaterno: v.apellidoPaterno.trim(),
        apellidoMaterno: v.apellidoMaterno.trim() || null,
        rol: v.rol,
        moduloIds: v.moduloIds.map(Number),
      };
      return detalle
        ? datos(
            await api.PUT('/api/admin/usuarios/{usuarioId}', {
              params: { path: { usuarioId: detalle.usuarioId } },
              body: { ...comunes, version: detalle.version },
            }),
          )
        : datos(await api.POST('/api/admin/usuarios', { body: { ...comunes, usuario: v.usuario.trim() } }));
    },
    onSuccess: (guardado) => {
      cliente.setQueryData(clavesAdmin.usuario(guardado.usuarioId), guardado);
      void cliente.invalidateQueries({ queryKey: ['admin', 'usuarios'] });
      if (!detalle) {
        void navegar(`/admin/usuarios/${guardado.usuarioId}`, {
          replace: true,
          state: { aviso: `La cuenta se creó y se envió la invitación a ${guardado.correo}.` },
        });
      }
    },
    onError: (error) => {
      void cliente.invalidateQueries({ queryKey: ['admin', 'usuarios'] });
      setEnCampos(copiarErroresAlFormulario(error, setError, CAMPOS_USUARIO));
    },
  });

  if (catalogos.isPending || modulos.isPending) return <Cargando texto="Cargando catálogos…" />;
  if (catalogos.isError) return <ErrorPanel error={catalogos.error} />;

  // La cuenta quedó creada aunque el correo no salió: no se vuelve a enviar el formulario.
  if (!detalle && guardar.error instanceof ErrorApi && guardar.error.codigo === 'CorreoNoDisponible') {
    const creadoId = guardar.error.problema?.usuarioId;
    return (
      <Alerta tono="info" titulo="La cuenta se creó, pero la invitación no salió.">
        <p>El servicio de correo no respondió. Abre la cuenta y usa «Reenviar invitación» más tarde.</p>
        {creadoId ? (
          <Link to={`/admin/usuarios/${creadoId}`} className="text-guinda-700 underline">
            Abrir la cuenta {getValues('usuario')}
          </Link>
        ) : (
          <Link
            to={`/admin/usuarios?busqueda=${encodeURIComponent(getValues('usuario'))}`}
            className="text-guinda-700 underline"
          >
            Buscar la cuenta {getValues('usuario')}
          </Link>
        )}
      </Alerta>
    );
  }

  return (
    <form noValidate onSubmit={handleSubmit((v) => guardar.mutate(v))} className="flex flex-col gap-4">
      <div className="grid gap-4 sm:grid-cols-2">
        <Campo
          etiqueta="Usuario"
          autoComplete="off"
          spellCheck={false}
          // Solo lectura y no deshabilitado: un campo deshabilitado no llega al formulario.
          readOnly={detalle !== null}
          ayuda={detalle ? 'El usuario no se puede cambiar.' : 'Con él entra al panel.'}
          error={errores.usuario?.message}
          {...register('usuario')}
        />
        <Campo
          etiqueta="Correo"
          type="email"
          autoComplete="off"
          error={errores.correo?.message}
          {...register('correo')}
        />
        <Campo
          etiqueta="Nombre o nombres"
          autoComplete="off"
          error={errores.nombre?.message}
          {...register('nombre')}
        />
        <Campo
          etiqueta="Primer apellido"
          autoComplete="off"
          error={errores.apellidoPaterno?.message}
          {...register('apellidoPaterno')}
        />
        <Campo
          etiqueta="Segundo apellido (opcional)"
          autoComplete="off"
          error={errores.apellidoMaterno?.message}
          {...register('apellidoMaterno')}
        />
        <Selector etiqueta="Rol" error={errores.rol?.message} {...register('rol')}>
          <option value="">Elige el rol</option>
          {catalogos.data.roles.map((r) => (
            <option key={r} value={r}>
              {r}
            </option>
          ))}
        </Selector>
      </div>
      <fieldset className="flex flex-col rounded-md border border-slate-200 bg-white p-4">
        <legend className="px-1 font-semibold">Módulos asignados</legend>
        <p className="text-sm text-slate-600">El administrador ve todos los módulos aunque no se marquen.</p>
        {modulos.data?.map((m) => (
          <Casilla
            key={m.moduloId}
            etiqueta={m.nombre}
            value={String(m.moduloId)}
            {...register('moduloIds')}
          />
        ))}
        {errores.moduloIds?.message && (
          <p className="text-sm font-medium text-red-800">{errores.moduloIds.message}</p>
        )}
      </fieldset>
      <RegionAnuncios>
        {detalle && guardar.isSuccess && (
          <Alerta tono="exito">
            Los cambios se guardaron. La sesión del usuario se actualiza en unos minutos.
          </Alerta>
        )}
        {guardar.isError && !enCampos && <ErrorPanel error={guardar.error} alRecargar={alRecargar} />}
      </RegionAnuncios>
      <Boton type="submit" ocupado={guardar.isPending} textoOcupado="Guardando…" className="self-start">
        {detalle ? 'Guardar cambios' : 'Crear usuario y enviar invitación'}
      </Boton>
    </form>
  );
}

/** Estado de la cuenta y acciones que no cambian sus datos; todas piden confirmación. */
function EstadoCuenta({ detalle }: { detalle: UsuarioDetalle }) {
  const cliente = useQueryClient();
  const [accion, setAccion] = useState<Accion | null>(null);
  const [aviso, setAviso] = useState<string | null>(null);
  const id = detalle.usuarioId;

  const terminar = (texto: string) => {
    setAccion(null);
    setAviso(texto);
    void cliente.invalidateQueries({ queryKey: clavesAdmin.usuario(id) });
    void cliente.invalidateQueries({ queryKey: ['admin', 'usuarios'] });
  };

  const activar = useMutation({
    mutationFn: async (activo: boolean) =>
      datos(
        await api.POST('/api/admin/usuarios/{usuarioId}/activacion', {
          params: { path: { usuarioId: id } },
          body: { activo },
        }),
      ),
    onSuccess: (u) => terminar(u.activo ? 'La cuenta quedó activa.' : 'La cuenta quedó desactivada.'),
  });
  const invitar = useMutation({
    mutationFn: async () => {
      await api.POST('/api/admin/usuarios/{usuarioId}/invitacion', { params: { path: { usuarioId: id } } });
    },
    onSuccess: () => terminar(`Se volvió a enviar la invitación a ${detalle.correo}.`),
  });
  const restablecerMfa = useMutation({
    mutationFn: async () => {
      await api.POST('/api/admin/usuarios/{usuarioId}/mfa/restablecimiento', {
        params: { path: { usuarioId: id } },
      });
    },
    onSuccess: () =>
      terminar('Se restableció la verificación en dos pasos. La configurará de nuevo al entrar.'),
  });

  const cerrar = () => {
    setAccion(null);
    activar.reset();
    invitar.reset();
    restablecerMfa.reset();
  };

  return (
    <div className="flex flex-col gap-3 rounded-md border border-slate-200 bg-white p-4">
      <h2 className="text-lg font-semibold">Estado de la cuenta</h2>
      <dl className="grid gap-2 sm:grid-cols-2">
        <div>
          <dt className="text-sm text-slate-600">Estado</dt>
          <dd className="font-medium">{detalle.activo ? 'Activa' : 'Desactivada'}</dd>
        </div>
        <div>
          <dt className="text-sm text-slate-600">Invitación</dt>
          <dd className="font-medium">{detalle.cuentaActivada ? 'Aceptada' : 'Pendiente'}</dd>
        </div>
        <div>
          <dt className="text-sm text-slate-600">Verificación en dos pasos</dt>
          <dd className="font-medium">{detalle.mfaActivo ? 'Configurada' : 'Sin configurar'}</dd>
        </div>
        <div>
          <dt className="text-sm text-slate-600">Último acceso</dt>
          <dd className="font-medium">
            {detalle.ultimoAccesoEn ? formatearInstante(detalle.ultimoAccesoEn) : 'Nunca'}
          </dd>
        </div>
      </dl>
      <RegionAnuncios>{aviso && <Alerta tono="exito">{aviso}</Alerta>}</RegionAnuncios>
      <div className="flex flex-wrap gap-2">
        <Boton variante="secundario" onClick={() => setAccion('activacion')}>
          {detalle.activo ? 'Desactivar cuenta' : 'Activar cuenta'}
        </Boton>
        {!detalle.cuentaActivada && (
          <Boton variante="secundario" onClick={() => setAccion('invitacion')}>
            Reenviar invitación
          </Boton>
        )}
        {detalle.mfaActivo && (
          <Boton variante="secundario" onClick={() => setAccion('mfa')}>
            Restablecer verificación en dos pasos
          </Boton>
        )}
      </div>

      {accion === 'activacion' && (
        <DialogoConfirmar
          titulo={detalle.activo ? '¿Desactivar la cuenta?' : '¿Activar la cuenta?'}
          descripcion={
            detalle.activo
              ? `${detalle.usuario} ya no podrá entrar al panel; su sesión abierta termina en unos minutos.`
              : `${detalle.usuario} podrá volver a entrar al panel.`
          }
          textoConfirmar={detalle.activo ? 'Desactivar' : 'Activar'}
          ocupado={activar.isPending}
          error={activar.error}
          alConfirmar={() => activar.mutate(!detalle.activo)}
          alCerrar={cerrar}
        />
      )}
      {accion === 'invitacion' && (
        <DialogoConfirmar
          titulo="¿Reenviar la invitación?"
          descripcion={`Se enviará el enlace de activación a ${detalle.correo}.`}
          textoConfirmar="Reenviar"
          ocupado={invitar.isPending}
          error={invitar.error}
          alConfirmar={() => invitar.mutate()}
          alCerrar={cerrar}
        />
      )}
      {accion === 'mfa' && (
        <DialogoConfirmar
          titulo="¿Restablecer la verificación en dos pasos?"
          descripcion="Sus códigos actuales dejan de servir. Al entrar de nuevo, configurará el autenticador desde cero."
          textoConfirmar="Restablecer"
          ocupado={restablecerMfa.isPending}
          error={restablecerMfa.error}
          alConfirmar={() => restablecerMfa.mutate()}
          alCerrar={cerrar}
        />
      )}
    </div>
  );
}
