import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { useFieldArray, useForm, type Path } from 'react-hook-form';
import { useNavigate, useParams } from 'react-router';
import { api, datos } from '@/api/cliente';
import type { components } from '@/api/esquema';
import { copiarErroresAlFormulario } from '@/api/errores';
import { Alerta, RegionAnuncios } from '@/components/ui/Alerta';
import { Boton } from '@/components/ui/Boton';
import { Campo, Casilla } from '@/components/ui/Campo';
import { Cargando } from '@/components/ui/Encabezado';
import { AreaTexto, Selector } from '@/components/ui/Selector';
import { EncabezadoPagina, ErrorPanel } from '@/features/admin/comun/Componentes';
import { usePermisos } from '@/features/admin/comun/permisos';
import { clavesAdmin, useCatalogosAdmin } from '@/features/admin/comun/useCatalogosAdmin';
import {
  TRAMITE_VACIO,
  datosDeTramite,
  esquemaTramite,
  rutasDeFormulario,
  valoresDeTramite,
  type ValoresTramite,
} from './esquemas';

type TipoTramiteDetalle = components['schemas']['TipoTramiteDetalle'];

/** Alta o edición de un tipo de trámite con sus requisitos por nivel escolar (RF-12). */
export function PaginaTramite() {
  const parametro = useParams().tipoTramiteId;
  const nuevo = parametro === 'nuevo';
  const tipoTramiteId = Number(parametro);
  const { administrador } = usePermisos();
  // Tras un conflicto, «Recargar» trae la versión nueva y vuelve a montar el formulario con ella.
  const [recargas, setRecargas] = useState(0);

  const tramite = useQuery({
    queryKey: clavesAdmin.tipoTramite(tipoTramiteId),
    queryFn: async () =>
      datos(
        await api.GET('/api/admin/tipos-tramite/{tipoTramiteId}', { params: { path: { tipoTramiteId } } }),
      ),
    enabled: !nuevo && Number.isInteger(tipoTramiteId) && tipoTramiteId > 0,
  });

  return (
    <section className="flex max-w-3xl flex-col gap-4">
      <EncabezadoPagina
        titulo={nuevo ? 'Nuevo trámite' : (tramite.data?.nombre ?? 'Trámite')}
        volver={{ a: '/admin/tramites', texto: 'Volver a trámites' }}
      />
      {nuevo && !administrador && <Alerta tono="error">No tienes permiso para esta acción.</Alerta>}
      {nuevo && administrador && <FormularioTramite detalle={null} editable />}
      {!nuevo && tramite.isPending && <Cargando texto="Cargando el trámite…" />}
      {!nuevo && tramite.isError && <ErrorPanel error={tramite.error} />}
      {tramite.data && (
        <FormularioTramite
          key={recargas}
          detalle={tramite.data}
          editable={administrador}
          alRecargar={() => void tramite.refetch().then(() => setRecargas((n) => n + 1))}
        />
      )}
    </section>
  );
}

interface FormularioTramiteProps {
  detalle: TipoTramiteDetalle | null;
  editable: boolean;
  alRecargar?: () => void;
}

function FormularioTramite({ detalle, editable, alRecargar }: FormularioTramiteProps) {
  const catalogos = useCatalogosAdmin();
  const cliente = useQueryClient();
  const navegar = useNavigate();
  const formulario = useForm<ValoresTramite>({
    resolver: zodResolver(esquemaTramite),
    defaultValues: detalle ? valoresDeTramite(detalle) : TRAMITE_VACIO,
  });
  const { register, handleSubmit, formState, setError, control, getValues } = formulario;
  const requisitos = useFieldArray({ control, name: 'requisitos' });
  const errores = formState.errors;
  const [enCampos, setEnCampos] = useState(false);

  const guardar = useMutation({
    mutationFn: async (valores: ValoresTramite) =>
      detalle
        ? datos(
            await api.PUT('/api/admin/tipos-tramite/{tipoTramiteId}', {
              params: { path: { tipoTramiteId: detalle.tipoTramiteId } },
              body: datosDeTramite(valores, detalle.version),
            }),
          )
        : datos(await api.POST('/api/admin/tipos-tramite', { body: datosDeTramite(valores, null) })),
    onSuccess: (guardado) => {
      cliente.setQueryData(clavesAdmin.tipoTramite(guardado.tipoTramiteId), guardado);
      void cliente.invalidateQueries({ queryKey: clavesAdmin.tiposTramite });
      if (!detalle) void navegar(`/admin/tramites/${guardado.tipoTramiteId}`, { replace: true });
    },
    onError: (error) => {
      const campos: Path<ValoresTramite>[] = ['nombre', 'descripcion', 'costo', 'notas', 'requisitos'];
      getValues('requisitos').forEach((_, i) => {
        campos.push(`requisitos.${i}.texto`, `requisitos.${i}.nivelEscolarId`);
      });
      setEnCampos(copiarErroresAlFormulario(rutasDeFormulario(error), setError, campos));
    },
  });

  const errorGeneral = guardar.isError && !enCampos;

  if (catalogos.isPending) return <Cargando texto="Cargando catálogos…" />;
  if (catalogos.isError) return <ErrorPanel error={catalogos.error} />;
  const niveles = catalogos.data.nivelesEscolares;

  return (
    <form noValidate onSubmit={handleSubmit((v) => guardar.mutate(v))} className="flex flex-col gap-4">
      <fieldset disabled={!editable} className="flex flex-col gap-4">
        <legend className="sr-only">Datos del trámite</legend>
        <Campo etiqueta="Nombre" error={errores.nombre?.message} {...register('nombre')} />
        <AreaTexto
          etiqueta="Descripción (opcional)"
          error={errores.descripcion?.message}
          {...register('descripcion')}
        />
        <Campo
          etiqueta="Costo en pesos"
          inputMode="decimal"
          ayuda="Déjalo vacío o en 0 si no tiene costo."
          error={errores.costo?.message}
          {...register('costo')}
        />
        <AreaTexto
          etiqueta="Notas para la persona (opcional)"
          error={errores.notas?.message}
          {...register('notas')}
        />
        <Casilla etiqueta="Activo: se puede ofrecer y agendar" {...register('activo')} />

        <fieldset className="flex flex-col gap-3 rounded-xl border border-slate-200 bg-white p-4 shadow-sm sm:p-5">
          <legend className="px-1 font-semibold">Requisitos</legend>
          <p className="text-sm text-slate-600">
            Se muestran en este orden. Un requisito sin nivel escolar aplica a todos los niveles.
          </p>
          {requisitos.fields.length === 0 && <p className="text-slate-700">Sin requisitos.</p>}
          <ol className="flex flex-col gap-4">
            {requisitos.fields.map((campo, i) => (
              <li key={campo.id} className="flex flex-col gap-2 border-b border-slate-100 pb-4">
                <Selector
                  etiqueta={`Nivel escolar del requisito ${i + 1}`}
                  error={errores.requisitos?.[i]?.nivelEscolarId?.message}
                  {...register(`requisitos.${i}.nivelEscolarId`)}
                >
                  <option value="">Todos los niveles</option>
                  {niveles.map((n) => (
                    <option key={n.nivelEscolarId} value={n.nivelEscolarId}>
                      {n.nombre}
                    </option>
                  ))}
                </Selector>
                <AreaTexto
                  etiqueta={`Requisito ${i + 1}`}
                  rows={2}
                  error={errores.requisitos?.[i]?.texto?.message}
                  {...register(`requisitos.${i}.texto`)}
                />
                {editable && (
                  <div className="flex flex-wrap gap-2">
                    <Boton
                      variante="secundario"
                      disabled={i === 0}
                      aria-label={`Subir el requisito ${i + 1}`}
                      onClick={() => requisitos.move(i, i - 1)}
                    >
                      Subir
                    </Boton>
                    <Boton
                      variante="secundario"
                      disabled={i === requisitos.fields.length - 1}
                      aria-label={`Bajar el requisito ${i + 1}`}
                      onClick={() => requisitos.move(i, i + 1)}
                    >
                      Bajar
                    </Boton>
                    <Boton
                      variante="secundario"
                      aria-label={`Quitar el requisito ${i + 1}`}
                      onClick={() => requisitos.remove(i)}
                    >
                      Quitar
                    </Boton>
                  </div>
                )}
              </li>
            ))}
          </ol>
          {(errores.requisitos?.message ?? errores.requisitos?.root?.message) && (
            <p className="text-sm font-medium text-red-800">
              {errores.requisitos?.message ?? errores.requisitos?.root?.message}
            </p>
          )}
          {editable && (
            <Boton
              variante="secundario"
              className="self-start"
              onClick={() => requisitos.append({ nivelEscolarId: '', texto: '' })}
            >
              Agregar requisito
            </Boton>
          )}
        </fieldset>
      </fieldset>
      <RegionAnuncios>
        {guardar.isSuccess && <Alerta tono="exito">Los cambios se guardaron.</Alerta>}
        {errorGeneral && <ErrorPanel error={guardar.error} alRecargar={alRecargar} />}
      </RegionAnuncios>
      {editable && (
        <Boton type="submit" ocupado={guardar.isPending} textoOcupado="Guardando…" className="self-start">
          {detalle ? 'Guardar cambios' : 'Crear trámite'}
        </Boton>
      )}
    </form>
  );
}
