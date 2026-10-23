import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { api } from '@/api/cliente';
import { ErrorApi, copiarErroresAlFormulario, mensajeDeError } from '@/api/errores';
import { Alerta, RegionAnuncios } from '@/components/ui/Alerta';
import { Boton } from '@/components/ui/Boton';
import { Campo } from '@/components/ui/Campo';
import { esquemaCambioContrasena, type ValoresCambioContrasena } from '@/features/admin/sesion/esquemas';
import { EncabezadoPagina } from '@/features/admin/comun/Componentes';

export function PaginaMiCuenta() {
  const formulario = useForm<ValoresCambioContrasena>({
    resolver: zodResolver(esquemaCambioContrasena),
    defaultValues: { actual: '', nueva: '', confirmacion: '' },
  });
  const cambiar = useMutation({
    mutationFn: async ({ actual, nueva }: ValoresCambioContrasena) => {
      await api.POST('/api/admin/auth/contrasena', { body: { actual, nueva } });
    },
  });
  const { errors } = formulario.formState;

  const enviar = formulario.handleSubmit(async (valores) => {
    try {
      await cambiar.mutateAsync(valores);
      formulario.reset();
    } catch (error) {
      copiarErroresAlFormulario(error, formulario.setError, ['actual', 'nueva']);
    }
  });

  const errorGeneral =
    cambiar.isError && !(cambiar.error instanceof ErrorApi && cambiar.error.codigo === 'Validacion');

  return (
    <section className="flex max-w-xl flex-col gap-6">
      <EncabezadoPagina titulo="Mi cuenta" descripcion="Tu contraseña para entrar al panel." />
      <form
        onSubmit={enviar}
        noValidate
        className="flex flex-col gap-4 rounded-xl border border-slate-200 bg-white p-4 shadow-sm sm:p-6"
      >
        <h2 className="text-lg font-bold text-slate-950">Cambiar contraseña</h2>
        <RegionAnuncios>
          {cambiar.isSuccess && <Alerta tono="exito">Tu contraseña cambió.</Alerta>}
          {errorGeneral && <Alerta tono="error">{mensajeDeError(cambiar.error)}</Alerta>}
        </RegionAnuncios>
        <Campo
          etiqueta="Contraseña actual"
          type="password"
          autoComplete="current-password"
          error={errors.actual?.message}
          {...formulario.register('actual')}
        />
        <Campo
          etiqueta="Contraseña nueva"
          type="password"
          autoComplete="new-password"
          ayuda="Al menos 12 caracteres."
          error={errors.nueva?.message}
          {...formulario.register('nueva')}
        />
        <Campo
          etiqueta="Confirma la contraseña nueva"
          type="password"
          autoComplete="new-password"
          error={errors.confirmacion?.message}
          {...formulario.register('confirmacion')}
        />
        <Boton type="submit" ocupado={cambiar.isPending} textoOcupado="Guardando…" className="self-start">
          Cambiar contraseña
        </Boton>
      </form>
    </section>
  );
}
