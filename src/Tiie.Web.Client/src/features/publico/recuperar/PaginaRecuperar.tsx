import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation } from '@tanstack/react-query';
import { useEffect, useRef, useState } from 'react';
import { useForm } from 'react-hook-form';
import { Link, useLocation } from 'react-router';
import { api } from '@/api/cliente';
import { ErrorApi, copiarErroresAlFormulario, mensajeDeCodigo, mensajeDeError } from '@/api/errores';
import { Captcha, type CaptchaHandle } from '@/components/Captcha';
import { Alerta, RegionAnuncios } from '@/components/ui/Alerta';
import { Boton, estiloBoton } from '@/components/ui/Boton';
import { Campo } from '@/components/ui/Campo';
import { Contenedor } from '@/components/ui/Contenedor';
import { Cargando, EncabezadoPaso } from '@/components/ui/Encabezado';
import { Icono } from '@/components/ui/Icono';
import { normalizarCurp } from '@/lib/curp';
import { EstadoVacio } from '../comun/EstadoVacio';
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
      <EstadoVacio
        icono="correo"
        tono="exito"
        titulo="Revisa tu correo"
        acciones={
          <Link to="/" className={estiloBoton({ variante: 'secundario', tamano: 'grande' })}>
            Volver al inicio
          </Link>
        }
      >
        <p>{MENSAJE_RECUPERACION}</p>
        <p className="text-base">El enlace llega en unos minutos. Ábrelo desde el mismo correo.</p>
      </EstadoVacio>
    );
  }

  return (
    <Contenedor ancho="amplio" className="py-8 sm:py-12">
      <div className="grid gap-10 lg:grid-cols-[1fr_26rem] lg:items-start">
        <section className="flex flex-col gap-6">
          <div className="flex flex-col gap-2">
            <EncabezadoPaso className="text-3xl tracking-tight sm:text-4xl">
              Recupera el enlace de tu cita
            </EncabezadoPaso>
            <p className="text-lg text-slate-600">
              Escribe la CURP de la persona que tiene la cita y el correo que registraste. Te enviaremos el
              enlace a ese correo.
            </p>
          </div>
          <form
            noValidate
            onSubmit={enviar}
            className="flex flex-col gap-5 rounded-2xl border border-slate-200 bg-white p-5 shadow-tarjeta sm:p-6"
          >
            <Campo
              etiqueta="CURP"
              autoComplete="off"
              autoCapitalize="characters"
              spellCheck={false}
              maxLength={22}
              className="[&_input]:font-mono [&_input]:tracking-wider [&_input]:uppercase"
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
                tamano="grande"
                bloque
                ocupado={recuperar.isPending}
                textoOcupado="Enviando…"
                disabled={bloqueadoHasta !== null}
              >
                <Icono nombre="correo" />
                Enviarme el enlace
              </Boton>
            </div>
          </form>
        </section>
        <aside
          aria-labelledby="titulo-como-funciona"
          className="rounded-2xl bg-guinda-50 p-6 ring-1 ring-guinda-100"
        >
          <h2 id="titulo-como-funciona" className="text-lg font-bold text-slate-950">
            ¿Cómo funciona?
          </h2>
          <ol className="mt-4 flex flex-col gap-4">
            {[
              'Escribe la CURP y el correo con los que agendaste.',
              'Si coinciden con una cita agendada, te enviamos el enlace.',
              'Ábrelo para ver, cambiar o cancelar tu cita.',
            ].map((texto, i) => (
              <li key={texto} className="flex gap-3 text-slate-700">
                <span className="grid size-7 shrink-0 place-items-center rounded-full bg-guinda-700 text-sm font-bold text-white">
                  {i + 1}
                </span>
                {texto}
              </li>
            ))}
          </ol>
          <p className="mt-5 flex gap-2 border-t border-guinda-100 pt-4 text-sm text-slate-600">
            <Icono nombre="escudo" className="mt-0.5 size-4 text-guinda-700" />
            Por tu privacidad, nunca mostramos una cita solo con la CURP.
          </p>
        </aside>
      </div>
    </Contenedor>
  );
}
