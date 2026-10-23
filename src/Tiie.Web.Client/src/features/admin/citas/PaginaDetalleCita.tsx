import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState, type ReactNode } from 'react';
import { Link, useLocation, useNavigate, useParams } from 'react-router';
import { api, datos } from '@/api/cliente';
import { Alerta, RegionAnuncios } from '@/components/ui/Alerta';
import { Boton, estiloBoton } from '@/components/ui/Boton';
import { Cargando } from '@/components/ui/Encabezado';
import {
  celda,
  celdaEncabezado,
  DialogoConfirmar,
  EncabezadoPagina,
  ErrorPanel,
  EtiquetaEstado,
  TablaDesplazable,
} from '@/features/admin/comun/Componentes';
import { usePermisos } from '@/features/admin/comun/permisos';
import {
  TEXTO_ESTADO,
  TEXTO_ESTADO_NOTIFICACION,
  TEXTO_ORIGEN,
  TEXTO_TIPO_NOTIFICACION,
} from '@/features/admin/comun/textos';
import { clavesAdmin } from '@/features/admin/comun/useCatalogosAdmin';
import { formatearFechaCorta, formatearFechaLarga, formatearInstante } from '@/lib/fechas';
import { DialogoReprogramar } from './DialogoReprogramar';

type Accion = 'cancelar' | 'reprogramar' | 'reenviar' | 'corregir';

/** Detalle de una cita para el personal: datos, acciones, citas relacionadas, avisos y movimientos (RF-10). */
export function PaginaDetalleCita() {
  const citaId = Number(useParams().citaId);
  const { supervisor } = usePermisos();
  const cliente = useQueryClient();
  const navegar = useNavigate();
  const ubicacion = useLocation();
  const [accion, setAccion] = useState<Accion | null>(null);
  const [aviso, setAviso] = useState<string | null>(
    (ubicacion.state as { aviso?: string } | null)?.aviso ?? null,
  );

  const cita = useQuery({
    queryKey: clavesAdmin.cita(citaId),
    queryFn: async () => datos(await api.GET('/api/admin/citas/{citaId}', { params: { path: { citaId } } })),
    enabled: Number.isInteger(citaId) && citaId > 0,
  });

  const refrescar = () =>
    Promise.all([
      cliente.invalidateQueries({ queryKey: clavesAdmin.cita(citaId) }),
      cliente.invalidateQueries({ queryKey: ['admin', 'citas'] }),
      cliente.invalidateQueries({ queryKey: ['admin', 'agenda'] }),
    ]);

  const terminar = async (texto: string) => {
    setAccion(null);
    setAviso(texto);
    await refrescar();
  };

  const cancelar = useMutation({
    mutationFn: async (motivo: string) =>
      datos(
        await api.POST('/api/admin/citas/{citaId}/cancelacion', {
          params: { path: { citaId } },
          body: { motivo },
        }),
      ),
    onSuccess: () => terminar('La cita se canceló y se avisó a la persona por correo.'),
  });

  const reenviar = useMutation({
    mutationFn: async () => {
      await api.POST('/api/admin/citas/{citaId}/reenvio', { params: { path: { citaId } } });
    },
    onSuccess: () => terminar('Se volvió a enviar la confirmación.'),
  });

  const corregir = useMutation({
    mutationFn: async ({ asistio, motivo }: { asistio: boolean; motivo: string }) =>
      datos(
        await api.POST('/api/admin/citas/{citaId}/correccion-asistencia', {
          params: { path: { citaId } },
          body: { asistio, motivo },
        }),
      ),
    onSuccess: (r) => terminar(`La asistencia se corrigió: ${TEXTO_ESTADO[r.estado]}.`),
  });

  const reprogramar = useMutation({
    mutationFn: async ({ cupoId, motivo }: { cupoId: number; motivo: string }) =>
      datos(
        await api.POST('/api/admin/citas/{citaId}/reprogramacion', {
          params: { path: { citaId } },
          body: { cupoId, motivo },
        }),
      ),
    onSuccess: async (nueva) => {
      setAccion(null);
      await refrescar();
      void navegar(`/admin/citas/${nueva.citaId}`, {
        state: { aviso: `La cita se reprogramó con el folio ${nueva.folio}.` },
      });
    },
  });

  const cerrar = () => {
    setAccion(null);
    cancelar.reset();
    reenviar.reset();
    corregir.reset();
    reprogramar.reset();
  };

  if (cita.isPending && cita.fetchStatus !== 'idle') return <Cargando texto="Cargando la cita…" />;
  if (cita.isError) return <ErrorPanel error={cita.error} />;
  if (!cita.data) return <Alerta tono="error">No encontramos lo que buscas.</Alerta>;

  const c = cita.data;
  const agendada = c.estado === 'Agendada';
  const corregible = supervisor && (c.estado === 'Atendida' || c.estado === 'NoAsistio');

  return (
    <section className="flex flex-col gap-6">
      <EncabezadoPagina
        titulo={`Cita ${c.folio}`}
        volver={{ a: '/admin/citas', texto: 'Volver a la búsqueda' }}
      >
        <div>
          <EtiquetaEstado estado={c.estado} />
        </div>
      </EncabezadoPagina>

      <RegionAnuncios>{aviso && <Alerta tono="exito">{aviso}</Alerta>}</RegionAnuncios>

      <dl className="grid gap-x-6 gap-y-3 rounded-xl border border-slate-200 bg-white p-4 shadow-sm sm:p-5 sm:grid-cols-2">
        <Dato termino="Fecha y hora">
          <span className="inline-block first-letter:uppercase">{formatearFechaLarga(c.fecha)}</span> a las{' '}
          {c.hora}
        </Dato>
        <Dato termino="Módulo">{c.modulo}</Dato>
        <Dato termino="Trámite">{c.tramite}</Dato>
        <Dato termino="Origen">{TEXTO_ORIGEN[c.origen]}</Dato>
        <Dato termino="Persona">{c.nombreCompleto}</Dato>
        <Dato termino="CURP">
          <span className="font-mono">{c.curp ?? 'Sin CURP'}</span>
        </Dato>
        <Dato termino="Correo">{c.correo ?? 'Sin correo'}</Dato>
        <Dato termino="Celular">{c.celular ?? 'Sin celular'}</Dato>
        <Dato termino="Registrada">
          {formatearInstante(c.creadaEn)}
          {c.creadaPor ? ` por ${c.creadaPor}` : ''}
        </Dato>
        {c.folioAnterior && <Dato termino="Folio anterior">{c.folioAnterior}</Dato>}
        {c.motivoCambio && <Dato termino="Motivo del último cambio">{c.motivoCambio}</Dato>}
      </dl>

      <div className="flex flex-wrap gap-2">
        <a
          href={`/api/admin/citas/${c.citaId}/comprobante.pdf`}
          className={estiloBoton({ variante: 'secundario' })}
        >
          Descargar comprobante (PDF)
        </a>
        {agendada && c.puedeModificar && (
          <>
            <Boton variante="secundario" onClick={() => setAccion('reprogramar')}>
              Reprogramar
            </Boton>
            <Boton variante="secundario" onClick={() => setAccion('cancelar')}>
              Cancelar cita
            </Boton>
          </>
        )}
        {agendada && (
          <Boton variante="secundario" onClick={() => setAccion('reenviar')}>
            Reenviar confirmación
          </Boton>
        )}
        {corregible && (
          <Boton variante="secundario" onClick={() => setAccion('corregir')}>
            Corregir asistencia
          </Boton>
        )}
      </div>

      {c.relacionadas.length > 0 && (
        <div className="flex flex-col gap-2">
          <h2 className="text-lg font-semibold">Citas relacionadas</h2>
          <ul className="flex flex-col gap-1">
            {c.relacionadas.map((r) => (
              <li key={r.citaId} className="flex flex-wrap items-center gap-2">
                <Link to={`/admin/citas/${r.citaId}`} className="text-guinda-700 underline">
                  {r.folio}
                </Link>
                <span>
                  {formatearFechaCorta(r.fecha)} · {r.hora}
                </span>
                <EtiquetaEstado estado={r.estado} />
              </li>
            ))}
          </ul>
        </div>
      )}

      <div className="flex flex-col gap-2">
        <h2 className="text-lg font-semibold">Avisos por correo</h2>
        {c.notificaciones.length === 0 ? (
          <p className="text-slate-700">Sin avisos.</p>
        ) : (
          <TablaDesplazable etiqueta="Avisos por correo de la cita">
            <thead>
              <tr>
                <th scope="col" className={celdaEncabezado}>
                  Aviso
                </th>
                <th scope="col" className={celdaEncabezado}>
                  Estado
                </th>
                <th scope="col" className={celdaEncabezado}>
                  Destinatario
                </th>
                <th scope="col" className={celdaEncabezado}>
                  Creado
                </th>
                <th scope="col" className={celdaEncabezado}>
                  Enviado
                </th>
              </tr>
            </thead>
            <tbody>
              {c.notificaciones.map((n) => (
                <tr key={n.notificacionId}>
                  <td className={celda}>{TEXTO_TIPO_NOTIFICACION[n.tipo]}</td>
                  <td className={celda}>
                    {TEXTO_ESTADO_NOTIFICACION[n.estado]}
                    {n.intentos > 1 && <div className="text-xs text-slate-600">{n.intentos} intentos</div>}
                    {n.ultimoError && <div className="text-xs text-red-800">{n.ultimoError}</div>}
                  </td>
                  <td className={celda}>{n.destinatario}</td>
                  <td className={celda}>{formatearInstante(n.creadaEn)}</td>
                  <td className={celda}>{n.enviadaEn ? formatearInstante(n.enviadaEn) : '—'}</td>
                </tr>
              ))}
            </tbody>
          </TablaDesplazable>
        )}
      </div>

      <div className="flex flex-col gap-2">
        <h2 className="text-lg font-semibold">Movimientos</h2>
        {c.movimientos.length === 0 ? (
          <p className="text-slate-700">Sin movimientos.</p>
        ) : (
          <ol className="flex flex-col gap-1">
            {c.movimientos.map((m, i) => (
              <li key={`${m.fecha}-${i}`}>
                <span className="font-medium">{formatearInstante(m.fecha)}</span> · {m.accion}
                {m.usuario ? ` · ${m.usuario}` : ''}
              </li>
            ))}
          </ol>
        )}
      </div>

      {accion === 'cancelar' && (
        <DialogoConfirmar
          titulo={`¿Cancelar la cita ${c.folio}?`}
          descripcion="Se libera el lugar y se avisa a la persona por correo."
          textoConfirmar="Sí, cancelar la cita"
          motivo={{ etiqueta: 'Motivo', obligatorio: true }}
          ocupado={cancelar.isPending}
          error={cancelar.error}
          alConfirmar={(motivo) => cancelar.mutate(motivo)}
          alCerrar={cerrar}
        />
      )}
      {accion === 'reenviar' && (
        <DialogoConfirmar
          titulo="¿Reenviar la confirmación?"
          descripcion={c.correo ? `Se enviará a ${c.correo}.` : 'La cita no tiene correo registrado.'}
          textoConfirmar="Reenviar"
          ocupado={reenviar.isPending}
          error={reenviar.error}
          alConfirmar={() => reenviar.mutate()}
          alCerrar={cerrar}
        />
      )}
      {accion === 'corregir' && (
        <DialogoConfirmar
          titulo="Corregir asistencia"
          descripcion={`La cita quedará como «${c.estado === 'Atendida' ? TEXTO_ESTADO.NoAsistio : TEXTO_ESTADO.Atendida}».`}
          textoConfirmar="Corregir"
          motivo={{ etiqueta: 'Motivo de la corrección', obligatorio: true }}
          ocupado={corregir.isPending}
          error={corregir.error}
          alConfirmar={(motivo) => corregir.mutate({ asistio: c.estado !== 'Atendida', motivo })}
          alCerrar={cerrar}
        />
      )}
      {accion === 'reprogramar' && (
        <DialogoReprogramar
          ofertaId={c.ofertaId}
          folio={c.folio}
          ocupado={reprogramar.isPending}
          error={reprogramar.error}
          alConfirmar={(cupoId, motivo) => reprogramar.mutate({ cupoId, motivo })}
          alCerrar={cerrar}
        />
      )}
    </section>
  );
}

function Dato({ termino, children }: { termino: string; children: ReactNode }) {
  return (
    <div>
      <dt className="text-sm text-slate-600">{termino}</dt>
      <dd className="font-medium text-slate-900">{children}</dd>
    </div>
  );
}
