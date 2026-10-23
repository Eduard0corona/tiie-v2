import { useState, type ReactNode } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router';
import { ErrorApi, mensajeDeError } from '@/api/errores';
import type { components } from '@/api/esquema';
import { ComoLlegar } from '@/components/ComoLlegar';
import { Requisitos } from '@/components/Requisitos';
import { Alerta, RegionAnuncios } from '@/components/ui/Alerta';
import { Boton } from '@/components/ui/Boton';
import { Cargando, EncabezadoPaso } from '@/components/ui/Encabezado';
import { formatearFechaLarga } from '@/lib/fechas';
import { TEXTO_ESTADO_CITA, formatearCosto } from '../comun/formatos';
import { DialogoCancelar } from './DialogoCancelar';
import { ReprogramarCita } from './ReprogramarCita';
import { useMiCita, useSalirMiCita } from './useMiCita';

type DetalleMiCita = components['schemas']['DetalleMiCita'];

/** Mi cita: consultar, descargar el comprobante, cancelar y reprogramar (RF-05). */
export function PaginaMiCita() {
  const [parametros] = useSearchParams();
  const enlaceInvalido = parametros.get('error') === 'enlace-invalido';
  const cita = useMiCita(!enlaceInvalido);

  if (enlaceInvalido || (cita.error instanceof ErrorApi && cita.error.status === 401))
    return <EnlaceInvalido />;
  if (cita.isPending) return <Cargando texto="Cargando tu cita…" />;
  if (cita.isError) {
    return (
      <Alerta tono="error" traza={cita.error instanceof ErrorApi ? cita.error.traceId : null}>
        {mensajeDeError(cita.error)}
      </Alerta>
    );
  }
  return <DetalleCita detalle={cita.data} nueva={parametros.get('nueva') === '1'} />;
}

function EnlaceInvalido() {
  return (
    <section className="flex flex-col gap-4">
      <EncabezadoPaso>Este enlace ya no es válido</EncabezadoPaso>
      <p className="text-slate-700">
        El enlace venció o no es correcto. Si tienes una cita agendada, podemos enviarte el enlace a tu
        correo.
      </p>
      <p>
        <Link
          to="/recuperar"
          className="inline-flex min-h-11 items-center rounded-md bg-guinda-700 px-4 py-2 font-medium text-white hover:bg-guinda-800"
        >
          Recuperar mi enlace
        </Link>
      </p>
    </section>
  );
}

type Accion = 'ninguna' | 'cancelar' | 'reprogramar';

function DetalleCita({ detalle, nueva }: { detalle: DetalleMiCita; nueva: boolean }) {
  const [accion, setAccion] = useState<Accion>('ninguna');
  const [aviso, setAviso] = useState<string | null>(null);
  const salir = useSalirMiCita();
  const navegar = useNavigate();
  const agendada = detalle.estado === 'Agendada';

  return (
    <article className="flex flex-col gap-6">
      <EncabezadoPaso>Tu cita</EncabezadoPaso>
      <RegionAnuncios>
        {nueva && agendada && !aviso && (
          <Alerta tono="exito" titulo="Tu cita quedó agendada">
            Te enviamos un correo con el comprobante y el enlace para consultarla.
          </Alerta>
        )}
        {aviso && <Alerta tono="exito">{aviso}</Alerta>}
      </RegionAnuncios>

      <dl className="grid grid-cols-1 gap-x-4 gap-y-3 rounded-lg border border-slate-200 bg-white p-4 sm:grid-cols-[auto_1fr]">
        <Dato etiqueta="Estado">
          <span className="font-semibold">{TEXTO_ESTADO_CITA[detalle.estado]}</span>
        </Dato>
        <Dato etiqueta="Folio">
          <span className="font-semibold">{detalle.folio}</span>
          {detalle.folioAnterior && (
            <span className="block text-sm text-slate-600">Folio anterior: {detalle.folioAnterior}</span>
          )}
        </Dato>
        <Dato etiqueta="Fecha">{formatearFechaLarga(detalle.fecha)}</Dato>
        <Dato etiqueta="Hora">{detalle.hora}</Dato>
        <Dato etiqueta="Sede">
          <span className="block">{detalle.modulo.nombre}</span>
          <span className="block text-slate-700">
            {detalle.modulo.direccion}, {detalle.modulo.municipio}
          </span>
          {detalle.modulo.horarioAtencion && (
            <span className="block text-sm text-slate-600">{detalle.modulo.horarioAtencion}</span>
          )}
          <ComoLlegar
            latitud={detalle.modulo.latitud}
            longitud={detalle.modulo.longitud}
            sede={detalle.modulo.nombre}
          />
        </Dato>
        <Dato etiqueta="Trámite">
          {detalle.tramite.nombre} · {formatearCosto(detalle.tramite.costo)}
        </Dato>
        <Dato etiqueta="Solicitante">
          <span className="block">{detalle.solicitante.nombreCompleto}</span>
          <span className="block text-sm text-slate-600">CURP {detalle.solicitante.curp}</span>
          <span className="block text-sm text-slate-600">{detalle.solicitante.correo}</span>
        </Dato>
      </dl>

      <section aria-labelledby="titulo-requisitos" className="flex flex-col gap-2">
        <h2 id="titulo-requisitos" className="text-lg font-semibold">
          Lleva estos requisitos
        </h2>
        <Requisitos requisitos={detalle.tramite.requisitos} />
        {detalle.tramite.notas && (
          <p className="whitespace-pre-line text-slate-700">{detalle.tramite.notas}</p>
        )}
        {detalle.tramite.notasSede && (
          <p className="whitespace-pre-line text-slate-700">
            <span className="font-medium">Indicaciones de la sede:</span> {detalle.tramite.notasSede}
          </p>
        )}
      </section>

      <div className="flex flex-wrap gap-3">
        <a
          href="/api/mi-cita/comprobante.pdf"
          download
          className="inline-flex min-h-11 items-center rounded-md border border-slate-300 bg-white px-4 py-2 font-medium hover:bg-slate-100"
        >
          Descargar comprobante (PDF)
        </a>
        {agendada && detalle.puedeModificar && (
          <>
            <Boton variante="secundario" onClick={() => setAccion('reprogramar')}>
              Cambiar fecha u hora
            </Boton>
            <Boton variante="secundario" onClick={() => setAccion('cancelar')}>
              Cancelar cita
            </Boton>
          </>
        )}
      </div>

      {agendada && !detalle.puedeModificar && (
        <Alerta>
          El plazo para cancelar o cambiar tu cita en línea terminó el{' '}
          {formatearFechaLarga(detalle.modificableHasta)}. Si necesitas ayuda, acude al módulo.
        </Alerta>
      )}
      {agendada && detalle.puedeModificar && (
        <p className="text-sm text-slate-600">
          Puedes cancelar o cambiar tu cita hasta el {formatearFechaLarga(detalle.modificableHasta)}.
        </p>
      )}

      {accion === 'reprogramar' && (
        <ReprogramarCita
          ofertaId={detalle.ofertaId}
          alTerminar={() => {
            setAccion('ninguna');
            setAviso('Tu cita cambió. Te enviamos un correo con el comprobante nuevo.');
          }}
          alCancelar={() => setAccion('ninguna')}
        />
      )}
      {accion === 'cancelar' && (
        <DialogoCancelar
          alCerrar={() => setAccion('ninguna')}
          alCancelar={() => {
            setAccion('ninguna');
            setAviso('Tu cita fue cancelada. Te enviamos un correo de confirmación.');
          }}
        />
      )}

      <div>
        <Boton
          variante="enlace"
          className="px-0"
          ocupado={salir.isPending}
          textoOcupado="Cerrando…"
          onClick={() => salir.mutate(undefined, { onSettled: () => void navegar('/') })}
        >
          Cerrar mi cita en este equipo
        </Boton>
      </div>
    </article>
  );
}

function Dato({ etiqueta, children }: { etiqueta: string; children: ReactNode }) {
  return (
    <div className="contents">
      <dt className="text-slate-600">{etiqueta}</dt>
      <dd>{children}</dd>
    </div>
  );
}
