import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation } from '@tanstack/react-query';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { Link, useSearchParams } from 'react-router';
import { api } from '@/api/cliente';
import { ErrorApi, copiarErroresAlFormulario, mensajeDeError } from '@/api/errores';
import { Alerta, RegionAnuncios } from '@/components/ui/Alerta';
import { Boton } from '@/components/ui/Boton';
import { Campo } from '@/components/ui/Campo';
import { EncabezadoPaso } from '@/components/ui/Encabezado';
import { esquemaContrasenaNueva, type ValoresContrasenaNueva } from './esquemas';

type Proposito = 'activacion' | 'restablecimiento';

const textos: Record<Proposito, { titulo: string; descripcion: string; boton: string; exito: string }> = {
  activacion: {
    titulo: 'Activa tu cuenta',
    descripcion:
      'Define la contraseña de tu cuenta del panel. Al entrar por primera vez configurarás tu app autenticadora.',
    boton: 'Activar cuenta',
    exito: 'Tu cuenta quedó activa. Ya puedes entrar.',
  },
  restablecimiento: {
    titulo: 'Define una contraseña nueva',
    descripcion: 'Escribe la contraseña nueva de tu cuenta del panel.',
    boton: 'Guardar contraseña',
    exito: 'Tu contraseña quedó guardada. Ya puedes entrar.',
  },
};

/** Activación de la cuenta o restablecimiento: el usuario y el token llegan en el enlace del correo. */
export function FormularioContrasenaConToken({ proposito }: { proposito: Proposito }) {
  const [parametros] = useSearchParams();
  const usuarioId = Number.parseInt(parametros.get('usuarioId') ?? '', 10);
  const token = parametros.get('token') ?? '';
  const enlaceCompleto = Number.isInteger(usuarioId) && usuarioId > 0 && token.length > 0;

  const [estado, setEstado] = useState<'formulario' | 'listo' | 'enlace-invalido'>(
    enlaceCompleto ? 'formulario' : 'enlace-invalido',
  );
  const texto = textos[proposito];

  const formulario = useForm<ValoresContrasenaNueva>({
    resolver: zodResolver(esquemaContrasenaNueva),
    defaultValues: { contrasena: '', confirmacion: '' },
  });
  const guardar = useMutation({
    mutationFn: async ({ contrasena }: ValoresContrasenaNueva) => {
      const body = { usuarioId, token, contrasena };
      if (proposito === 'activacion') await api.POST('/api/admin/auth/activacion', { body });
      else await api.POST('/api/admin/auth/restablecimiento', { body });
    },
  });
  const { errors } = formulario.formState;

  const enviar = formulario.handleSubmit(async (valores) => {
    try {
      await guardar.mutateAsync(valores);
      setEstado('listo');
    } catch (error) {
      if (error instanceof ErrorApi && error.codigo === 'TokenInvalido') setEstado('enlace-invalido');
      else copiarErroresAlFormulario(error, formulario.setError, ['contrasena']);
    }
  });

  if (estado === 'enlace-invalido') {
    return (
      <div className="flex flex-col gap-4">
        <EncabezadoPaso>Este enlace ya no es válido</EncabezadoPaso>
        <p className="text-slate-700">
          El enlace venció, ya se usó o está incompleto. Los enlaces duran 24 horas y sirven una sola vez.
        </p>
        <Link
          to="/admin/entrar?olvide=1"
          className="inline-flex min-h-11 items-center text-guinda-700 underline underline-offset-4"
        >
          ¿Olvidaste tu contraseña? Pide un enlace nuevo
        </Link>
      </div>
    );
  }

  if (estado === 'listo') {
    return (
      <div className="flex flex-col gap-4">
        <EncabezadoPaso>{texto.titulo}</EncabezadoPaso>
        <Alerta tono="exito">{texto.exito}</Alerta>
        <Link
          to="/admin/entrar"
          className="inline-flex min-h-11 items-center justify-center rounded-md bg-guinda-700 px-4 py-2 font-medium text-white hover:bg-guinda-800"
        >
          Entrar al panel
        </Link>
      </div>
    );
  }

  const errorGeneral =
    guardar.isError && !(guardar.error instanceof ErrorApi && guardar.error.codigo === 'Validacion');

  return (
    <form onSubmit={enviar} noValidate className="flex flex-col gap-4">
      <EncabezadoPaso>{texto.titulo}</EncabezadoPaso>
      <p className="text-slate-700">{texto.descripcion}</p>
      <RegionAnuncios>
        {errorGeneral && <Alerta tono="error">{mensajeDeError(guardar.error)}</Alerta>}
      </RegionAnuncios>
      <Campo
        etiqueta="Contraseña nueva"
        type="password"
        autoComplete="new-password"
        ayuda="Al menos 12 caracteres. Una frase fácil de recordar funciona bien."
        error={errors.contrasena?.message}
        {...formulario.register('contrasena')}
      />
      <Campo
        etiqueta="Confirma la contraseña"
        type="password"
        autoComplete="new-password"
        error={errors.confirmacion?.message}
        {...formulario.register('confirmacion')}
      />
      <Boton type="submit" ocupado={guardar.isPending} textoOcupado="Guardando…">
        {texto.boton}
      </Boton>
    </form>
  );
}
