import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { useParams } from 'react-router';
import { api, datos } from '@/api/cliente';
import type { components } from '@/api/esquema';
import { copiarErroresAlFormulario } from '@/api/errores';
import { Alerta, RegionAnuncios } from '@/components/ui/Alerta';
import { Boton } from '@/components/ui/Boton';
import { Campo, Casilla } from '@/components/ui/Campo';
import { Cargando } from '@/components/ui/Encabezado';
import { AreaTexto } from '@/components/ui/Selector';
import { EncabezadoPagina, ErrorPanel } from '@/features/admin/comun/Componentes';
import { usePermisos } from '@/features/admin/comun/permisos';
import { clavesAdmin } from '@/features/admin/comun/useCatalogosAdmin';
import { EditorPlantilla } from './EditorPlantilla';
import { esquemaOferta, type ValoresOferta } from './esquemas';
import { TablaCupos } from './TablaCupos';

type OfertaAdmin = components['schemas']['OfertaAdmin'];

/** Una oferta: ventana y notas, plantilla semanal y cupos. Edita el supervisor del módulo (RF-13, RF-14). */
export function PaginaOferta() {
  const ofertaId = Number(useParams().ofertaId);
  const valido = Number.isInteger(ofertaId) && ofertaId > 0;
  const { supervisor } = usePermisos();
  // Tras un conflicto, «Recargar» trae la versión nueva y vuelve a montar el formulario con ella.
  const [recargas, setRecargas] = useState(0);

  const oferta = useQuery({
    queryKey: clavesAdmin.oferta(ofertaId),
    queryFn: async () =>
      datos(await api.GET('/api/admin/ofertas/{ofertaId}', { params: { path: { ofertaId } } })),
    enabled: valido,
  });
  const plantilla = useQuery({
    queryKey: clavesAdmin.plantilla(ofertaId),
    queryFn: async () =>
      datos(await api.GET('/api/admin/ofertas/{ofertaId}/plantilla', { params: { path: { ofertaId } } })),
    enabled: valido,
    // El editor toma la plantilla una sola vez; volver a pedirla no debe borrar lo que se está editando.
    staleTime: Infinity,
  });

  return (
    <section className="flex flex-col gap-6">
      <EncabezadoPagina
        titulo={oferta.data ? `${oferta.data.tramite} · ${oferta.data.modulo}` : 'Oferta'}
        volver={{ a: '/admin/ofertas', texto: 'Volver a ofertas' }}
      />
      {oferta.isPending && <Cargando texto="Cargando la oferta…" />}
      {oferta.isError && <ErrorPanel error={oferta.error} />}
      {oferta.data && (
        <FormularioOferta
          key={recargas}
          oferta={oferta.data}
          editable={supervisor}
          alRecargar={() => void oferta.refetch().then(() => setRecargas((n) => n + 1))}
        />
      )}

      <div className="border-t border-slate-200 pt-4">
        {plantilla.isPending && <Cargando texto="Cargando la plantilla…" />}
        {plantilla.isError && <ErrorPanel error={plantilla.error} />}
        {plantilla.data && (
          <EditorPlantilla ofertaId={ofertaId} plantilla={plantilla.data} editable={supervisor} />
        )}
      </div>

      <div className="border-t border-slate-200 pt-4">
        <TablaCupos ofertaId={ofertaId} editable={supervisor} />
      </div>
    </section>
  );
}

interface FormularioOfertaProps {
  oferta: OfertaAdmin;
  editable: boolean;
  alRecargar: () => void;
}

function FormularioOferta({ oferta, editable, alRecargar }: FormularioOfertaProps) {
  const cliente = useQueryClient();
  const formulario = useForm<ValoresOferta>({
    resolver: zodResolver(esquemaOferta),
    defaultValues: {
      diasVentana: String(oferta.diasVentana),
      notasSede: oferta.notasSede ?? '',
      activo: oferta.activo,
    },
  });
  const { register, handleSubmit, formState, setError } = formulario;
  const errores = formState.errors;
  const [enCampos, setEnCampos] = useState(false);

  const guardar = useMutation({
    mutationFn: async (v: ValoresOferta) =>
      datos(
        await api.PUT('/api/admin/ofertas/{ofertaId}', {
          params: { path: { ofertaId: oferta.ofertaId } },
          body: {
            diasVentana: Number(v.diasVentana),
            notasSede: v.notasSede.trim() || null,
            activo: v.activo,
            version: oferta.version,
          },
        }),
      ),
    onSuccess: (guardada) => {
      cliente.setQueryData(clavesAdmin.oferta(guardada.ofertaId), guardada);
      void cliente.invalidateQueries({ queryKey: ['admin', 'ofertas'] });
      void cliente.invalidateQueries({ queryKey: ['admin', 'cupos', guardada.ofertaId] });
    },
    onError: (error) =>
      setEnCampos(copiarErroresAlFormulario(error, setError, ['diasVentana', 'notasSede', 'activo'])),
  });

  return (
    <form
      noValidate
      onSubmit={handleSubmit((v) => guardar.mutate(v))}
      className="flex max-w-xl flex-col gap-4"
    >
      <h2 className="text-lg font-semibold">Datos de la oferta</h2>
      <fieldset disabled={!editable} className="flex flex-col gap-4">
        <legend className="sr-only">Datos de la oferta</legend>
        <Campo
          etiqueta="Días de ventana"
          inputMode="numeric"
          ayuda="Cuántos días hacia adelante se puede agendar, de 1 a 120."
          error={errores.diasVentana?.message}
          {...register('diasVentana')}
        />
        <AreaTexto
          etiqueta="Notas de la sede (opcional)"
          ayuda="Se muestran a la persona junto con los requisitos."
          error={errores.notasSede?.message}
          {...register('notasSede')}
        />
        <Casilla etiqueta="Activa: se puede agendar en línea" {...register('activo')} />
      </fieldset>
      <RegionAnuncios>
        {guardar.isSuccess && <Alerta tono="exito">Los cambios se guardaron.</Alerta>}
        {guardar.isError && !enCampos && <ErrorPanel error={guardar.error} alRecargar={alRecargar} />}
      </RegionAnuncios>
      {editable && (
        <Boton type="submit" ocupado={guardar.isPending} textoOcupado="Guardando…" className="self-start">
          Guardar cambios
        </Boton>
      )}
    </form>
  );
}
