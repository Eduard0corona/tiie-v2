import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { useFieldArray, useForm, type Path } from 'react-hook-form';
import { api, datos } from '@/api/cliente';
import type { components } from '@/api/esquema';
import { copiarErroresAlFormulario, rutasDeFormulario } from '@/api/errores';
import { Alerta, RegionAnuncios } from '@/components/ui/Alerta';
import { Boton, estiloBoton } from '@/components/ui/Boton';
import { Campo } from '@/components/ui/Campo';
import { Cargando } from '@/components/ui/Encabezado';
import { Icono } from '@/components/ui/Icono';
import { Insignia, type TonoInsignia } from '@/components/ui/Insignia';
import { AreaTexto } from '@/components/ui/Selector';
import { EncabezadoPagina, ErrorPanel, Panel } from '@/features/admin/comun/Componentes';
import { usePermisos } from '@/features/admin/comun/permisos';
import { clavesAdmin } from '@/features/admin/comun/useCatalogosAdmin';
import { clavesPublicas } from '@/features/publico/comun/usePublico';
import { AvisoPortada } from '@/features/publico/inicio/AvisoPortada';
import { Portada } from '@/features/publico/inicio/Portada';
import { PreguntasFrecuentes } from '@/features/publico/inicio/PreguntasFrecuentes';
import { formatearInstante } from '@/lib/fechas';
import {
  PREGUNTAS_MAXIMAS,
  datosDePortada,
  esquemaPortada,
  valoresDePortada,
  type ValoresPortada,
} from './esquemas';

type PortadaAdmin = components['schemas']['PortadaAdmin'];
type EstadoAviso = components['schemas']['EstadoAviso'];

const ESTADOS_AVISO: Record<EstadoAviso, { texto: string; tono: TonoInsignia }> = {
  SinAviso: { texto: 'Sin aviso', tono: 'neutro' },
  Programado: { texto: 'Programado', tono: 'marca' },
  Visible: { texto: 'Visible hoy', tono: 'exito' },
  Vencido: { texto: 'Vencido', tono: 'aviso' },
};

const TONOS: readonly { valor: ValoresPortada['avisoTono']; etiqueta: string; detalle: string }[] = [
  { valor: 'Informativo', etiqueta: 'Informativo', detalle: 'Franja guinda clara' },
  { valor: 'Importante', etiqueta: 'Importante', detalle: 'Franja ámbar, para cierres o cambios' },
];

const AYUDA_ENLACES = (
  <>
    Para un enlace escribe <code className="rounded bg-slate-100 px-1">[texto](/ruta)</code> o{' '}
    <code className="rounded bg-slate-100 px-1">[texto](https://…)</code>.
  </>
);

/**
 * Contenido de la portada del sitio (docs/17#portada-del-sitio): encabezado, aviso y preguntas frecuentes. Solo
 * el administrador lo edita; la vista previa usa los mismos componentes que el sitio.
 */
export function PaginaPortada() {
  const { administrador } = usePermisos();
  // Tras un conflicto, «Recargar» trae la versión nueva y vuelve a montar el formulario con ella.
  const [recargas, setRecargas] = useState(0);
  const portada = useQuery({
    queryKey: clavesAdmin.portada,
    queryFn: async () => datos(await api.GET('/api/admin/portada')),
    enabled: administrador,
  });

  return (
    <section className="flex flex-col gap-6">
      <EncabezadoPagina
        titulo="Portada del sitio"
        descripcion="Lo primero que ve la gente al entrar: el encabezado, un aviso y las preguntas frecuentes."
        acciones={
          <a
            href="/"
            target="_blank"
            rel="noopener noreferrer"
            className={estiloBoton({ variante: 'secundario' })}
          >
            Ver el sitio
            <Icono nombre="externo" className="size-4" />
            <span className="sr-only">(abre en una pestaña nueva)</span>
          </a>
        }
      />
      {!administrador && <Alerta tono="error">Solo el administrador edita la portada.</Alerta>}
      {administrador && portada.isPending && <Cargando texto="Cargando la portada…" />}
      {portada.isError && <ErrorPanel error={portada.error} />}
      {portada.data && (
        <EditorPortada
          key={recargas}
          portada={portada.data}
          alRecargar={() => void portada.refetch().then(() => setRecargas((n) => n + 1))}
        />
      )}
    </section>
  );
}

function EditorPortada({ portada, alRecargar }: { portada: PortadaAdmin; alRecargar: () => void }) {
  const cliente = useQueryClient();
  const [modo, setModo] = useState<'editar' | 'vista'>('editar');
  const formulario = useForm<ValoresPortada>({
    resolver: zodResolver(esquemaPortada),
    defaultValues: valoresDePortada(portada),
  });
  const { register, handleSubmit, formState, setError, control, getValues, reset } = formulario;
  const preguntas = useFieldArray({ control, name: 'preguntas' });
  const errores = formState.errors;
  const [enCampos, setEnCampos] = useState(false);

  const guardar = useMutation({
    mutationFn: async (valores: ValoresPortada) =>
      datos(await api.PUT('/api/admin/portada', { body: datosDePortada(valores, portada.version) })),
    onSuccess: (guardada) => {
      cliente.setQueryData(clavesAdmin.portada, guardada);
      void cliente.invalidateQueries({ queryKey: clavesPublicas.portada });
      reset(valoresDePortada(guardada));
    },
    onError: (error) => {
      const campos: Path<ValoresPortada>[] = [
        'etiqueta',
        'titulo',
        'tituloDestacado',
        'descripcion',
        'avisoTexto',
        'avisoTono',
        'avisoDesde',
        'avisoHasta',
        'preguntas',
      ];
      getValues('preguntas').forEach((_, i) =>
        campos.push(`preguntas.${i}.pregunta`, `preguntas.${i}.respuesta`),
      );
      const marcados = copiarErroresAlFormulario(rutasDeFormulario(error), setError, campos);
      setEnCampos(marcados);
      if (marcados) setModo('editar');
    },
  });

  const estado = ESTADOS_AVISO[portada.estadoAviso];
  const errorPreguntas = errores.preguntas?.message ?? errores.preguntas?.root?.message;

  return (
    <div className="flex flex-col gap-5">
      <div
        role="group"
        aria-label="Modo"
        className="inline-flex self-start rounded-xl border border-slate-200 bg-white p-1 shadow-sm"
      >
        {(['editar', 'vista'] as const).map((m) => (
          <button
            key={m}
            type="button"
            aria-pressed={modo === m}
            onClick={() => setModo(m)}
            className={`inline-flex min-h-10 items-center gap-2 rounded-lg px-4 font-semibold transition-colors ${
              modo === m ? 'bg-guinda-700 text-white' : 'text-slate-700 hover:bg-slate-100'
            }`}
          >
            <Icono nombre={m === 'editar' ? 'documento' : 'portada'} className="size-4" />
            {m === 'editar' ? 'Editar' : 'Vista previa'}
          </button>
        ))}
      </div>

      <form
        id="formulario-portada"
        noValidate
        hidden={modo !== 'editar'}
        onSubmit={handleSubmit(
          (v) => guardar.mutate(v),
          () => setModo('editar'),
        )}
        className="flex max-w-3xl flex-col gap-5"
      >
        <Panel
          titulo="Encabezado"
          icono="portada"
          descripcion="El mensaje principal, sobre los botones «Agendar cita» y «Ya tengo una cita»."
        >
          <Campo
            etiqueta="Etiqueta"
            ayuda="Frase corta sobre el título. Hasta 80 caracteres."
            error={errores.etiqueta?.message}
            {...register('etiqueta')}
          />
          <Campo
            etiqueta="Título"
            ayuda="Hasta 120 caracteres."
            error={errores.titulo?.message}
            {...register('titulo')}
          />
          <Campo
            etiqueta="Continuación del título (opcional)"
            ayuda="Se muestra en guinda después del título. Hasta 120 caracteres."
            error={errores.tituloDestacado?.message}
            {...register('tituloDestacado')}
          />
          <AreaTexto
            etiqueta="Descripción"
            rows={4}
            ayuda="Hasta 300 caracteres."
            error={errores.descripcion?.message}
            {...register('descripcion')}
          />
        </Panel>

        <Panel
          titulo="Aviso"
          icono="alerta"
          descripcion="Una franja sobre la portada para cierres, temporadas o cambios de requisitos. Déjalo vacío si no hay aviso."
          acciones={
            <span className="flex flex-col items-end gap-1">
              <Insignia tono={estado.tono}>{estado.texto}</Insignia>
              <span className="text-xs text-slate-500">Según lo guardado</span>
            </span>
          }
        >
          <AreaTexto
            etiqueta="Texto del aviso (opcional)"
            rows={3}
            ayuda={<>Hasta 300 caracteres. {AYUDA_ENLACES}</>}
            error={errores.avisoTexto?.message}
            {...register('avisoTexto')}
          />
          <fieldset className="flex flex-col gap-2">
            <legend className="mb-2 font-semibold text-slate-900">Tipo de aviso</legend>
            <div className="grid gap-2 sm:grid-cols-2">
              {TONOS.map((t) => (
                <label
                  key={t.valor}
                  className="flex min-h-11 cursor-pointer items-start gap-3 rounded-xl border border-slate-200 p-3 has-checked:border-guinda-600 has-checked:bg-guinda-50 has-checked:ring-1 has-checked:ring-guinda-600"
                >
                  <input
                    type="radio"
                    value={t.valor}
                    className="mt-1 size-4 accent-guinda-700"
                    {...register('avisoTono')}
                  />
                  <span className="flex flex-col">
                    <span className="font-semibold text-slate-900">{t.etiqueta}</span>
                    <span className="text-sm text-slate-600">{t.detalle}</span>
                  </span>
                </label>
              ))}
            </div>
          </fieldset>
          <div className="grid gap-4 sm:grid-cols-2">
            <Campo
              etiqueta="Mostrar desde (opcional)"
              type="date"
              ayuda="Vacío: desde que se guarda."
              error={errores.avisoDesde?.message}
              {...register('avisoDesde')}
            />
            <Campo
              etiqueta="Mostrar hasta (opcional)"
              type="date"
              ayuda="Vacío: hasta que lo quites."
              error={errores.avisoHasta?.message}
              {...register('avisoHasta')}
            />
          </div>
        </Panel>

        <Panel
          titulo="Preguntas frecuentes"
          icono="info"
          descripcion={<>Se muestran en este orden. {AYUDA_ENLACES} Sin preguntas, la sección no aparece.</>}
        >
          {preguntas.fields.length === 0 && <p className="text-slate-700">Sin preguntas.</p>}
          <ol className="flex flex-col gap-4">
            {preguntas.fields.map((campo, i) => (
              <li key={campo.id} className="flex flex-col gap-2 border-b border-slate-100 pb-4">
                <Campo
                  etiqueta={`Pregunta ${i + 1}`}
                  error={errores.preguntas?.[i]?.pregunta?.message}
                  {...register(`preguntas.${i}.pregunta`)}
                />
                <AreaTexto
                  etiqueta={`Respuesta ${i + 1}`}
                  rows={3}
                  error={errores.preguntas?.[i]?.respuesta?.message}
                  {...register(`preguntas.${i}.respuesta`)}
                />
                <div className="flex flex-wrap gap-2">
                  <Boton
                    variante="secundario"
                    disabled={i === 0}
                    aria-label={`Subir la pregunta ${i + 1}`}
                    onClick={() => preguntas.move(i, i - 1)}
                  >
                    Subir
                  </Boton>
                  <Boton
                    variante="secundario"
                    disabled={i === preguntas.fields.length - 1}
                    aria-label={`Bajar la pregunta ${i + 1}`}
                    onClick={() => preguntas.move(i, i + 1)}
                  >
                    Bajar
                  </Boton>
                  <Boton
                    variante="secundario"
                    aria-label={`Quitar la pregunta ${i + 1}`}
                    onClick={() => preguntas.remove(i)}
                  >
                    Quitar
                  </Boton>
                </div>
              </li>
            ))}
          </ol>
          {errorPreguntas && <p className="text-sm font-medium text-red-800">{errorPreguntas}</p>}
          <Boton
            variante="secundario"
            className="self-start"
            disabled={preguntas.fields.length >= PREGUNTAS_MAXIMAS}
            onClick={() => preguntas.append({ pregunta: '', respuesta: '' })}
          >
            <Icono nombre="mas" className="size-4" />
            Agregar pregunta
          </Boton>
        </Panel>
      </form>

      {modo === 'vista' && <VistaPrevia valores={getValues()} />}

      <RegionAnuncios>
        {guardar.isSuccess && !formState.isDirty && (
          <Alerta tono="exito">Los cambios se guardaron y ya se ven en el sitio.</Alerta>
        )}
        {guardar.isError && !enCampos && <ErrorPanel error={guardar.error} alRecargar={alRecargar} />}
      </RegionAnuncios>
      <div className="flex flex-wrap items-center gap-x-4 gap-y-2">
        <Boton type="submit" form="formulario-portada" ocupado={guardar.isPending} textoOcupado="Guardando…">
          Guardar cambios
        </Boton>
        <p className="text-sm text-slate-600">
          {formState.isDirty
            ? 'Hay cambios sin guardar.'
            : portada.modificadoEn
              ? `Guardado el ${formatearInstante(portada.modificadoEn)}.`
              : 'El sitio muestra el contenido predeterminado.'}
        </p>
      </div>
    </div>
  );
}

/**
 * Los mismos componentes del sitio con los valores del formulario. Es inerte: no recibe el foco ni se anuncia,
 * porque el formulario ya tiene el mismo contenido.
 */
function VistaPrevia({ valores }: { valores: ValoresPortada }) {
  const contenido = datosDePortada(valores, null);
  return (
    <section aria-labelledby="titulo-vista-previa" className="flex flex-col gap-3">
      <div className="flex flex-col gap-1">
        <h2 id="titulo-vista-previa" className="text-lg font-bold text-slate-950">
          Vista previa
        </h2>
        <p className="text-sm text-slate-600">
          Así se verá la portada con estos cambios, aún sin guardar. El aviso aparece aquí aunque no esté en
          sus fechas; en el sitio solo se muestra mientras está vigente.
        </p>
      </div>
      <div inert className="overflow-hidden rounded-2xl border border-slate-300 bg-white shadow-sm">
        {contenido.avisoTexto && (
          <AvisoPortada aviso={{ texto: contenido.avisoTexto, tono: contenido.avisoTono }} />
        )}
        <Portada encabezado={contenido} />
        <PreguntasFrecuentes preguntas={contenido.preguntas} />
      </div>
    </section>
  );
}
