import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { Link } from 'react-router';
import { api, datos } from '@/api/cliente';
import type { components } from '@/api/esquema';
import { ErrorApi, copiarErroresAlFormulario, mensajeDeError } from '@/api/errores';
import { SelectorHora } from '@/components/SelectorHora';
import { Alerta, RegionAnuncios } from '@/components/ui/Alerta';
import { Boton, estiloBoton } from '@/components/ui/Boton';
import { Campo, Casilla } from '@/components/ui/Campo';
import { Cargando } from '@/components/ui/Encabezado';
import { Selector } from '@/components/ui/Selector';
import { EncabezadoPagina, ErrorPanel } from '@/features/admin/comun/Componentes';
import { useModulosAdmin, useOfertasAdmin } from '@/features/admin/comun/useCatalogosAdmin';
import { normalizarCurp } from '@/lib/curp';
import { esFechaIso, formatearFechaLarga, hoyLocal } from '@/lib/fechas';
import { CAMPOS_VENTANILLA, esquemaVentanilla, type ValoresVentanilla } from './esquemas';
import { useCuposDelDia } from './useCuposAdmin';

type CitaRegistrada = components['schemas']['CitaRegistrada'];

interface Aviso {
  texto: string;
  traza?: string | null;
  curp?: string;
}

const VALORES_INICIALES: ValoresVentanilla = {
  curp: '',
  nombre: '',
  apellidoPaterno: '',
  apellidoMaterno: '',
  celular: '',
  correo: '',
  avisoLeido: false,
};

/**
 * Registro de una cita en ventanilla (RF-08): módulo, oferta, fecha, hora y datos. Admite hoy con horas por
 * venir. La persona no acepta en línea: el personal confirma que le leyó el aviso de privacidad simplificado.
 */
export function PaginaVentanilla() {
  const hoy = hoyLocal();
  const [moduloId, setModuloId] = useState<number | null>(null);
  const [ofertaId, setOfertaId] = useState<number | null>(null);
  const [fecha, setFecha] = useState(hoy);
  const [cupoId, setCupoId] = useState<number | null>(null);
  // Una clave por hora elegida: un reintento por red lenta reutiliza la misma y no duplica la cita.
  const [clave, setClave] = useState(() => crypto.randomUUID());
  const [aviso, setAviso] = useState<Aviso | null>(null);
  const [registrada, setRegistrada] = useState<CitaRegistrada | null>(null);
  const cliente = useQueryClient();

  const modulos = useModulosAdmin(true);
  // Con un solo módulo disponible, queda elegido.
  const moduloElegido = moduloId ?? (modulos.data?.length === 1 ? (modulos.data[0]?.moduloId ?? null) : null);
  const ofertas = useOfertasAdmin(moduloElegido, moduloElegido !== null);
  const ofertasActivas = ofertas.data?.filter((o) => o.activo) ?? [];
  const fechaValida = esFechaIso(fecha) && fecha >= hoy ? fecha : null;
  const cupos = useCuposDelDia(ofertaId, fechaValida);

  const formulario = useForm<ValoresVentanilla>({
    resolver: zodResolver(esquemaVentanilla),
    defaultValues: VALORES_INICIALES,
  });
  const { register, handleSubmit, formState, setError, reset } = formulario;

  const elegirCupo = (id: number | null) => {
    setCupoId(id);
    setClave(crypto.randomUUID());
  };

  const crear = useMutation({
    mutationFn: async (valores: ValoresVentanilla) =>
      datos(
        await api.POST('/api/admin/citas', {
          params: { header: { 'Idempotency-Key': clave } },
          body: {
            cupoId: cupoId ?? 0,
            curp: normalizarCurp(valores.curp),
            nombre: valores.nombre.trim(),
            apellidoPaterno: valores.apellidoPaterno.trim(),
            apellidoMaterno: valores.apellidoMaterno.trim() || null,
            celular: valores.celular.trim() || null,
            correo: valores.correo.trim() || null,
            avisoLeido: valores.avisoLeido,
          },
        }),
      ),
    onSuccess: (cita) => {
      setRegistrada(cita);
      setAviso(null);
      void cliente.invalidateQueries({ queryKey: ['admin', 'cupos'] });
      void cliente.invalidateQueries({ queryKey: ['admin', 'agenda'] });
      void cliente.invalidateQueries({ queryKey: ['admin', 'citas'] });
    },
    onError: (error, valores) => {
      if (!(error instanceof ErrorApi)) {
        setAviso({ texto: mensajeDeError(error) });
        return;
      }
      switch (error.codigo) {
        case 'SinCupo':
        case 'CupoCerrado':
        case 'FueraDeVentana':
        case 'OfertaInactiva':
          elegirCupo(null);
          void cliente.invalidateQueries({ queryKey: ['admin', 'cupos'] });
          setAviso({ texto: `${error.mensaje} Elige otra hora.` });
          return;
        case 'YaTieneCita':
          setAviso({ texto: error.mensaje, curp: normalizarCurp(valores.curp) });
          return;
        case 'Validacion':
          setAviso(
            copiarErroresAlFormulario(error, setError, CAMPOS_VENTANILLA) ? null : { texto: error.mensaje },
          );
          return;
        default:
          setAviso({ texto: error.mensaje, traza: error.traceId });
      }
    },
  });

  const registrarOtra = () => {
    setRegistrada(null);
    elegirCupo(null);
    reset(VALORES_INICIALES);
    crear.reset();
  };

  if (registrada) {
    return (
      <section className="flex max-w-xl flex-col gap-4">
        <EncabezadoPagina titulo="Cita registrada" />
        <Alerta tono="exito" titulo={`Folio ${registrada.folio}`}>
          Entrega el comprobante a la persona. Si dio correo, también se lo enviamos.
        </Alerta>
        <div className="flex flex-wrap gap-2">
          <Link to={`/admin/citas/${registrada.citaId}`} className={estiloBoton({ variante: 'secundario' })}>
            Ver la cita
          </Link>
          <a
            href={`/api/admin/citas/${registrada.citaId}/comprobante.pdf`}
            className={estiloBoton({ variante: 'secundario' })}
          >
            Descargar comprobante (PDF)
          </a>
          <Boton onClick={registrarOtra}>Registrar otra cita</Boton>
        </div>
      </section>
    );
  }

  const errores = formState.errors;
  return (
    <section className="flex max-w-2xl flex-col gap-6">
      <EncabezadoPagina
        titulo="Registrar cita en ventanilla"
        descripcion="Para la persona que llega al módulo: usa cupos de hoy con hora futura y no pide captcha."
      />

      <div className="grid gap-4 sm:grid-cols-2">
        {modulos.isPending ? (
          <Cargando texto="Cargando módulos…" />
        ) : modulos.isError ? (
          <ErrorPanel error={modulos.error} />
        ) : (
          <Selector
            etiqueta="Módulo"
            value={moduloElegido ?? ''}
            onChange={(e) => {
              setModuloId(Number(e.target.value) || null);
              setOfertaId(null);
              elegirCupo(null);
            }}
          >
            <option value="">Elige un módulo</option>
            {modulos.data.map((m) => (
              <option key={m.moduloId} value={m.moduloId}>
                {m.nombre}
              </option>
            ))}
          </Selector>
        )}
        {moduloElegido !== null && (
          <Selector
            etiqueta="Trámite"
            value={ofertaId ?? ''}
            disabled={ofertas.isPending}
            ayuda={
              ofertas.data && ofertasActivas.length === 0
                ? 'Este módulo no tiene trámites activos.'
                : undefined
            }
            onChange={(e) => {
              setOfertaId(Number(e.target.value) || null);
              elegirCupo(null);
            }}
          >
            <option value="">Elige un trámite</option>
            {ofertasActivas.map((o) => (
              <option key={o.ofertaId} value={o.ofertaId}>
                {o.tramite}
              </option>
            ))}
          </Selector>
        )}
        {ofertaId !== null && (
          <Campo
            etiqueta="Fecha"
            type="date"
            min={hoy}
            value={fecha}
            error={esFechaIso(fecha) && fecha < hoy ? 'Elige hoy o una fecha posterior.' : undefined}
            onChange={(e) => {
              setFecha(e.target.value);
              elegirCupo(null);
            }}
          />
        )}
      </div>

      {ofertas.isError && <ErrorPanel error={ofertas.error} />}
      {ofertaId !== null && fechaValida && (
        <>
          {cupos.isPending && <Cargando texto="Buscando horas…" />}
          {cupos.isError && <ErrorPanel error={cupos.error} />}
          {cupos.data && cupos.data.length === 0 && (
            <p className="text-slate-700">No hay horas con lugar ese día. Elige otra fecha.</p>
          )}
          {cupos.data && cupos.data.length > 0 && (
            <SelectorHora
              leyenda={`Horas con lugar el ${formatearFechaLarga(fechaValida)}`}
              cupos={cupos.data}
              seleccion={cupoId}
              alElegir={elegirCupo}
            />
          )}
        </>
      )}

      <RegionAnuncios>
        {aviso && (
          <Alerta tono="error" traza={aviso.traza ?? null}>
            <p>{aviso.texto}</p>
            {aviso.curp && (
              <Link to={`/admin/citas?busqueda=${aviso.curp}`} className="text-guinda-700 underline">
                Ver las citas de esta CURP
              </Link>
            )}
          </Alerta>
        )}
      </RegionAnuncios>

      {cupoId !== null && (
        <form
          noValidate
          onSubmit={handleSubmit((valores) => crear.mutate(valores))}
          className="flex flex-col gap-4 rounded-xl border border-slate-200 bg-white p-4 shadow-sm sm:p-5"
        >
          <h2 className="text-lg font-semibold">Datos de la persona</h2>
          <Campo
            etiqueta="CURP"
            autoComplete="off"
            autoCapitalize="characters"
            spellCheck={false}
            maxLength={22}
            error={errores.curp?.message}
            {...register('curp')}
          />
          <Campo
            etiqueta="Nombre o nombres"
            autoComplete="off"
            error={errores.nombre?.message}
            {...register('nombre')}
          />
          <Campo
            etiqueta="Primer apellido"
            autoComplete="off"
            error={errores.apellidoPaterno?.message}
            {...register('apellidoPaterno')}
          />
          <Campo
            etiqueta="Segundo apellido (opcional)"
            autoComplete="off"
            error={errores.apellidoMaterno?.message}
            {...register('apellidoMaterno')}
          />
          <Campo
            etiqueta="Celular (opcional)"
            type="tel"
            inputMode="numeric"
            autoComplete="off"
            maxLength={10}
            error={errores.celular?.message}
            {...register('celular')}
          />
          <Campo
            etiqueta="Correo electrónico (opcional)"
            type="email"
            autoComplete="off"
            ayuda="Si lo da, le enviamos el comprobante y el enlace de su cita."
            error={errores.correo?.message}
            {...register('correo')}
          />
          <div className="flex flex-col gap-1">
            <Casilla
              etiqueta="Leí el aviso de privacidad simplificado a la persona"
              aria-invalid={errores.avisoLeido ? true : undefined}
              aria-describedby={errores.avisoLeido ? 'error-aviso-leido' : undefined}
              {...register('avisoLeido')}
            />
            {errores.avisoLeido && (
              <p id="error-aviso-leido" className="text-sm font-medium text-red-800">
                {errores.avisoLeido.message}
              </p>
            )}
          </div>
          <Boton type="submit" ocupado={crear.isPending} textoOcupado="Registrando…" className="self-start">
            Registrar cita
          </Boton>
        </form>
      )}
    </section>
  );
}
