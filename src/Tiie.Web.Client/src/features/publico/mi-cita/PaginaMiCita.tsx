import { useState, type ReactNode } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router';
import { ErrorApi, mensajeDeError } from '@/api/errores';
import type { components } from '@/api/esquema';
import { ComoLlegar } from '@/components/ComoLlegar';
import { Requisitos } from '@/components/Requisitos';
import { Alerta, RegionAnuncios } from '@/components/ui/Alerta';
import { Boton, estiloBoton } from '@/components/ui/Boton';
import { Contenedor } from '@/components/ui/Contenedor';
import { Cargando, EncabezadoPaso } from '@/components/ui/Encabezado';
import { Icono, type NombreIcono } from '@/components/ui/Icono';
import { Insignia, type TonoInsignia } from '@/components/ui/Insignia';
import { diasEntre, formatearFechaLarga, hoyLocal } from '@/lib/fechas';
import { EstadoVacio } from '../comun/EstadoVacio';
import { TEXTO_ESTADO_CITA, formatearCosto } from '../comun/formatos';
import { AgregarCalendario, CopiarFolio } from './AccionesBoleto';
import { DialogoCancelar } from './DialogoCancelar';
import { ReprogramarCita } from './ReprogramarCita';
import { useMiCita, useSalirMiCita } from './useMiCita';

type DetalleMiCita = components['schemas']['DetalleMiCita'];
type EstadoCita = components['schemas']['EstadoCita'];

/** Mi cita: consultar, descargar el comprobante, cancelar y reprogramar (RF-05). */
export function PaginaMiCita() {
  const [parametros] = useSearchParams();
  const enlaceInvalido = parametros.get('error') === 'enlace-invalido';
  const cita = useMiCita(!enlaceInvalido);

  if (enlaceInvalido) return <EnlaceInvalido />;
  // Sin cookie: la persona llegó desde el menú, no desde un enlace vencido. Se le explica cómo entrar.
  if (cita.error instanceof ErrorApi && cita.error.status === 401) return <SinSesion />;
  if (cita.isPending)
    return (
      <Contenedor className="py-12">
        <Cargando texto="Cargando tu cita…" />
      </Contenedor>
    );
  if (cita.isError) {
    return (
      <Contenedor className="py-12">
        <Alerta tono="error" traza={cita.error instanceof ErrorApi ? cita.error.traceId : null}>
          {mensajeDeError(cita.error)}
        </Alerta>
      </Contenedor>
    );
  }
  return <DetalleCita detalle={cita.data} nueva={parametros.get('nueva') === '1'} />;
}

function EnlaceInvalido() {
  return (
    <EstadoVacio
      icono="alerta"
      tono="aviso"
      titulo="Este enlace ya no es válido"
      acciones={
        <Link to="/recuperar" className={estiloBoton({ tamano: 'grande' })}>
          Recuperar mi enlace
        </Link>
      }
    >
      <p>
        El enlace venció o no es correcto. Si tienes una cita agendada, podemos enviarte el enlace a tu
        correo.
      </p>
    </EstadoVacio>
  );
}

function SinSesion() {
  return (
    <EstadoVacio
      icono="correo"
      titulo="Consulta tu cita"
      acciones={
        <>
          <Link to="/recuperar" className={estiloBoton({ tamano: 'grande' })}>
            Enviarme el enlace de nuevo
          </Link>
          <Link to="/agendar" className={estiloBoton({ variante: 'secundario', tamano: 'grande' })}>
            Agendar una cita
          </Link>
        </>
      }
    >
      <p>
        Para ver, cambiar o cancelar tu cita, abre el enlace que te enviamos por correo cuando la agendaste.
        Por tu privacidad, no pedimos tu CURP para mostrarla.
      </p>
    </EstadoVacio>
  );
}

const TONO_ESTADO: Record<EstadoCita, TonoInsignia> = {
  Agendada: 'exito',
  Atendida: 'neutro',
  NoAsistio: 'aviso',
  Cancelada: 'error',
  Reprogramada: 'neutro',
};

/** Explicación de una cita que ya no está agendada, con el siguiente paso. */
const NOTA_ESTADO: Partial<Record<EstadoCita, { texto: string; agendarOtra: boolean }>> = {
  Cancelada: { texto: 'Esta cita está cancelada.', agendarOtra: true },
  Reprogramada: {
    texto: 'Esta cita se cambió a otra fecha. Abre el enlace del correo más reciente para ver la nueva.',
    agendarOtra: false,
  },
  Atendida: { texto: 'Tu trámite ya fue atendido en esta cita.', agendarOtra: false },
  NoAsistio: { texto: 'No se registró tu asistencia a esta cita.', agendarOtra: true },
};

function cuentaRegresiva(fecha: string): string | null {
  const dias = diasEntre(hoyLocal(), fecha);
  if (dias < 0) return null;
  if (dias === 0) return 'Es hoy';
  if (dias === 1) return 'Es mañana';
  return `Faltan ${dias} días`;
}

type Accion = 'ninguna' | 'cancelar' | 'reprogramar';

function DetalleCita({ detalle, nueva }: { detalle: DetalleMiCita; nueva: boolean }) {
  const [accion, setAccion] = useState<Accion>('ninguna');
  const [aviso, setAviso] = useState<string | null>(null);
  const salir = useSalirMiCita();
  const navegar = useNavigate();
  const agendada = detalle.estado === 'Agendada';
  const nota = NOTA_ESTADO[detalle.estado];
  const faltan = agendada ? cuentaRegresiva(detalle.fecha) : null;

  return (
    <Contenedor className="flex flex-col gap-8 py-8 sm:py-12">
      <RegionAnuncios>
        {nueva && agendada && !aviso && (
          <div className="flex items-start gap-4 rounded-2xl bg-emerald-50 p-5 ring-1 ring-emerald-200">
            <span className="grid size-12 shrink-0 place-items-center rounded-full bg-emerald-600 text-white shadow-sm">
              <Icono nombre="check" className="size-7" />
            </span>
            <div>
              <p className="text-xl font-bold text-emerald-950">Tu cita quedó agendada</p>
              <p className="mt-1 text-emerald-900">
                Te enviamos un correo a {detalle.solicitante.correo} con el comprobante y el enlace para
                consultarla.
              </p>
            </div>
          </div>
        )}
        {aviso && <Alerta tono="exito">{aviso}</Alerta>}
      </RegionAnuncios>

      <header className="flex flex-wrap items-center justify-between gap-3">
        <EncabezadoPaso className="text-3xl tracking-tight sm:text-4xl">Tu cita</EncabezadoPaso>
        <Insignia tono={TONO_ESTADO[detalle.estado]}>{TEXTO_ESTADO_CITA[detalle.estado]}</Insignia>
      </header>

      {nota && !aviso && (
        <Alerta tono={detalle.estado === 'NoAsistio' ? 'aviso' : 'info'}>
          <p>{nota.texto}</p>
          {nota.agendarOtra && (
            <Link
              to="/agendar"
              className="mt-1 inline-flex min-h-11 items-center font-semibold text-guinda-700 underline underline-offset-4"
            >
              Agendar una cita nueva
            </Link>
          )}
        </Alerta>
      )}

      <article
        aria-label="Comprobante de la cita"
        className="relative overflow-hidden rounded-3xl border border-slate-200 bg-white shadow-elevada"
      >
        <div
          className={`relative px-6 pt-6 pb-8 text-white sm:px-8 ${agendada ? 'bg-guinda-800' : 'bg-slate-700'}`}
        >
          <div
            aria-hidden="true"
            className="pointer-events-none absolute -top-16 -right-16 size-48 rounded-full bg-white/5"
          />
          <div className="relative flex flex-wrap items-center justify-between gap-2">
            <div className="flex flex-wrap items-center gap-x-2">
              <span className="text-sm text-white/80">Folio</span>
              <span className="font-mono text-lg font-bold tracking-wide">{detalle.folio}</span>
              <CopiarFolio folio={detalle.folio} />
            </div>
            {faltan && (
              <span className="rounded-full bg-white/15 px-3 py-1 text-sm font-semibold ring-1 ring-white/25">
                {faltan}
              </span>
            )}
          </div>
          {detalle.folioAnterior && (
            <p className="relative text-sm text-white/80">Folio anterior: {detalle.folioAnterior}</p>
          )}
          <div className="relative mt-6 flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
            <p className="flex items-start gap-3">
              <Icono nombre="calendario" className="mt-1 size-6 text-dorado-200" />
              <span className="text-2xl leading-tight font-bold first-letter:uppercase sm:text-3xl">
                {formatearFechaLarga(detalle.fecha)}
              </span>
            </p>
            <p className="flex items-center gap-3">
              <Icono nombre="reloj" className="size-6 text-dorado-200" />
              <span className="text-3xl font-extrabold tabular-nums">{detalle.hora}</span>
            </p>
          </div>
        </div>

        <Perforacion />

        <div className="flex flex-col gap-6 px-6 pt-2 pb-6 sm:px-8 sm:pb-8">
          <div className="grid gap-6 sm:grid-cols-2">
            <Dato icono="sede" etiqueta="Sede">
              <span className="block font-semibold text-slate-950">{detalle.modulo.nombre}</span>
              <span className="block text-slate-700">
                {detalle.modulo.direccion}, {detalle.modulo.municipio}
              </span>
              {detalle.modulo.horarioAtencion && (
                <span className="mt-1 block text-sm text-slate-600">{detalle.modulo.horarioAtencion}</span>
              )}
              <ComoLlegar
                latitud={detalle.modulo.latitud}
                longitud={detalle.modulo.longitud}
                sede={detalle.modulo.nombre}
              />
            </Dato>
            <Dato icono="tarjeta" etiqueta="Trámite">
              <span className="block font-semibold text-slate-950">{detalle.tramite.nombre}</span>
              <span className="block text-slate-700">{formatearCosto(detalle.tramite.costo)}</span>
            </Dato>
          </div>
          <div className="flex flex-col gap-4 border-t border-slate-100 pt-6 sm:flex-row sm:items-end sm:justify-between">
            <a
              href="/api/mi-cita/comprobante.pdf"
              download
              className={estiloBoton({ variante: agendada ? 'primario' : 'secundario' })}
            >
              <Icono nombre="descargar" />
              Descargar comprobante (PDF)
            </a>
            {agendada && (
              <AgregarCalendario
                evento={{
                  folio: detalle.folio,
                  fecha: detalle.fecha,
                  hora: detalle.hora,
                  tramite: detalle.tramite.nombre,
                  sede: detalle.modulo.nombre,
                  direccion: detalle.modulo.direccion,
                  municipio: detalle.modulo.municipio,
                }}
              />
            )}
          </div>
        </div>
      </article>

      <Seccion id="titulo-requisitos" icono="documento" titulo="Lleva estos requisitos">
        <Requisitos requisitos={detalle.tramite.requisitos} />
        {detalle.tramite.notas && (
          <p className="mt-4 rounded-lg bg-slate-50 px-4 py-3 whitespace-pre-line text-slate-700">
            {detalle.tramite.notas}
          </p>
        )}
        {detalle.tramite.notasSede && (
          <p className="mt-3 whitespace-pre-line text-slate-700">
            <span className="font-semibold">Indicaciones de la sede:</span> {detalle.tramite.notasSede}
          </p>
        )}
      </Seccion>

      <Seccion id="titulo-solicitante" icono="usuario" titulo="Persona que hará el trámite">
        <p className="font-semibold text-slate-950">{detalle.solicitante.nombreCompleto}</p>
        <p className="mt-1 font-mono text-sm text-slate-600">CURP {detalle.solicitante.curp}</p>
        <p className="text-sm text-slate-600">{detalle.solicitante.correo}</p>
      </Seccion>

      {agendada && (
        <Seccion id="titulo-cambios" icono="cambiar" titulo="¿Cambio de planes?">
          {detalle.puedeModificar ? (
            <>
              <p className="text-slate-700">
                Puedes cancelar o cambiar tu cita hasta el {formatearFechaLarga(detalle.modificableHasta)}. Si
                no podrás ir, cancélala para que alguien más use tu lugar.
              </p>
              <div className="mt-4 flex flex-col gap-2 sm:flex-row sm:gap-3">
                <Boton variante="secundario" bloque onClick={() => setAccion('reprogramar')}>
                  <Icono nombre="calendario" />
                  Cambiar fecha u hora
                </Boton>
                <Boton variante="peligro" bloque onClick={() => setAccion('cancelar')}>
                  <Icono nombre="cerrar" />
                  Cancelar cita
                </Boton>
              </div>
            </>
          ) : (
            <Alerta tono="aviso">
              El plazo para cancelar o cambiar tu cita en línea terminó el{' '}
              {formatearFechaLarga(detalle.modificableHasta)}. Si necesitas ayuda, acude al módulo.
            </Alerta>
          )}
        </Seccion>
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

      <div className="flex flex-col items-start gap-1 border-t border-slate-200 pt-6 text-sm text-slate-600">
        <Boton
          variante="enlace"
          ocupado={salir.isPending}
          textoOcupado="Cerrando…"
          onClick={() => salir.mutate(undefined, { onSettled: () => void navegar('/') })}
        >
          <Icono nombre="salir" className="size-4" />
          Cerrar mi cita en este equipo
        </Boton>
        <p>Hazlo si usas un equipo compartido. Podrás volver a abrirla con el enlace de tu correo.</p>
      </div>
    </Contenedor>
  );
}

/** Línea punteada con muescas a los lados: el comprobante se lee como un boleto. */
function Perforacion() {
  return (
    <div aria-hidden="true" className="relative h-6">
      <span className="absolute top-1/2 -left-3 size-6 -translate-y-1/2 rounded-full border border-slate-200 bg-slate-50" />
      <span className="absolute top-1/2 -right-3 size-6 -translate-y-1/2 rounded-full border border-slate-200 bg-slate-50" />
      <span className="absolute inset-x-6 top-1/2 border-t-2 border-dashed border-slate-200" />
    </div>
  );
}

function Dato({ icono, etiqueta, children }: { icono: NombreIcono; etiqueta: string; children: ReactNode }) {
  return (
    <div className="flex gap-3">
      <span className="grid size-10 shrink-0 place-items-center rounded-xl bg-guinda-50 text-guinda-700">
        <Icono nombre={icono} />
      </span>
      <div className="min-w-0">
        <p className="text-xs font-semibold tracking-wide text-slate-500 uppercase">{etiqueta}</p>
        <div className="mt-0.5">{children}</div>
      </div>
    </div>
  );
}

function Seccion({
  id,
  icono,
  titulo,
  children,
}: {
  id: string;
  icono: NombreIcono;
  titulo: string;
  children: ReactNode;
}) {
  return (
    <section aria-labelledby={id} className="rounded-2xl border border-slate-200 bg-white p-6 shadow-tarjeta">
      <h2 id={id} className="mb-4 flex items-center gap-2 text-lg font-bold text-slate-950">
        <Icono nombre={icono} className="text-guinda-700" />
        {titulo}
      </h2>
      {children}
    </section>
  );
}
