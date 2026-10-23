import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation } from '@tanstack/react-query';
import { useEffect, useRef, useState } from 'react';
import { useForm } from 'react-hook-form';
import { Link, useLocation } from 'react-router';
import { api } from '@/api/cliente';
import { ErrorApi, copiarErroresAlFormulario, mensajeDeCodigo, mensajeDeError } from '@/api/errores';
import { Captcha, type CaptchaHandle } from '@/components/Captcha';
import { Alerta, RegionAnuncios } from '@/components/ui/Alerta';
import { Boton } from '@/components/ui/Boton';
import { Campo } from '@/components/ui/Campo';
import { Cargando, EncabezadoPaso } from '@/components/ui/Encabezado';
import { normalizarCurp } from '@/lib/curp';
import { useConfiguracion } from '../comun/usePublico';
import { esquemaRecuperar, type ValoresRecuperar } from './esquemas';

/** El mismo mensaje exista o no la cita: quien pregunta no puede saber si hay una (RF-06). */
export const MENSAJE_RECUPERACION =
  'Si los datos coinciden con una cita agendada, te enviamos el enlace a tu correo. Revisa también la carpeta de correo no deseado.';

interface EstadoRecuperar {
  curp?: string;
}

/** Recuperar el enlace de la cita por correo (RF-06). */
export function PaginaRecuperar() {
  const ubicacion = useLocation();
  // La CURP llega del asistente por el estado de navegación, nunca por la URL.
  const curpInicial = (ubicacion.state as EstadoRecuperar | null)?.curp ?? '';
  const [token, setToken] = useState<string | null>(null);
  const [aviso, setAviso] = useState<{ texto: string; traza?: string | null } | null>(null);
  const [enviado, setEnviado] = useState(false);
  const [bloqueadoHasta, setBloqueadoHasta] = useState<number | null>(null);
  const captcha = useRef<CaptchaHandle>(null);
  const configuracion = useConfiguracion();

  const { register, handleSubmit, formState, setError } = useForm<ValoresRecuperar>({
    resolver: zodResolver(esquemaRecuperar),
    defaultValues: { curp: curpInicial, correo: '' },
  });

  useEffect(() => {
    if (bloqueadoHasta === null) return;
    const espera = setTimeout(() => setBloqueadoHasta(null), Math.max(0, bloqueadoHasta - Date.now()));
    return () => clearTimeout(espera);
  }, [bloqueadoHasta]);

  const recuperar = useMutation({
    mutationFn: async (valores: ValoresRecuperar) => {
      await api.POST('/api/publico/citas/recuperacion', {
        body: { curp: normalizarCurp(valores.curp), correo: valores.correo.trim(), tokenCaptcha: token },
      });
    },
    onSuccess: () => setEnviado(true),
    onError: (error) => {
      if (!(error instanceof ErrorApi)) {
        setAviso({ texto: mensajeDeError(error) });
        return;
      }
      switch (error.codigo) {
        case 'CaptchaInvalido':
          captcha.current?.reiniciar();
          setAviso({ texto: mensajeDeCodigo('CaptchaInvalido') });
          return;
        case 'Validacion':
          if (!copiarErroresAlFormulario(error, setError, ['curp', 'correo']))
            setAviso({ texto: error.mensaje });
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
    recuperar.mutate(valores);
  });

  if (enviado) {
    return (
      <section className="flex flex-col gap-4">
        <EncabezadoPaso>Revisa tu correo</EncabezadoPaso>
        <Alerta tono="exito">{MENSAJE_RECUPERACION}</Alerta>
        <p>
          <Link
            to="/"
            className="inline-flex min-h-11 items-center text-guinda-700 underline underline-offset-4"
          >
            Volver al inicio
          </Link>
        </p>
      </section>
    );
  }

  return (
    <section className="flex flex-col gap-6">
      <EncabezadoPaso>Recupera el enlace de tu cita</EncabezadoPaso>
      <p className="text-slate-700">
        Escribe la CURP de la persona que tiene la cita y el correo que registraste. Te enviaremos el enlace a
        ese correo.
      </p>
      <form noValidate onSubmit={enviar} className="flex flex-col gap-4">
        <Campo
          etiqueta="CURP"
          autoComplete="off"
          autoCapitalize="characters"
          spellCheck={false}
          maxLength={22}
          error={formState.errors.curp?.message}
          {...register('curp')}
        />
        <Campo
          etiqueta="Correo electrónico"
          type="email"
          autoComplete="email"
          error={formState.errors.correo?.message}
          {...register('correo')}
        />
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
              {aviso.texto}
            </Alerta>
          )}
        </RegionAnuncios>
        <div>
          <Boton
            type="submit"
            ocupado={recuperar.isPending}
            textoOcupado="Enviando…"
            disabled={bloqueadoHasta !== null}
          >
            Enviarme el enlace
          </Boton>
        </div>
      </form>
    </section>
  );
}
