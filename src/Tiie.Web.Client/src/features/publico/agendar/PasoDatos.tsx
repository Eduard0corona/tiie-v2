import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useEffect, useRef, useState } from 'react';
import { useForm } from 'react-hook-form';
import { Link, useNavigate } from 'react-router';
import { api, datos } from '@/api/cliente';
import { ErrorApi, copiarErroresAlFormulario, mensajeDeCodigo, mensajeDeError } from '@/api/errores';
import { Captcha, type CaptchaHandle } from '@/components/Captcha';
import { Alerta, RegionAnuncios } from '@/components/ui/Alerta';
import { Boton } from '@/components/ui/Boton';
import { Campo, Casilla } from '@/components/ui/Campo';
import { Cargando, EncabezadoPaso } from '@/components/ui/Encabezado';
import { normalizarCurp } from '@/lib/curp';
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

/** Paso 4: datos del solicitante, aviso de privacidad y captcha; crea la cita (RF-03, RF-08). */
export function PasoDatos({ seleccion, ir }: Asistente) {
  // Una clave por apertura del paso: un reintento por red lenta reutiliza la misma y no duplica la cita.
  const [claveIdempotencia] = useState(() => crypto.randomUUID());
  const [token, setToken] = useState<string | null>(null);
  const [aviso, setAviso] = useState<Aviso | null>(null);
  const [bloqueadoHasta, setBloqueadoHasta] = useState<number | null>(null);
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
  const { register, handleSubmit, formState, setError, getValues } = formulario;

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
      <EncabezadoPaso>Tus datos</EncabezadoPaso>
      <ResumenSeleccion seleccion={seleccion} />
      <p className="text-slate-700">
        Si la cita es para una persona menor de edad, escribe sus datos; el correo y el celular pueden ser de
        su madre, padre o tutor.
      </p>
      <form noValidate onSubmit={enviar} className="flex flex-col gap-4">
        <Campo
          etiqueta="CURP"
          autoComplete="off"
          autoCapitalize="characters"
          spellCheck={false}
          maxLength={22}
          error={errores.curp?.message}
          ayuda="18 letras y números. La fecha de nacimiento y el sexo se toman de la CURP."
          {...register('curp')}
        />
        <Campo
          etiqueta="Nombre o nombres"
          autoComplete="given-name"
          error={errores.nombre?.message}
          {...register('nombre')}
        />
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
        <Campo
          etiqueta="Correo electrónico"
          type="email"
          autoComplete="email"
          error={errores.correo?.message}
          ayuda="Ahí te enviamos el comprobante y el enlace de tu cita."
          {...register('correo')}
        />

        <div className="flex flex-col gap-2 rounded-md border border-slate-200 bg-white p-3 text-sm text-slate-700">
          <p>
            <span className="font-semibold">Aviso de privacidad simplificado:</span> tus datos se usan solo
            para agendar y atender tu cita del trámite de la TIIE y para avisarte sobre ella.{' '}
            <Link
              to="/aviso-de-privacidad"
              target="_blank"
              rel="noopener"
              className="text-guinda-700 underline underline-offset-4"
            >
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
            <p id="error-aviso" className="font-medium text-red-800">
              {errores.aceptaAvisoPrivacidad.message}
            </p>
          )}
        </div>

        {configuracion.data ? (
          <Captcha
            ref={captcha}
            claveSitio={configuracion.data.claveSitioCaptcha}
            alCambiarToken={setToken}
          />
        ) : (
          <Cargando texto="Cargando verificación…" />
        )}

        <RegionAnuncios>
          {aviso && (
            <Alerta tono="error" traza={aviso.traza ?? null}>
              <p>{aviso.texto}</p>
              {aviso.ofrecerRecuperar && (
                <Boton
                  variante="enlace"
                  className="mt-2 px-0"
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

        <div className="flex flex-wrap gap-3">
          <Boton
            type="submit"
            ocupado={crear.isPending}
            textoOcupado="Agendando…"
            disabled={bloqueadoHasta !== null}
          >
            Agendar cita
          </Boton>
          <Boton variante="secundario" onClick={() => ir({ paso: 'hora' })}>
            Cambiar hora
          </Boton>
        </div>
      </form>
    </section>
  );
}
