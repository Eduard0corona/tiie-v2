import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { useForm, useWatch } from 'react-hook-form';
import { Link } from 'react-router';
import { api, datos } from '@/api/cliente';
import type { components } from '@/api/esquema';
import { copiarErroresAlFormulario } from '@/api/errores';
import { Alerta, RegionAnuncios } from '@/components/ui/Alerta';
import { Boton } from '@/components/ui/Boton';
import { Campo } from '@/components/ui/Campo';
import { Cargando, EncabezadoPaso } from '@/components/ui/Encabezado';
import { AreaTexto, Selector } from '@/components/ui/Selector';
import {
  celda,
  celdaEncabezado,
  DialogoConfirmar,
  ErrorPanel,
  TablaDesplazable,
} from '@/features/admin/comun/Componentes';
import { usePermisos } from '@/features/admin/comun/permisos';
import { TEXTO_ALCANCE } from '@/features/admin/comun/textos';
import { clavesAdmin, useModulosAdmin, useOfertasAdmin } from '@/features/admin/comun/useCatalogosAdmin';
import { idOpcional, useFiltrosUrl } from '@/features/admin/comun/useFiltrosUrl';
import { formatearFechaCorta, formatearInstante, hoyLocal } from '@/lib/fechas';
import { datosDeBloqueo, esquemaBloqueo, type ValoresBloqueo } from './esquemas';

type BloqueoResumen = components['schemas']['BloqueoResumen'];
type BloqueoCreado = components['schemas']['BloqueoCreado'];

const FILTROS = ['desde', 'hasta', 'moduloId'] as const;

/** Bloqueos y días inhábiles: cierran cupos de un rango de fechas (RF-15, RN-07). */
export function PaginaBloqueos() {
  const { supervisor } = usePermisos();
  const { filtros, aplicar, firma } = useFiltrosUrl(FILTROS);
  const modulos = useModulosAdmin();
  const cliente = useQueryClient();
  const [eliminando, setEliminando] = useState<BloqueoResumen | null>(null);
  const [aviso, setAviso] = useState<string | null>(null);
  const consulta = {
    desde: filtros.desde || undefined,
    hasta: filtros.hasta || undefined,
    moduloId: idOpcional(filtros.moduloId),
  };

  const bloqueos = useQuery({
    queryKey: clavesAdmin.bloqueos(consulta),
    queryFn: async () => datos(await api.GET('/api/admin/bloqueos', { params: { query: consulta } })),
  });

  const eliminar = useMutation({
    mutationFn: async (bloqueoId: number) => {
      await api.DELETE('/api/admin/bloqueos/{bloqueoId}', { params: { path: { bloqueoId } } });
    },
    onSuccess: () => {
      setEliminando(null);
      setAviso('El bloqueo se eliminó y sus cupos se reabrieron.');
      void cliente.invalidateQueries({ queryKey: ['admin', 'bloqueos'] });
      void cliente.invalidateQueries({ queryKey: ['admin', 'cupos'] });
    },
  });

  return (
    <section className="flex flex-col gap-6">
      <EncabezadoPaso>Bloqueos y días inhábiles</EncabezadoPaso>

      <form
        key={`${firma}|${modulos.isSuccess}`}
        role="search"
        aria-label="Filtros de bloqueos"
        className="grid gap-3 rounded-md border border-slate-200 bg-white p-4 sm:grid-cols-2 lg:grid-cols-4"
        onSubmit={(e) => {
          e.preventDefault();
          aplicar(e.currentTarget);
        }}
      >
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
        <div className="flex items-end">
          <Boton type="submit">Filtrar</Boton>
        </div>
      </form>

      <RegionAnuncios>{aviso && <Alerta tono="exito">{aviso}</Alerta>}</RegionAnuncios>

      {bloqueos.isPending && <Cargando texto="Cargando bloqueos…" />}
      {bloqueos.isError && <ErrorPanel error={bloqueos.error} />}
      {bloqueos.data?.length === 0 && <p className="text-slate-700">No hay bloqueos.</p>}
      {bloqueos.data && bloqueos.data.length > 0 && (
        <TablaDesplazable etiqueta="Bloqueos">
          <thead>
            <tr>
              <th scope="col" className={celdaEncabezado}>
                Fechas
              </th>
              <th scope="col" className={celdaEncabezado}>
                Alcance
              </th>
              <th scope="col" className={celdaEncabezado}>
                Motivo
              </th>
              <th scope="col" className={celdaEncabezado}>
                Creado
              </th>
              {supervisor && (
                <th scope="col" className={celdaEncabezado}>
                  <span className="sr-only">Acciones</span>
                </th>
              )}
            </tr>
          </thead>
          <tbody>
            {bloqueos.data.map((b) => (
              <tr key={b.bloqueoId}>
                <td className={celda}>
                  {b.desde === b.hasta
                    ? formatearFechaCorta(b.desde)
                    : `${formatearFechaCorta(b.desde)} a ${formatearFechaCorta(b.hasta)}`}
                </td>
                <td className={celda}>
                  {TEXTO_ALCANCE[b.alcance]}
                  {b.modulo && <div className="text-xs text-slate-600">{b.modulo}</div>}
                  {b.oferta && <div className="text-xs text-slate-600">{b.oferta}</div>}
                </td>
                <td className={celda}>{b.motivo}</td>
                <td className={celda}>{formatearInstante(b.creadoEn)}</td>
                {supervisor && (
                  <td className={celda}>
                    <Boton
                      variante="secundario"
                      aria-label={`Eliminar el bloqueo «${b.motivo}»`}
                      onClick={() => {
                        setAviso(null);
                        setEliminando(b);
                      }}
                    >
                      Eliminar
                    </Boton>
                  </td>
                )}
              </tr>
            ))}
          </tbody>
        </TablaDesplazable>
      )}

      {supervisor && <NuevoBloqueo />}

      {eliminando && (
        <DialogoConfirmar
          titulo="¿Eliminar el bloqueo?"
          descripcion={`Se reabren los cupos de ${formatearFechaCorta(eliminando.desde)} a ${formatearFechaCorta(eliminando.hasta)}. Las citas que ya se cancelaron no se recuperan.`}
          textoConfirmar="Eliminar"
          ocupado={eliminar.isPending}
          error={eliminar.error}
          alConfirmar={() => eliminar.mutate(eliminando.bloqueoId)}
          alCerrar={() => {
            setEliminando(null);
            eliminar.reset();
          }}
        />
      )}
    </section>
  );
}

function NuevoBloqueo() {
  const { administrador } = usePermisos();
  const cliente = useQueryClient();
  const [creado, setCreado] = useState<BloqueoCreado | null>(null);
  const [enCampos, setEnCampos] = useState(false);
  const hoy = hoyLocal();
  const formulario = useForm<ValoresBloqueo>({
    resolver: zodResolver(esquemaBloqueo),
    defaultValues: {
      alcance: administrador ? 'General' : 'Modulo',
      desde: hoy,
      hasta: hoy,
      moduloId: '',
      ofertaId: '',
      motivo: '',
    },
  });
  const { register, handleSubmit, formState, setError, control, reset } = formulario;
  const errores = formState.errors;
  const alcance = useWatch({ control, name: 'alcance' });
  const moduloId = useWatch({ control, name: 'moduloId' });
  const modulos = useModulosAdmin(true);
  const ofertas = useOfertasAdmin(Number(moduloId) || null, alcance === 'Oferta' && moduloId !== '');

  const crear = useMutation({
    mutationFn: async (v: ValoresBloqueo) =>
      datos(await api.POST('/api/admin/bloqueos', { body: datosDeBloqueo(v) })),
    onSuccess: (resultado) => {
      setCreado(resultado);
      reset();
      void cliente.invalidateQueries({ queryKey: ['admin', 'bloqueos'] });
      void cliente.invalidateQueries({ queryKey: ['admin', 'cupos'] });
    },
    onError: (error) =>
      setEnCampos(
        copiarErroresAlFormulario(error, setError, ['desde', 'hasta', 'moduloId', 'ofertaId', 'motivo']),
      ),
  });

  const alcances = (['General', 'Modulo', 'Oferta'] as const).filter((a) => a !== 'General' || administrador);

  return (
    <div className="flex flex-col gap-4">
      <form
        noValidate
        aria-labelledby="titulo-nuevo-bloqueo"
        onSubmit={handleSubmit((v) => {
          setCreado(null);
          crear.mutate(v);
        })}
        className="flex flex-col gap-4 rounded-md border border-slate-200 bg-white p-4"
      >
        <h2 id="titulo-nuevo-bloqueo" className="text-lg font-semibold">
          Nuevo bloqueo
        </h2>
        <fieldset className="flex flex-col gap-1">
          <legend className="mb-1 font-medium text-slate-800">Alcance</legend>
          {alcances.map((a) => (
            <label key={a} className="flex min-h-11 items-center gap-3">
              <input type="radio" value={a} className="size-5 accent-guinda-700" {...register('alcance')} />
              {TEXTO_ALCANCE[a]}
            </label>
          ))}
        </fieldset>
        <div className="grid gap-4 sm:grid-cols-2">
          <Campo etiqueta="Desde" type="date" error={errores.desde?.message} {...register('desde')} />
          <Campo etiqueta="Hasta" type="date" error={errores.hasta?.message} {...register('hasta')} />
          {alcance !== 'General' && (
            <Selector etiqueta="Módulo" error={errores.moduloId?.message} {...register('moduloId')}>
              <option value="">Elige el módulo</option>
              {modulos.data?.map((m) => (
                <option key={m.moduloId} value={m.moduloId}>
                  {m.nombre}
                </option>
              ))}
            </Selector>
          )}
          {alcance === 'Oferta' && (
            <Selector
              etiqueta="Trámite"
              error={errores.ofertaId?.message}
              disabled={moduloId === ''}
              {...register('ofertaId')}
            >
              <option value="">Elige el trámite</option>
              {ofertas.data?.map((o) => (
                <option key={o.ofertaId} value={o.ofertaId}>
                  {o.tramite}
                </option>
              ))}
            </Selector>
          )}
        </div>
        <AreaTexto
          etiqueta="Motivo"
          maxLength={200}
          ayuda="Se muestra en el calendario público, por ejemplo «Día inhábil»."
          error={errores.motivo?.message}
          {...register('motivo')}
        />
        <RegionAnuncios>{crear.isError && !enCampos && <ErrorPanel error={crear.error} />}</RegionAnuncios>
        <Boton type="submit" ocupado={crear.isPending} textoOcupado="Creando…" className="self-start">
          Crear bloqueo
        </Boton>
      </form>
      {creado && <ResultadoBloqueo creado={creado} />}
    </div>
  );
}

/** Lo que cerró el bloqueo; las citas afectadas siguen agendadas hasta que alguien decide cancelarlas. */
function ResultadoBloqueo({ creado }: { creado: BloqueoCreado }) {
  const cliente = useQueryClient();
  const [confirmando, setConfirmando] = useState(false);
  const cancelar = useMutation({
    mutationFn: async (motivo: string) =>
      datos(
        await api.POST('/api/admin/bloqueos/{bloqueoId}/cancelacion-citas', {
          params: { path: { bloqueoId: creado.bloqueoId } },
          body: { motivo },
        }),
      ),
    onSuccess: () => {
      setConfirmando(false);
      void cliente.invalidateQueries({ queryKey: ['admin', 'citas'] });
      void cliente.invalidateQueries({ queryKey: ['admin', 'agenda'] });
    },
  });
  const afectadas = creado.citasAfectadas;

  return (
    <div className="flex flex-col gap-3" aria-live="polite">
      <Alerta tono="exito" titulo="El bloqueo se creó.">
        {creado.cuposCerrados === 1 ? 'Se cerró 1 cupo.' : `Se cerraron ${creado.cuposCerrados} cupos.`}
        {afectadas.length === 0 && ' No hay citas agendadas en esas fechas.'}
      </Alerta>
      {cancelar.isSuccess ? (
        <Alerta tono="exito">
          {cancelar.data.citasCanceladas === 1
            ? 'Se canceló 1 cita y se avisó a la persona por correo.'
            : `Se cancelaron ${cancelar.data.citasCanceladas} citas y se avisó a cada persona por correo.`}
        </Alerta>
      ) : (
        afectadas.length > 0 && (
          <>
            <h3 className="font-semibold">
              {afectadas.length === 1
                ? '1 cita agendada queda en las fechas bloqueadas'
                : `${afectadas.length} citas agendadas quedan en las fechas bloqueadas`}
            </h3>
            <TablaDesplazable etiqueta="Citas afectadas por el bloqueo">
              <thead>
                <tr>
                  <th scope="col" className={celdaEncabezado}>
                    Folio
                  </th>
                  <th scope="col" className={celdaEncabezado}>
                    Fecha y hora
                  </th>
                </tr>
              </thead>
              <tbody>
                {afectadas.map((c) => (
                  <tr key={c.citaId}>
                    <td className={celda}>
                      <Link to={`/admin/citas/${c.citaId}`} className="text-guinda-700 underline">
                        {c.folio}
                      </Link>
                    </td>
                    <td className={celda}>
                      {formatearFechaCorta(c.fecha)} · {c.hora}
                    </td>
                  </tr>
                ))}
              </tbody>
            </TablaDesplazable>
            <Boton className="self-start" onClick={() => setConfirmando(true)}>
              Cancelar y avisar
            </Boton>
          </>
        )
      )}
      {confirmando && (
        <DialogoConfirmar
          titulo="¿Cancelar las citas afectadas?"
          descripcion="Se cancelan y cada persona recibe un correo con el motivo para que agende otra fecha."
          textoConfirmar="Cancelar y avisar"
          motivo={{ etiqueta: 'Motivo para las personas', obligatorio: true }}
          ocupado={cancelar.isPending}
          error={cancelar.error}
          alConfirmar={(motivo) => cancelar.mutate(motivo)}
          alCerrar={() => {
            setConfirmando(false);
            cancelar.reset();
          }}
        />
      )}
    </div>
  );
}
