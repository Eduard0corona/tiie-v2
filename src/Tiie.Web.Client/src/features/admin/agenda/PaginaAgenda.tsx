import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState, type ReactNode } from 'react';
import { Link, useSearchParams } from 'react-router';
import { api, datos } from '@/api/cliente';
import { Alerta, RegionAnuncios } from '@/components/ui/Alerta';
import { Boton, estiloBoton } from '@/components/ui/Boton';
import { Campo } from '@/components/ui/Campo';
import { Cargando } from '@/components/ui/Encabezado';
import { Icono, type NombreIcono } from '@/components/ui/Icono';
import { Insignia } from '@/components/ui/Insignia';
import { Selector } from '@/components/ui/Selector';
import {
  celda,
  celdaEncabezado,
  EncabezadoPagina,
  ErrorPanel,
  EtiquetaEstado,
  estiloPanel,
  SinResultados,
  TablaDesplazable,
} from '@/features/admin/comun/Componentes';
import { usePermisos } from '@/features/admin/comun/permisos';
import { TEXTO_ESTADO, TEXTO_ORIGEN } from '@/features/admin/comun/textos';
import { clavesAdmin } from '@/features/admin/comun/useCatalogosAdmin';
import { esFechaIso, formatearFechaLarga, horaLocal, hoyLocal, sumarDias } from '@/lib/fechas';
import { FILTROS_AGENDA, filtrarAgenda, resumirAgenda, seguimientoDeHoy, type FiltroAgenda } from './resumen';

/** Meta de operación de docs/01: citas del día con asistencia registrada. */
const META_ASISTENCIA_REGISTRADA = 95;

const ACTUALIZAR_HOY_CADA = 60_000;

const botonIcono =
  'grid size-11 shrink-0 place-items-center rounded-lg border border-slate-300 bg-white text-slate-800 shadow-sm transition-colors hover:border-slate-400 hover:bg-slate-50';

/**
 * Agenda del día de un módulo y registro de asistencia (RF-09). Por omisión, hoy y el primer módulo del
 * usuario. Solo se marca asistencia en citas Agendada de hoy o anteriores; la API lo exige también. Arriba, cómo
 * va el día; abajo, la lista con filtros por estado y búsqueda.
 */
export function PaginaAgenda() {
  const { modulos } = usePermisos();
  const [parametros, setParametros] = useSearchParams();
  const [filtro, setFiltro] = useState<FiltroAgenda>('todas');
  const [texto, setTexto] = useState('');
  const hoy = hoyLocal();
  const fechaUrl = parametros.get('fecha');
  const fecha = esFechaIso(fechaUrl) ? fechaUrl : hoy;
  const moduloId = Number(parametros.get('modulo')) || modulos[0]?.moduloId || 0;
  const cliente = useQueryClient();

  const agenda = useQuery({
    queryKey: clavesAdmin.agenda(moduloId, fecha),
    queryFn: async () =>
      datos(await api.GET('/api/admin/agenda', { params: { query: { moduloId, fecha } } })),
    enabled: moduloId > 0,
    // La de hoy cambia sola: cancelaciones en línea y citas de ventanilla de otros compañeros.
    refetchInterval: fecha === hoy ? ACTUALIZAR_HOY_CADA : false,
  });

  const marcar = useMutation({
    mutationFn: async ({ citaId, asistio }: { citaId: number; asistio: boolean; folio: string }) =>
      datos(
        await api.POST('/api/admin/citas/{citaId}/asistencia', {
          params: { path: { citaId } },
          body: { asistio },
        }),
      ),
    onSuccess: () => cliente.invalidateQueries({ queryKey: ['admin', 'agenda'] }),
  });

  const cambiar = (clave: string, valor: string) =>
    setParametros(
      (p) => {
        p.set(clave, valor);
        return p;
      },
      { replace: true },
    );

  const citas = agenda.data ?? [];
  const resumen = resumirAgenda(citas);
  const esHoy = fecha === hoy;
  const ahora = horaLocal();
  const { siguiente, atrasadas } = esHoy
    ? seguimientoDeHoy(citas, ahora)
    : { siguiente: undefined, atrasadas: fecha < hoy ? resumen.porFiltro.porAtender : 0 };
  const visibles = filtrarAgenda(citas, filtro, texto);
  const avance = resumen.programadas > 0 ? Math.round((100 * resumen.registradas) / resumen.programadas) : 0;

  return (
    <section className="flex flex-col gap-6">
      <EncabezadoPagina
        titulo="Agenda del día"
        descripcion="Registra la asistencia de las citas de tu módulo conforme llegan las personas."
        acciones={
          <Link to="/admin/citas/nueva" className={estiloBoton()}>
            <Icono nombre="ventanilla" />
            Registrar en ventanilla
          </Link>
        }
      />

      {modulos.length === 0 ? (
        <Alerta>No tienes módulos asignados. Pide a un administrador que te asigne uno.</Alerta>
      ) : (
        <div
          className={`${estiloPanel} grid grid-cols-1 gap-4 md:grid-cols-[minmax(0,1fr)_auto] md:items-end`}
        >
          <Selector etiqueta="Módulo" value={moduloId} onChange={(e) => cambiar('modulo', e.target.value)}>
            {modulos.map((m) => (
              <option key={m.moduloId} value={m.moduloId}>
                {m.nombre}
              </option>
            ))}
          </Selector>
          <div className="grid grid-cols-[auto_minmax(0,1fr)_auto_auto] items-end gap-2">
            <button
              type="button"
              aria-label="Día anterior"
              className={botonIcono}
              onClick={() => cambiar('fecha', sumarDias(fecha, -1))}
            >
              <Icono nombre="chevronIzquierda" />
            </button>
            <Campo
              etiqueta="Fecha"
              type="date"
              className="min-w-0"
              value={fecha}
              onChange={(e) => cambiar('fecha', e.target.value || hoy)}
            />
            <button
              type="button"
              aria-label="Día siguiente"
              className={botonIcono}
              onClick={() => cambiar('fecha', sumarDias(fecha, 1))}
            >
              <Icono nombre="chevronDerecha" />
            </button>
            <Boton variante="secundario" disabled={esHoy} onClick={() => cambiar('fecha', hoy)}>
              Hoy
            </Boton>
          </div>
        </div>
      )}

      <RegionAnuncios>
        {marcar.isSuccess && (
          <Alerta tono="exito">
            Cita {marcar.variables.folio}: {TEXTO_ESTADO[marcar.data.estado]}.
          </Alerta>
        )}
        {marcar.isError && <ErrorPanel error={marcar.error} />}
      </RegionAnuncios>

      {moduloId > 0 && (
        <div className="flex flex-wrap items-center gap-3">
          <h2 className="text-xl font-bold text-slate-950 first-letter:uppercase">
            {formatearFechaLarga(fecha)}
          </h2>
          {esHoy && <Insignia tono="marca">Hoy</Insignia>}
        </div>
      )}
      {agenda.isPending && moduloId > 0 && <Cargando texto="Cargando la agenda…" />}
      {agenda.isError && <ErrorPanel error={agenda.error} />}
      {agenda.data && agenda.data.length === 0 && (
        <SinResultados titulo="No hay citas para este día.">
          Si llega alguien sin cita, regístralo en ventanilla.
        </SinResultados>
      )}

      {agenda.data && agenda.data.length > 0 && (
        <>
          <ul aria-label="Resumen del día" className="grid grid-cols-2 gap-3 lg:grid-cols-4">
            <Indicador icono="calendario" titulo="Citas" valor={resumen.programadas}>
              {resumen.porFiltro.canceladas > 0 &&
                `Sin contar ${resumen.porFiltro.canceladas} cancelada${resumen.porFiltro.canceladas === 1 ? '' : 's'}`}
            </Indicador>
            <Indicador icono="reloj" titulo="Por atender" valor={resumen.porFiltro.porAtender} tono="marca">
              {atrasadas > 0 ? (
                <span className="font-semibold text-amber-800">
                  {atrasadas === 1 ? '1 ya pasó su hora' : `${atrasadas} ya pasaron su hora`}
                </span>
              ) : (
                siguiente && `Siguiente a las ${siguiente.hora}`
              )}
            </Indicador>
            <Indicador icono="check" titulo="Atendidas" valor={resumen.porFiltro.atendidas} tono="exito" />
            <Indicador
              icono="prohibido"
              titulo="No asistió"
              valor={resumen.porFiltro.noAsistio}
              tono="aviso"
            />
          </ul>

          {fecha <= hoy && resumen.programadas > 0 && (
            <div className={`${estiloPanel} flex flex-col gap-2`}>
              <div className="flex flex-wrap items-baseline justify-between gap-2">
                <p className="font-semibold text-slate-900">
                  Asistencia registrada: {resumen.registradas} de {resumen.programadas}
                </p>
                <p className="text-sm text-slate-600">
                  Meta: {META_ASISTENCIA_REGISTRADA} % de las citas del día
                </p>
              </div>
              <div aria-hidden="true" className="h-2.5 overflow-hidden rounded-full bg-slate-100">
                <div
                  className={`h-full rounded-full ${avance >= META_ASISTENCIA_REGISTRADA ? 'bg-emerald-600' : 'bg-guinda-700'}`}
                  style={{ width: `${avance}%` }}
                />
              </div>
            </div>
          )}

          <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
            <fieldset className="min-w-0">
              <legend className="sr-only">Mostrar</legend>
              <div className="flex flex-wrap gap-2">
                {FILTROS_AGENDA.map((f) => (
                  <label
                    key={f.valor}
                    className={`inline-flex min-h-11 cursor-pointer items-center gap-2 rounded-full border px-4 text-sm font-semibold transition-colors has-[:focus-visible]:outline-3 has-[:focus-visible]:outline-offset-2 has-[:focus-visible]:outline-guinda-700 ${
                      filtro === f.valor
                        ? 'border-guinda-700 bg-guinda-700 text-white'
                        : 'border-slate-300 bg-white text-slate-800 hover:border-slate-400'
                    }`}
                  >
                    <input
                      type="radio"
                      name="filtro-agenda"
                      className="sr-only"
                      checked={filtro === f.valor}
                      onChange={() => setFiltro(f.valor)}
                    />
                    {f.texto}
                    <span
                      className={`rounded-full px-1.5 text-xs tabular-nums ${
                        filtro === f.valor ? 'bg-white/20' : 'bg-slate-100 text-slate-700'
                      }`}
                    >
                      {resumen.porFiltro[f.valor]}
                    </span>
                  </label>
                ))}
              </div>
            </fieldset>
            <div className="relative lg:w-80">
              <label htmlFor="buscar-en-agenda" className="sr-only">
                Buscar en la agenda
              </label>
              <Icono
                nombre="buscar"
                className="pointer-events-none absolute top-1/2 left-3 size-4.5 -translate-y-1/2 text-slate-400"
              />
              <input
                id="buscar-en-agenda"
                type="search"
                value={texto}
                onChange={(e) => setTexto(e.target.value)}
                placeholder="Nombre, folio o CURP"
                autoComplete="off"
                spellCheck={false}
                className="min-h-11 w-full rounded-lg border border-slate-300 bg-white py-2 pr-3 pl-10 text-base shadow-sm placeholder:text-slate-500 hover:border-slate-400"
              />
            </div>
          </div>

          {visibles.length === 0 ? (
            <SinResultados titulo="Ninguna cita coincide con el filtro.">
              Cambia el estado o el texto de búsqueda.
            </SinResultados>
          ) : (
            <TablaDesplazable etiqueta={`Citas del ${formatearFechaLarga(fecha)}`}>
              <thead>
                <tr>
                  <th scope="col" className={celdaEncabezado}>
                    Hora
                  </th>
                  <th scope="col" className={celdaEncabezado}>
                    Persona
                  </th>
                  <th scope="col" className={celdaEncabezado}>
                    Folio
                  </th>
                  <th scope="col" className={celdaEncabezado}>
                    Trámite
                  </th>
                  <th scope="col" className={celdaEncabezado}>
                    Estado
                  </th>
                  <th scope="col" className={celdaEncabezado}>
                    Asistencia
                  </th>
                </tr>
              </thead>
              <tbody>
                {visibles.map((cita) => {
                  const marcable = cita.estado === 'Agendada' && fecha <= hoy;
                  const esSiguiente = cita.citaId === siguiente?.citaId;
                  const atrasada = esHoy && cita.estado === 'Agendada' && cita.hora < ahora;
                  return (
                    <tr key={cita.citaId} className={esSiguiente ? 'bg-guinda-50/60' : undefined}>
                      <td className={`${celda} whitespace-nowrap`}>
                        <span className="text-base font-bold text-slate-950 tabular-nums">{cita.hora}</span>
                        {esSiguiente && (
                          <span className="mt-1 block">
                            <Insignia tono="marca">Siguiente</Insignia>
                          </span>
                        )}
                        {atrasada && (
                          <span className="mt-1 block text-xs font-semibold text-amber-800">
                            Pasó su hora
                          </span>
                        )}
                      </td>
                      <td className={celda}>
                        <span className="font-semibold text-slate-900">{cita.nombreCompleto}</span>
                        <div className="font-mono text-xs text-slate-600">{cita.curp}</div>
                      </td>
                      <td className={celda}>
                        <Link
                          to={`/admin/citas/${cita.citaId}`}
                          className="font-medium text-guinda-700 underline underline-offset-2"
                        >
                          {cita.folio}
                        </Link>
                        <div className="text-xs text-slate-600">{TEXTO_ORIGEN[cita.origen]}</div>
                      </td>
                      <td className={celda}>{cita.tramite}</td>
                      <td className={celda}>
                        <EtiquetaEstado estado={cita.estado} />
                      </td>
                      <td className={celda}>
                        {marcable ? (
                          <div className="flex flex-wrap gap-2">
                            <Boton
                              variante="exito"
                              aria-label={`Atendida: ${cita.folio}`}
                              disabled={marcar.isPending}
                              onClick={() =>
                                marcar.mutate({ citaId: cita.citaId, asistio: true, folio: cita.folio })
                              }
                            >
                              <Icono nombre="check" className="size-4" />
                              Atendida
                            </Boton>
                            <Boton
                              variante="secundario"
                              aria-label={`No asistió: ${cita.folio}`}
                              disabled={marcar.isPending}
                              onClick={() =>
                                marcar.mutate({ citaId: cita.citaId, asistio: false, folio: cita.folio })
                              }
                            >
                              No asistió
                            </Boton>
                          </div>
                        ) : (
                          <span className="text-slate-500">—</span>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </TablaDesplazable>
          )}
        </>
      )}
    </section>
  );
}

const tonosIndicador = {
  neutro: 'bg-slate-100 text-slate-600',
  marca: 'bg-guinda-50 text-guinda-700',
  exito: 'bg-emerald-50 text-emerald-700',
  aviso: 'bg-amber-50 text-amber-700',
} as const;

function Indicador({
  icono,
  titulo,
  valor,
  tono = 'neutro',
  children,
}: {
  icono: NombreIcono;
  titulo: string;
  valor: number;
  tono?: keyof typeof tonosIndicador;
  children?: ReactNode;
}) {
  return (
    <li className={`${estiloPanel} flex flex-col gap-1`}>
      <div className="flex items-center justify-between gap-2">
        <span className="text-sm font-semibold text-slate-600">{titulo}</span>
        <span className={`grid size-8 place-items-center rounded-lg ${tonosIndicador[tono]}`}>
          <Icono nombre={icono} className="size-4.5" />
        </span>
      </div>
      <span className="text-3xl font-bold text-slate-950 tabular-nums">{valor}</span>
      {children && <span className="text-sm text-slate-600">{children}</span>}
    </li>
  );
}
