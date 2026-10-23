import { Link } from 'react-router';
import { mensajeDeError } from '@/api/errores';
import type { components } from '@/api/esquema';
import { ComoLlegar } from '@/components/ComoLlegar';
import { Requisitos } from '@/components/Requisitos';
import { Alerta } from '@/components/ui/Alerta';
import { Cargando } from '@/components/ui/Encabezado';
import { formatearCosto } from '../comun/formatos';
import { useModulos, useTiposTramite } from '../comun/usePublico';

type ModuloPublico = components['schemas']['ModuloPublico'];

const enlaceBoton =
  'inline-flex min-h-11 items-center justify-center rounded-md bg-guinda-700 px-5 py-2 font-medium text-white hover:bg-guinda-800';

/** Trámites, requisitos por nivel escolar y sedes (RF-01). */
export function PaginaInicio() {
  return (
    <div className="flex flex-col gap-10">
      <section aria-labelledby="titulo-inicio" className="flex flex-col gap-4">
        <h1 id="titulo-inicio" className="text-2xl font-bold sm:text-3xl">
          Agenda tu cita para la Tarjeta Inteligente de Identidad Estudiantil
        </h1>
        <p className="text-lg text-slate-700">
          Elige la sede, el día y la hora desde tu celular. Recibirás un correo con tu comprobante y un enlace
          para consultar, cambiar o cancelar tu cita.
        </p>
        <div className="flex flex-wrap items-center gap-4">
          <Link to="/agendar" className={enlaceBoton}>
            Agendar cita
          </Link>
          <Link
            to="/recuperar"
            className="inline-flex min-h-11 items-center text-guinda-700 underline underline-offset-4"
          >
            ¿Ya tienes cita? Recupera tu enlace
          </Link>
        </div>
      </section>
      <SeccionTramites />
      <SeccionSedes />
    </div>
  );
}

function SeccionTramites() {
  const tipos = useTiposTramite();
  return (
    <section aria-labelledby="titulo-tramites" className="flex flex-col gap-4">
      <h2 id="titulo-tramites" className="text-xl font-bold">
        Trámites y requisitos
      </h2>
      {tipos.isPending && <Cargando texto="Cargando trámites…" />}
      {tipos.isError && <Alerta tono="error">{mensajeDeError(tipos.error)}</Alerta>}
      {tipos.data?.length === 0 && <p className="text-slate-700">Por ahora no hay trámites disponibles.</p>}
      {tipos.data?.map((tipo) => (
        <article
          key={tipo.tipoTramiteId}
          className="flex flex-col gap-3 rounded-lg border border-slate-200 bg-white p-4"
        >
          <header className="flex flex-wrap items-baseline justify-between gap-2">
            <h3 className="text-lg font-semibold">{tipo.nombre}</h3>
            <p className="text-slate-700">{formatearCosto(tipo.costo)}</p>
          </header>
          {tipo.descripcion && <p className="text-slate-700">{tipo.descripcion}</p>}
          <Requisitos requisitos={tipo.requisitos} />
        </article>
      ))}
    </section>
  );
}

function SeccionSedes() {
  const modulos = useModulos();
  const porMunicipio = new Map<string, ModuloPublico[]>();
  for (const modulo of modulos.data ?? []) {
    porMunicipio.set(modulo.municipio, [...(porMunicipio.get(modulo.municipio) ?? []), modulo]);
  }

  return (
    <section aria-labelledby="titulo-sedes" className="flex flex-col gap-4">
      <h2 id="titulo-sedes" className="text-xl font-bold">
        Sedes
      </h2>
      {modulos.isPending && <Cargando texto="Cargando sedes…" />}
      {modulos.isError && <Alerta tono="error">{mensajeDeError(modulos.error)}</Alerta>}
      {[...porMunicipio.entries()].map(([municipio, sedes]) => (
        <div key={municipio} className="flex flex-col gap-2">
          <h3 className="font-semibold text-slate-800">{municipio}</h3>
          <ul className="flex flex-col gap-2">
            {sedes.map((sede) => (
              <li key={sede.moduloId} className="rounded-lg border border-slate-200 bg-white p-4">
                <p className="font-medium">{sede.nombre}</p>
                <p className="text-slate-700">{sede.direccion}</p>
                {sede.horarioAtencion && <p className="text-sm text-slate-600">{sede.horarioAtencion}</p>}
                <ComoLlegar latitud={sede.latitud} longitud={sede.longitud} sede={sede.nombre} />
              </li>
            ))}
          </ul>
        </div>
      ))}
    </section>
  );
}
