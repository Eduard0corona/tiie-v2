import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { Link, useNavigate, useParams } from 'react-router';
import { api, datos } from '@/api/cliente';
import type { components } from '@/api/esquema';
import { copiarErroresAlFormulario } from '@/api/errores';
import { Alerta, RegionAnuncios } from '@/components/ui/Alerta';
import { Boton } from '@/components/ui/Boton';
import { Campo } from '@/components/ui/Campo';
import { Cargando } from '@/components/ui/Encabezado';
import { Selector } from '@/components/ui/Selector';
import { DialogoConfirmar, EncabezadoPagina, ErrorPanel } from '@/features/admin/comun/Componentes';
import { usePermisos } from '@/features/admin/comun/permisos';
import { clavesAdmin, useCatalogosAdmin } from '@/features/admin/comun/useCatalogosAdmin';
import { hoyLocal } from '@/lib/fechas';
import {
  CAMPOS_MODULO,
  MODULO_VACIO,
  datosDeModulo,
  esquemaModulo,
  valoresDeModulo,
  type ValoresModulo,
} from './esquemas';

type ModuloDetalle = components['schemas']['ModuloDetalle'];
type ResultadoActivacion = components['schemas']['ResultadoActivacionModulo'];

/** Alta o edición de un módulo. Solo el administrador edita; los demás ven los datos (RF-11). */
export function PaginaModulo() {
  const parametro = useParams().moduloId;
  const nuevo = parametro === 'nuevo';
  const moduloId = Number(parametro);
  const { administrador } = usePermisos();
  // Tras un conflicto, «Recargar» trae la versión nueva y vuelve a montar el formulario con ella.
  const [recargas, setRecargas] = useState(0);

  const modulo = useQuery({
    queryKey: clavesAdmin.modulo(moduloId),
    queryFn: async () =>
      datos(await api.GET('/api/admin/modulos/{moduloId}', { params: { path: { moduloId } } })),
    enabled: !nuevo && Number.isInteger(moduloId) && moduloId > 0,
  });

  return (
    <section className="flex max-w-3xl flex-col gap-4">
      <EncabezadoPagina
        titulo={nuevo ? 'Nuevo módulo' : (modulo.data?.nombre ?? 'Módulo')}
        volver={{ a: '/admin/modulos', texto: 'Volver a módulos' }}
      />
      {nuevo && !administrador && <Alerta tono="error">No tienes permiso para esta acción.</Alerta>}
      {nuevo && administrador && <FormularioModulo detalle={null} editable />}
      {!nuevo && modulo.isPending && <Cargando texto="Cargando el módulo…" />}
      {!nuevo && modulo.isError && <ErrorPanel error={modulo.error} />}
      {modulo.data && (
        <>
          <FormularioModulo
            key={recargas}
            detalle={modulo.data}
            editable={administrador}
            alRecargar={() => void modulo.refetch().then(() => setRecargas((n) => n + 1))}
          />
          {administrador && <ActivacionModulo detalle={modulo.data} />}
        </>
      )}
    </section>
  );
}

interface FormularioModuloProps {
  detalle: ModuloDetalle | null;
  editable: boolean;
  alRecargar?: () => void;
}

function FormularioModulo({ detalle, editable, alRecargar }: FormularioModuloProps) {
  const catalogos = useCatalogosAdmin();
  const cliente = useQueryClient();
  const navegar = useNavigate();
  const formulario = useForm<ValoresModulo>({
    resolver: zodResolver(esquemaModulo),
    defaultValues: detalle ? valoresDeModulo(detalle) : MODULO_VACIO,
  });
  const { register, handleSubmit, formState, setError } = formulario;
  const errores = formState.errors;
  // Un 422 que se copió a los campos no se repite arriba del botón.
  const [enCampos, setEnCampos] = useState(false);

  const guardar = useMutation({
    mutationFn: async (valores: ValoresModulo) =>
      detalle
        ? datos(
            await api.PUT('/api/admin/modulos/{moduloId}', {
              params: { path: { moduloId: detalle.moduloId } },
              body: datosDeModulo(valores, detalle.version),
            }),
          )
        : datos(await api.POST('/api/admin/modulos', { body: datosDeModulo(valores, null) })),
    onSuccess: (guardado) => {
      cliente.setQueryData(clavesAdmin.modulo(guardado.moduloId), guardado);
      void cliente.invalidateQueries({ queryKey: ['admin', 'modulos'] });
      if (!detalle) void navegar(`/admin/modulos/${guardado.moduloId}`, { replace: true });
    },
    onError: (error) => setEnCampos(copiarErroresAlFormulario(error, setError, CAMPOS_MODULO)),
  });

  const errorGeneral = guardar.isError && !enCampos;

  // El municipio se elige de la lista: el formulario espera al catálogo para mostrar el valor guardado.
  if (catalogos.isPending) return <Cargando texto="Cargando catálogos…" />;
  if (catalogos.isError) return <ErrorPanel error={catalogos.error} />;

  return (
    <form noValidate onSubmit={handleSubmit((v) => guardar.mutate(v))} className="flex flex-col gap-4">
      <fieldset disabled={!editable} className="grid gap-4 sm:grid-cols-2">
        <legend className="sr-only">Datos del módulo</legend>
        <Selector etiqueta="Municipio" error={errores.municipioId?.message} {...register('municipioId')}>
          <option value="">Elige el municipio</option>
          {catalogos.data?.municipios.map((m) => (
            <option key={m.municipioId} value={m.municipioId}>
              {m.nombre}
            </option>
          ))}
        </Selector>
        <Campo etiqueta="Nombre" error={errores.nombre?.message} {...register('nombre')} />
        <Campo etiqueta="Calle" error={errores.calle?.message} {...register('calle')} />
        <div className="grid grid-cols-2 gap-4">
          <Campo
            etiqueta="Número exterior"
            error={errores.numeroExterior?.message}
            {...register('numeroExterior')}
          />
          <Campo
            etiqueta="Número interior (opcional)"
            error={errores.numeroInterior?.message}
            {...register('numeroInterior')}
          />
        </div>
        <Campo etiqueta="Colonia" error={errores.colonia?.message} {...register('colonia')} />
        <Campo
          etiqueta="Código postal"
          inputMode="numeric"
          maxLength={5}
          error={errores.codigoPostal?.message}
          {...register('codigoPostal')}
        />
        <Campo
          etiqueta="Localidad (opcional)"
          error={errores.localidad?.message}
          {...register('localidad')}
        />
        <Campo
          etiqueta="Teléfono (opcional)"
          type="tel"
          error={errores.telefono?.message}
          {...register('telefono')}
        />
        <Campo
          etiqueta="Correo (opcional)"
          type="email"
          error={errores.correo?.message}
          {...register('correo')}
        />
        <Campo
          etiqueta="Horario de atención (opcional)"
          ayuda="Por ejemplo: Lunes a viernes de 8:00 a 15:00."
          error={errores.horarioAtencion?.message}
          {...register('horarioAtencion')}
        />
        <Campo
          etiqueta="Latitud (opcional)"
          inputMode="decimal"
          error={errores.latitud?.message}
          {...register('latitud')}
        />
        <Campo
          etiqueta="Longitud (opcional)"
          inputMode="decimal"
          ayuda="Para el enlace «Cómo llegar»."
          error={errores.longitud?.message}
          {...register('longitud')}
        />
      </fieldset>
      <RegionAnuncios>
        {guardar.isSuccess && <Alerta tono="exito">Los cambios se guardaron.</Alerta>}
        {errorGeneral && <ErrorPanel error={guardar.error} alRecargar={alRecargar} />}
      </RegionAnuncios>
      {editable && (
        <Boton type="submit" ocupado={guardar.isPending} textoOcupado="Guardando…" className="self-start">
          {detalle ? 'Guardar cambios' : 'Crear módulo'}
        </Boton>
      )}
    </form>
  );
}

/** Activar o desactivar; al desactivar, la API informa cuántas citas futuras quedan por atender (RN-09). */
function ActivacionModulo({ detalle }: { detalle: ModuloDetalle }) {
  const cliente = useQueryClient();
  const [confirmando, setConfirmando] = useState(false);
  const [resultado, setResultado] = useState<ResultadoActivacion | null>(null);
  const activar = useMutation({
    mutationFn: async (activo: boolean) =>
      datos(
        await api.POST('/api/admin/modulos/{moduloId}/activacion', {
          params: { path: { moduloId: detalle.moduloId } },
          body: { activo },
        }),
      ),
    onSuccess: (r) => {
      setConfirmando(false);
      setResultado(r);
      void cliente.invalidateQueries({ queryKey: clavesAdmin.modulo(detalle.moduloId) });
      void cliente.invalidateQueries({ queryKey: ['admin', 'modulos'] });
    },
  });

  return (
    <div className="flex flex-col gap-3 border-t border-slate-200 pt-4">
      <h2 className="text-lg font-semibold">Estado del módulo</h2>
      <p>
        El módulo está <strong>{detalle.activo ? 'activo' : 'inactivo'}</strong>.
        {detalle.activo ? ' Si lo desactivas, ya no se podrá agendar en él.' : ''}
      </p>
      <RegionAnuncios>
        {resultado && (
          <Alerta tono={resultado.citasFuturas > 0 ? 'info' : 'exito'}>
            <p>El módulo quedó {resultado.activo ? 'activo' : 'inactivo'}.</p>
            {resultado.citasFuturas > 0 && (
              <p>
                Tiene{' '}
                {resultado.citasFuturas === 1
                  ? '1 cita futura agendada'
                  : `${resultado.citasFuturas} citas futuras agendadas`}
                .{' '}
                <Link
                  to={`/admin/citas?moduloId=${detalle.moduloId}&estado=Agendada&desde=${hoyLocal()}`}
                  className="text-guinda-700 underline"
                >
                  Revisar las citas
                </Link>
              </p>
            )}
          </Alerta>
        )}
      </RegionAnuncios>
      <Boton variante="secundario" className="self-start" onClick={() => setConfirmando(true)}>
        {detalle.activo ? 'Desactivar módulo' : 'Activar módulo'}
      </Boton>
      {confirmando && (
        <DialogoConfirmar
          titulo={detalle.activo ? '¿Desactivar el módulo?' : '¿Activar el módulo?'}
          descripcion={
            detalle.activo
              ? 'Ya no aparecerá en el sitio público. Las citas agendadas no se cancelan solas.'
              : 'Volverá a aparecer en el sitio público con sus trámites activos.'
          }
          textoConfirmar={detalle.activo ? 'Desactivar' : 'Activar'}
          ocupado={activar.isPending}
          error={activar.error}
          alConfirmar={() => activar.mutate(!detalle.activo)}
          alCerrar={() => {
            setConfirmando(false);
            activar.reset();
          }}
        />
      )}
    </div>
  );
}
