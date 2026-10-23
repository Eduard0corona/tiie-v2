import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { useNavigate, useSearchParams } from 'react-router';
import { api, datos } from '@/api/cliente';
import { ErrorApi, copiarErroresAlFormulario, mensajeDeError } from '@/api/errores';
import { claves, rutaDeRegreso } from '@/app/consultas';
import { Alerta, RegionAnuncios } from '@/components/ui/Alerta';
import { Boton } from '@/components/ui/Boton';
import { Campo, Casilla } from '@/components/ui/Campo';
import { EncabezadoPaso } from '@/components/ui/Encabezado';
import { formatearHoraDeInstante } from '@/lib/fechas';
import {
  esquemaCodigoMfa,
  esquemaEntrar,
  esquemaOlvide,
  esquemaRecuperacion,
  type ValoresCodigoMfa,
  type ValoresEntrar,
  type ValoresOlvide,
  type ValoresRecuperacion,
} from './esquemas';
import { limpiarCodigo } from './useSesion';

type Paso = 'credenciales' | 'codigo' | 'recuperacion' | 'olvide' | 'olvide-enviado';

/** Usuario y contraseña, luego el código MFA o su configuración (docs/07#inicio-de-sesión). */
export function PaginaEntrar() {
  const [parametros] = useSearchParams();
  const [paso, setPaso] = useState<Paso>(parametros.get('olvide') === '1' ? 'olvide' : 'credenciales');
  const [aviso, setAviso] = useState<string | null>(null);
  const navegar = useNavigate();
  const cliente = useQueryClient();

  const irA = (siguiente: Paso, mensaje: string | null = null) => {
    setAviso(mensaje);
    setPaso(siguiente);
  };

  const sesionIniciada = () => {
    // La sesión anterior en caché ya no sirve: la guarda vuelve a consultarla.
    cliente.removeQueries({ queryKey: claves.admin });
    void navegar(rutaDeRegreso(parametros.get('volver')), { replace: true });
  };

  const expirada = () => irA('credenciales', 'La verificación expiró. Vuelve a escribir tu contraseña.');

  switch (paso) {
    case 'credenciales':
      return (
        <PasoCredenciales
          aviso={aviso}
          alIniciar={sesionIniciada}
          alPedirCodigo={() => irA('codigo')}
          alConfigurarMfa={() => void navegar('/admin/configurar-mfa', { replace: true })}
          alOlvidar={() => irA('olvide')}
        />
      );
    case 'codigo':
      return (
        <PasoCodigo
          alIniciar={sesionIniciada}
          alExpirar={expirada}
          alUsarRecuperacion={() => irA('recuperacion')}
          alVolver={() => irA('credenciales')}
        />
      );
    case 'recuperacion':
      return (
        <PasoRecuperacion alIniciar={sesionIniciada} alExpirar={expirada} alVolver={() => irA('codigo')} />
      );
    case 'olvide':
      return <PasoOlvide alEnviar={() => irA('olvide-enviado')} alVolver={() => irA('credenciales')} />;
    case 'olvide-enviado':
      return <PasoOlvideEnviado alVolver={() => irA('credenciales')} />;
  }
}

function mensajeDeEntrada(error: unknown): string {
  if (error instanceof ErrorApi && error.codigo === 'CuentaBloqueada' && error.problema?.desbloqueoEn) {
    return `La cuenta está bloqueada por demasiados intentos. Podrás intentar de nuevo a las ${formatearHoraDeInstante(error.problema.desbloqueoEn)}.`;
  }
  return mensajeDeError(error);
}

function AlertaError({ error }: { error: unknown }) {
  const traza = error instanceof ErrorApi && error.status >= 500 ? error.traceId : null;
  return (
    <Alerta tono="error" traza={traza}>
      {mensajeDeEntrada(error)}
    </Alerta>
  );
}

function PasoCredenciales(props: {
  aviso: string | null;
  alIniciar: () => void;
  alPedirCodigo: () => void;
  alConfigurarMfa: () => void;
  alOlvidar: () => void;
}) {
  const formulario = useForm<ValoresEntrar>({
    resolver: zodResolver(esquemaEntrar),
    defaultValues: { usuario: '', contrasena: '' },
  });
  const entrar = useMutation({
    mutationFn: async (valores: ValoresEntrar) =>
      datos(await api.POST('/api/admin/auth/entrar', { body: valores })),
  });
  const { errors } = formulario.formState;

  const enviar = formulario.handleSubmit(async (valores) => {
    try {
      const { siguientePaso } = await entrar.mutateAsync(valores);
      if (siguientePaso === 'Listo') props.alIniciar();
      else if (siguientePaso === 'CodigoMfa') props.alPedirCodigo();
      else props.alConfigurarMfa();
    } catch (error) {
      copiarErroresAlFormulario(error, formulario.setError, ['usuario', 'contrasena']);
      formulario.resetField('contrasena', { keepError: true });
    }
  });

  return (
    <form onSubmit={enviar} noValidate className="flex flex-col gap-4">
      <EncabezadoPaso>Entrar al panel</EncabezadoPaso>
      <RegionAnuncios>
        {entrar.isError ? (
          <AlertaError error={entrar.error} />
        ) : (
          props.aviso && <Alerta tono="info">{props.aviso}</Alerta>
        )}
      </RegionAnuncios>
      <Campo
        etiqueta="Usuario"
        autoComplete="username"
        autoCapitalize="none"
        spellCheck={false}
        error={errors.usuario?.message}
        {...formulario.register('usuario')}
      />
      <Campo
        etiqueta="Contraseña"
        type="password"
        autoComplete="current-password"
        error={errors.contrasena?.message}
        {...formulario.register('contrasena')}
      />
      <Boton type="submit" ocupado={entrar.isPending} textoOcupado="Entrando…">
        Entrar
      </Boton>
      <Boton variante="enlace" onClick={props.alOlvidar} className="self-start px-0">
        ¿Olvidaste tu contraseña?
      </Boton>
    </form>
  );
}

function PasoCodigo(props: {
  alIniciar: () => void;
  alExpirar: () => void;
  alUsarRecuperacion: () => void;
  alVolver: () => void;
}) {
  const formulario = useForm<ValoresCodigoMfa>({
    resolver: zodResolver(esquemaCodigoMfa),
    defaultValues: { codigo: '', recordarEsteEquipo: false },
  });
  const verificar = useMutation({
    mutationFn: async (valores: ValoresCodigoMfa) => {
      await api.POST('/api/admin/auth/entrar/mfa', {
        body: { codigo: limpiarCodigo(valores.codigo), recordarEsteEquipo: valores.recordarEsteEquipo },
      });
    },
  });
  const { errors } = formulario.formState;

  const enviar = formulario.handleSubmit(async (valores) => {
    try {
      await verificar.mutateAsync(valores);
      props.alIniciar();
    } catch (error) {
      if (error instanceof ErrorApi && error.codigo === 'NoAutenticado') props.alExpirar();
      else {
        copiarErroresAlFormulario(error, formulario.setError, ['codigo']);
        formulario.resetField('codigo', { keepError: true });
      }
    }
  });

  return (
    <form onSubmit={enviar} noValidate className="flex flex-col gap-4">
      <EncabezadoPaso>Verificación en dos pasos</EncabezadoPaso>
      <p className="text-slate-700">Abre tu app autenticadora y escribe el código de 6 dígitos de TIIE.</p>
      <RegionAnuncios>{verificar.isError && <AlertaError error={verificar.error} />}</RegionAnuncios>
      <Campo
        etiqueta="Código de 6 dígitos"
        inputMode="numeric"
        autoComplete="one-time-code"
        maxLength={7}
        error={errors.codigo?.message}
        {...formulario.register('codigo')}
      />
      <Casilla etiqueta="Recordar este equipo por 14 días" {...formulario.register('recordarEsteEquipo')} />
      <Boton type="submit" ocupado={verificar.isPending} textoOcupado="Verificando…">
        Verificar
      </Boton>
      <div className="flex flex-col items-start gap-1">
        <Boton variante="enlace" onClick={props.alUsarRecuperacion} className="px-0">
          Usar un código de recuperación
        </Boton>
        <Boton variante="enlace" onClick={props.alVolver} className="px-0">
          Volver a escribir usuario y contraseña
        </Boton>
      </div>
    </form>
  );
}

function PasoRecuperacion(props: { alIniciar: () => void; alExpirar: () => void; alVolver: () => void }) {
  const formulario = useForm<ValoresRecuperacion>({
    resolver: zodResolver(esquemaRecuperacion),
    defaultValues: { codigoRecuperacion: '' },
  });
  const verificar = useMutation({
    mutationFn: async (valores: ValoresRecuperacion) => {
      await api.POST('/api/admin/auth/entrar/recuperacion', { body: valores });
    },
  });
  const { errors } = formulario.formState;

  const enviar = formulario.handleSubmit(async (valores) => {
    try {
      await verificar.mutateAsync(valores);
      props.alIniciar();
    } catch (error) {
      if (error instanceof ErrorApi && error.codigo === 'NoAutenticado') props.alExpirar();
      else copiarErroresAlFormulario(error, formulario.setError, ['codigoRecuperacion']);
    }
  });

  return (
    <form onSubmit={enviar} noValidate className="flex flex-col gap-4">
      <EncabezadoPaso>Código de recuperación</EncabezadoPaso>
      <p className="text-slate-700">
        Escribe uno de los códigos que guardaste al configurar la verificación. Cada código sirve una sola
        vez.
      </p>
      <RegionAnuncios>{verificar.isError && <AlertaError error={verificar.error} />}</RegionAnuncios>
      <Campo
        etiqueta="Código de recuperación"
        autoComplete="off"
        autoCapitalize="characters"
        spellCheck={false}
        error={errors.codigoRecuperacion?.message}
        {...formulario.register('codigoRecuperacion')}
      />
      <Boton type="submit" ocupado={verificar.isPending} textoOcupado="Verificando…">
        Entrar
      </Boton>
      <Boton variante="enlace" onClick={props.alVolver} className="self-start px-0">
        Usar el código de la app
      </Boton>
    </form>
  );
}

function PasoOlvide(props: { alEnviar: () => void; alVolver: () => void }) {
  const formulario = useForm<ValoresOlvide>({
    resolver: zodResolver(esquemaOlvide),
    defaultValues: { correo: '' },
  });
  const solicitar = useMutation({
    mutationFn: async (valores: ValoresOlvide) => {
      await api.POST('/api/admin/auth/olvide-contrasena', { body: valores });
    },
  });
  const { errors } = formulario.formState;

  const enviar = formulario.handleSubmit(async (valores) => {
    try {
      await solicitar.mutateAsync(valores);
      props.alEnviar();
    } catch (error) {
      copiarErroresAlFormulario(error, formulario.setError, ['correo']);
    }
  });

  return (
    <form onSubmit={enviar} noValidate className="flex flex-col gap-4">
      <EncabezadoPaso>Restablecer contraseña</EncabezadoPaso>
      <p className="text-slate-700">
        Escribe el correo de tu cuenta. Te enviaremos un enlace para definir una contraseña nueva.
      </p>
      <RegionAnuncios>{solicitar.isError && <AlertaError error={solicitar.error} />}</RegionAnuncios>
      <Campo
        etiqueta="Correo"
        type="email"
        autoComplete="email"
        error={errors.correo?.message}
        {...formulario.register('correo')}
      />
      <Boton type="submit" ocupado={solicitar.isPending} textoOcupado="Enviando…">
        Enviar enlace
      </Boton>
      <Boton variante="enlace" onClick={props.alVolver} className="self-start px-0">
        Volver a entrar
      </Boton>
    </form>
  );
}

function PasoOlvideEnviado({ alVolver }: { alVolver: () => void }) {
  return (
    <div className="flex flex-col gap-4">
      <EncabezadoPaso>Revisa tu correo</EncabezadoPaso>
      <p className="text-slate-700">
        Si el correo está registrado, te enviamos un enlace para definir una contraseña nueva. Vence en 24
        horas.
      </p>
      <Boton variante="secundario" onClick={alVolver}>
        Volver a entrar
      </Boton>
    </div>
  );
}
