import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { QRCodeSVG } from 'qrcode.react';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { Navigate, useNavigate } from 'react-router';
import { api, datos } from '@/api/cliente';
import { ErrorApi, copiarErroresAlFormulario, mensajeDeError } from '@/api/errores';
import { claves, rutaEntrar } from '@/app/consultas';
import { Alerta, RegionAnuncios } from '@/components/ui/Alerta';
import { Boton } from '@/components/ui/Boton';
import { Campo } from '@/components/ui/Campo';
import { Cargando, EncabezadoPaso } from '@/components/ui/Encabezado';
import { esquemaActivarMfa, type ValoresActivarMfa } from './esquemas';
import { limpiarCodigo, useSalir, useSesion } from './useSesion';

/**
 * Primer inicio de sesión: configurar la app autenticadora. La clave nunca sale del navegador hacia un
 * servicio externo: el QR se dibuja aquí (docs/07#inicio-de-sesión).
 */
export function PaginaConfigurarMfa() {
  const sesion = useSesion();
  const [codigos, setCodigos] = useState<string[] | null>(null);

  if (codigos) return <PasoCodigosRecuperacion codigos={codigos} />;
  if (sesion.isPending) return <Cargando />;
  if (sesion.error instanceof ErrorApi && sesion.error.status === 401) {
    return <Navigate to={rutaEntrar('/admin/configurar-mfa')} replace />;
  }
  if (sesion.isError) return <Alerta tono="error">{mensajeDeError(sesion.error)}</Alerta>;
  if (sesion.data.mfaActivo) return <Navigate to="/admin/agenda" replace />;
  return <PasoConfigurar alActivar={setCodigos} />;
}

function PasoConfigurar({ alActivar }: { alActivar: (codigos: string[]) => void }) {
  const clave = useQuery({
    queryKey: claves.claveMfa,
    queryFn: async () => datos(await api.GET('/api/admin/auth/mfa')),
    staleTime: Infinity,
    retry: false,
  });
  const formulario = useForm<ValoresActivarMfa>({
    resolver: zodResolver(esquemaActivarMfa),
    defaultValues: { codigo: '' },
  });
  const activar = useMutation({
    mutationFn: async (valores: ValoresActivarMfa) =>
      datos(await api.POST('/api/admin/auth/mfa', { body: { codigo: limpiarCodigo(valores.codigo) } })),
  });
  const salir = useSalir();
  const { errors } = formulario.formState;

  const enviar = formulario.handleSubmit(async (valores) => {
    try {
      const { codigosRecuperacion } = await activar.mutateAsync(valores);
      alActivar(codigosRecuperacion);
    } catch (error) {
      copiarErroresAlFormulario(error, formulario.setError, ['codigo']);
    }
  });

  // Un 422 marca el campo; los demás errores se muestran arriba.
  const errorGeneral =
    activar.isError && !(activar.error instanceof ErrorApi && activar.error.codigo === 'Validacion');

  return (
    <div className="flex flex-col gap-4">
      <EncabezadoPaso>Configura la verificación en dos pasos</EncabezadoPaso>
      <p className="text-slate-700">
        Para proteger los datos de las citas, el panel pide un código de tu celular además de la contraseña.
      </p>
      <ol className="list-decimal space-y-2 pl-5 text-slate-800">
        <li>Instala una app autenticadora, por ejemplo Microsoft Authenticator o Google Authenticator.</li>
        <li>En la app, agrega una cuenta y escanea el código QR. Si no puedes, escribe la clave.</li>
        <li>Escribe el código de 6 dígitos que muestra la app.</li>
      </ol>

      {clave.isPending && <Cargando texto="Preparando tu clave…" />}
      {clave.isError && <Alerta tono="error">{mensajeDeError(clave.error)}</Alerta>}
      {clave.data && (
        <div className="flex flex-col items-center gap-3 rounded-xl border border-slate-200 p-4">
          <QRCodeSVG
            value={clave.data.uriAutenticador}
            size={192}
            marginSize={2}
            title="Código QR para agregar TIIE a tu app autenticadora"
          />
          <p className="text-center text-sm text-slate-700">
            Clave para escribirla a mano:
            <br />
            <code className="text-base font-semibold tracking-wider text-slate-900">
              {clave.data.claveCompartida}
            </code>
          </p>
        </div>
      )}

      <form onSubmit={enviar} noValidate className="flex flex-col gap-4">
        <RegionAnuncios>
          {errorGeneral && <Alerta tono="error">{mensajeDeError(activar.error)}</Alerta>}
        </RegionAnuncios>
        <Campo
          etiqueta="Código de 6 dígitos"
          inputMode="numeric"
          autoComplete="one-time-code"
          maxLength={7}
          error={errors.codigo?.message}
          {...formulario.register('codigo')}
        />
        <Boton type="submit" ocupado={activar.isPending} textoOcupado="Verificando…" disabled={!clave.data}>
          Activar
        </Boton>
      </form>
      <Boton variante="enlace" onClick={() => salir.mutate()} className="self-start px-0">
        Salir
      </Boton>
    </div>
  );
}

function PasoCodigosRecuperacion({ codigos }: { codigos: string[] }) {
  const cliente = useQueryClient();
  const navegar = useNavigate();
  const [copiados, setCopiados] = useState(false);

  const continuar = () => {
    // La sesión ya trae mfa = true: la guarda la vuelve a consultar.
    cliente.removeQueries({ queryKey: claves.admin });
    void navegar('/admin/agenda', { replace: true });
  };

  const copiar = async () => {
    try {
      await navigator.clipboard.writeText(codigos.join('\n'));
      setCopiados(true);
    } catch {
      setCopiados(false);
    }
  };

  return (
    <div className="flex flex-col gap-4">
      <EncabezadoPaso>Guarda tus códigos de recuperación</EncabezadoPaso>
      <Alerta tono="exito">La verificación en dos pasos quedó activa.</Alerta>
      <p className="text-slate-700">
        Si pierdes tu celular, cada uno de estos códigos te deja entrar una sola vez. Guárdalos en un lugar
        seguro: no los volveremos a mostrar.
      </p>
      <ol aria-label="Códigos de recuperación" className="grid grid-cols-2 gap-2 rounded-xl bg-slate-100 p-4">
        {codigos.map((codigo) => (
          <li key={codigo} className="font-mono text-base tracking-wider">
            {codigo}
          </li>
        ))}
      </ol>
      <RegionAnuncios>
        {copiados && <p className="text-sm text-green-800">Códigos copiados.</p>}
      </RegionAnuncios>
      <Boton variante="secundario" onClick={() => void copiar()}>
        Copiar códigos
      </Boton>
      <Boton onClick={continuar}>Ya los guardé, continuar</Boton>
    </div>
  );
}
