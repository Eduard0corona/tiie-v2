import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useEffect, useRef, useState, type ReactNode } from 'react';
import { useForm, useWatch } from 'react-hook-form';
import { Link, useNavigate } from 'react-router';
import { api, datos } from '@/api/cliente';
import { ErrorApi, copiarErroresAlFormulario, mensajeDeCodigo, mensajeDeError } from '@/api/errores';
import { Captcha, type CaptchaHandle } from '@/components/Captcha';
import { Alerta, RegionAnuncios } from '@/components/ui/Alerta';
import { Boton } from '@/components/ui/Boton';
import { Campo, Casilla } from '@/components/ui/Campo';
import { Cargando, EncabezadoPaso } from '@/components/ui/Encabezado';
import { Icono } from '@/components/ui/Icono';
import { sugerirCorreo } from '@/lib/correo';
import { interpretarCurp, normalizarCurp, type Sexo } from '@/lib/curp';
import { formatearFechaCorta } from '@/lib/fechas';
import { clavesPublicas, useConfiguracion } from '../comun/usePublico';
import type { useEstadoAsistente } from './estadoAsistente';
import { CAMPOS_DATOS, esquemaDatos, type ValoresDatos } from './esquemas';
import { ResumenSeleccion } from './ResumenSeleccion';

type Asistente = ReturnType<typeof useEstadoAsistente>;

interface Aviso {
  texto: string;
  traza?: string | null;
  ofrecerRecuperar?: boolean;
}

const TEXTO_SEXO: Record<Sexo, string> = { H: 'hombre', M: 'mujer', X: 'no binario' };

const enlace = 'font-semibold text-guinda-700 underline underline-offset-4 hover:text-guinda-900';

/** Paso 4: datos del solicitante, aviso de privacidad y captcha; crea la cita (RF-03, RF-08). */
export function PasoDatos({ seleccion, ir }: Asistente) {
  // Una clave por apertura del paso: un reintento por red lenta reutiliza la misma y no duplica la cita.
  const [claveIdempotencia] = useState(() => crypto.randomUUID());
  const [token, setToken] = useState<string | null>(null);
  const [aviso, setAviso] = useState<Aviso | null>(null);
  const [bloqueadoHasta, setBloqueadoHasta] = useState<number | null>(null);
  const [revisarCorreo, setRevisarCorreo] = useState(false);
  const captcha = useRef<CaptchaHandle>(null);
  const configuracion = useConfiguracion();
  const navegar = useNavigate();
  const cliente = useQueryClient();
  const ofertaId = seleccion.oferta ?? 0;

  const formulario = useForm<ValoresDatos>({
    resolver: zodResolver(esquemaDatos),
    defaultValues: {
      curp: '',
      nombre: '',
      apellidoPaterno: '',
      apellidoMaterno: '',
      celular: '',
      correo: '',
      aceptaAvisoPrivacidad: false,
    },
  });
  const { register, handleSubmit, formState, setError, getValues, setValue, control } = formulario;
  const [curp, correo] = useWatch({ control, name: ['curp', 'correo'] });
  const datosCurp = interpretarCurp(curp);
  const sugerencia = revisarCorreo ? sugerirCorreo(correo) : null;
  const registroCorreo = register('correo');

  useEffect(() => {
    if (bloqueadoHasta === null) return;
    const espera = setTimeout(() => setBloqueadoHasta(null), Math.max(0, bloqueadoHasta - Date.now()));
    return () => clearTimeout(espera);
  }, [bloqueadoHasta]);

  const crear = useMutation({
    mutationFn: async (valores: ValoresDatos) =>
      datos(
        await api.POST('/api/publico/citas', {
          params: { header: { 'Idempotency-Key': claveIdempotencia } },
          body: {
            cupoId: seleccion.cupo ?? 0,
            curp: normalizarCurp(valores.curp),
            nombre: valores.nombre.trim(),
            apellidoPaterno: valores.apellidoPaterno.trim(),
            apellidoMaterno: valores.apellidoMaterno.trim() || null,
            celular: valores.celular.trim(),
            correo: valores.correo.trim(),
            aceptaAvisoPrivacidad: valores.aceptaAvisoPrivacidad,
            tokenCaptcha: token,
          },
        }),
      ),
    onSuccess: () => {
      void cliente.invalidateQueries({ queryKey: clavesPublicas.disponibilidad(ofertaId) });
      void navegar('/mi-cita?nueva=1');
    },
    onError: (error) => {
      if (!(error instanceof ErrorApi)) {
        setAviso({ texto: mensajeDeError(error) });
        return;
      }
      switch (error.codigo) {
        case 'SinCupo':
          void cliente.invalidateQueries({ queryKey: ['publico', 'cupos', ofertaId] });
          ir({ paso: 'hora', cupo: null }, { aviso: mensajeDeCodigo('SinCupo') });
          return;
        case 'FueraDeVentana':
        case 'CupoCerrado':
        case 'OfertaInactiva':
          void cliente.invalidateQueries({ queryKey: clavesPublicas.disponibilidad(ofertaId) });
          ir({ paso: 'fecha', fecha: null, cupo: null }, { aviso: mensajeDeCodigo(error.codigo) });
          return;
        case 'YaTieneCita':
          setAviso({ texto: mensajeDeCodigo('YaTieneCita'), ofrecerRecuperar: true });
          return;
        case 'CaptchaInvalido':
          captcha.current?.reiniciar();
          setAviso({ texto: mensajeDeCodigo('CaptchaInvalido') });
          return;
        case 'Validacion':
          if (!copiarErroresAlFormulario(error, setError, CAMPOS_DATOS)) setAviso({ texto: error.mensaje });
          else setAviso(null);
          return;
        case 'DemasiadasSolicitudes':
          setBloqueadoHasta(Date.now() + (error.retryAfter ?? 60) * 1000);
          setAviso({ texto: error.mensaje });
          return;
        default:
          setAviso({ texto: error.mensaje, traza: error.traceId });
      }
    },
  });

  const enviar = handleSubmit((valores) => {
    if (!token) {
      setAviso({ texto: mensajeDeCodigo('CaptchaInvalido') });
      return;
    }
    setAviso(null);
    crear.mutate(valores);
  });

  const errores = formState.errors;
  return (
    <section className="flex flex-col gap-6">
      <div className="flex flex-col gap-2">
        <EncabezadoPaso className="text-3xl tracking-tight sm:text-4xl">Tus datos</EncabezadoPaso>
        <p className="text-lg text-slate-600">
          Con ellos te atendemos en el módulo y te enviamos tu comprobante. Solo toma un minuto.
        </p>
      </div>
      <ResumenSeleccion seleccion={seleccion} />
      <form noValidate onSubmit={enviar} className="flex flex-col gap-6">
        <Tarjeta>
          <fieldset className="flex min-w-0 flex-col gap-5">
            <legend className="mb-1 flex items-center gap-2 text-lg font-bold text-slate-950">
              <Icono nombre="usuario" className="text-guinda-700" />
              ¿Quién hará el trámite?
            </legend>
            <p className="-mt-2 rounded-lg bg-slate-50 px-3 py-2 text-sm text-slate-700">
              Si la cita es para una persona menor de edad, escribe sus datos; el correo y el celular pueden
              ser de su madre, padre o tutor.
            </p>
            <Campo
              etiqueta="CURP"
              autoComplete="off"
              autoCapitalize="characters"
              spellCheck={false}
              maxLength={22}
              className="[&_input]:font-mono [&_input]:tracking-wider [&_input]:uppercase"
              error={errores.curp?.message}
              ayuda={
                <>
                  18 letras y números. La fecha de nacimiento y el sexo se toman de la CURP.{' '}
                  <a
                    href="https://www.gob.mx/curp/"
                    target="_blank"
                    rel="noopener noreferrer"
                    className={enlace}
                  >
                    Consulta tu CURP en gob.mx (abre en una pestaña nueva)
                  </a>
                </>
              }
              confirmacion={
                datosCurp &&
                `CURP válida: nacimiento el ${formatearFechaCorta(datosCurp.fechaNacimiento)}, sexo ${TEXTO_SEXO[datosCurp.sexo]}.`
              }
              {...register('curp')}
            />
            <Campo
              etiqueta="Nombre o nombres"
              autoComplete="given-name"
              error={errores.nombre?.message}
              {...register('nombre')}
            />
            <div className="grid gap-5 sm:grid-cols-2">
              <Campo
                etiqueta="Primer apellido"
                autoComplete="family-name"
                error={errores.apellidoPaterno?.message}
                {...register('apellidoPaterno')}
              />
              <Campo
                etiqueta="Segundo apellido (opcional)"
                autoComplete="additional-name"
                error={errores.apellidoMaterno?.message}
                {...register('apellidoMaterno')}
              />
            </div>
          </fieldset>
        </Tarjeta>

        <Tarjeta>
          <fieldset className="flex min-w-0 flex-col gap-5">
            <legend className="mb-1 flex items-center gap-2 text-lg font-bold text-slate-950">
              <Icono nombre="correo" className="text-guinda-700" />
              ¿Dónde te avisamos?
            </legend>
            <Campo
              etiqueta="Celular"
              type="tel"
              inputMode="numeric"
              autoComplete="tel-national"
              maxLength={10}
              error={errores.celular?.message}
              ayuda="10 dígitos, sin espacios."
              {...register('celular')}
            />
            <div className="flex flex-col gap-2">
              <Campo
                etiqueta="Correo electrónico"
                type="email"
                autoComplete="email"
                error={errores.correo?.message}
                ayuda="Ahí te enviamos el comprobante y el enlace para consultar, cambiar o cancelar tu cita."
                {...registroCorreo}
                onChange={(e) => {
                  setRevisarCorreo(false);
                  void registroCorreo.onChange(e);
                }}
                onBlur={(e) => {
                  setRevisarCorreo(true);
                  void registroCorreo.onBlur(e);
                }}
              />
              <RegionAnuncios>
                {sugerencia && (
                  <p className="flex flex-wrap items-center gap-x-3 rounded-lg bg-amber-50 px-3 py-1 text-sm text-amber-950 ring-1 ring-amber-200">
                    <span>
                      ¿Quisiste decir <strong className="font-semibold">{sugerencia}</strong>?
                    </span>
                    <Boton
                      variante="enlace"
                      onClick={() =>
                        setValue('correo', sugerencia, { shouldValidate: formState.isSubmitted })
                      }
                    >
                      Sí, usar este correo
                    </Boton>
                  </p>
                )}
              </RegionAnuncios>
            </div>
          </fieldset>
        </Tarjeta>

        <Tarjeta>
          <div className="flex flex-col gap-3 text-sm text-slate-700">
            <p className="flex items-center gap-2 text-base font-bold text-slate-950">
              <Icono nombre="escudo" className="text-guinda-700" />
              Aviso de privacidad simplificado
            </p>
            <p>
              Tus datos se usan solo para agendar y atender tu cita del trámite de la TIIE y para avisarte
              sobre ella.{' '}
              <Link to="/aviso-de-privacidad" target="_blank" rel="noopener" className={enlace}>
                Consulta el aviso integral (abre en una pestaña nueva)
              </Link>
              .
            </p>
            <Casilla
              etiqueta="Leí y acepto el aviso de privacidad"
              aria-invalid={errores.aceptaAvisoPrivacidad ? true : undefined}
              aria-describedby={errores.aceptaAvisoPrivacidad ? 'error-aviso' : undefined}
              {...register('aceptaAvisoPrivacidad')}
            />
            {errores.aceptaAvisoPrivacidad && (
              <p id="error-aviso" className="flex items-start gap-1.5 font-medium text-red-800">
                <Icono nombre="error" className="mt-px size-4" />
                {errores.aceptaAvisoPrivacidad.message}
              </p>
            )}
            <div className="border-t border-slate-100 pt-4">
              {configuracion.data ? (
                <Captcha
                  ref={captcha}
                  claveSitio={configuracion.data.claveSitioCaptcha}
                  alCambiarToken={setToken}
                />
              ) : (
                <Cargando texto="Cargando verificación…" />
              )}
            </div>
          </div>
        </Tarjeta>

        <RegionAnuncios>
          {aviso && (
            <Alerta tono="error" traza={aviso.traza ?? null}>
              <p>{aviso.texto}</p>
              {aviso.ofrecerRecuperar && (
                <Boton
                  variante="enlace"
                  className="mt-1"
                  onClick={() =>
                    void navegar('/recuperar', { state: { curp: normalizarCurp(getValues('curp')) } })
                  }
                >
                  Recibir mi enlace por correo
                </Boton>
              )}
            </Alerta>
          )}
        </RegionAnuncios>

        <div className="flex flex-col gap-3">
          <div className="flex flex-col gap-2 sm:flex-row sm:gap-3">
            <Boton
              type="submit"
              tamano="grande"
              bloque
              ocupado={crear.isPending}
              textoOcupado="Agendando…"
              disabled={bloqueadoHasta !== null}
            >
              Agendar cita
            </Boton>
            <Boton variante="secundario" tamano="grande" bloque onClick={() => ir({ paso: 'hora' })}>
              Cambiar hora
            </Boton>
          </div>
          <p className="flex items-center gap-2 text-sm text-slate-600">
            <Icono nombre="correo" className="size-4" />
            Al agendar te enviamos el comprobante por correo.
          </p>
        </div>
      </form>
    </section>
  );
}

function Tarjeta({ children }: { children: ReactNode }) {
  return (
    <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-tarjeta sm:p-6">{children}</div>
  );
}
