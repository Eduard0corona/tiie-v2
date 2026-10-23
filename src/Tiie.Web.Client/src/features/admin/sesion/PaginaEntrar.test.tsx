import { screen, waitFor } from '@testing-library/react';
import { api, problema, sesionPersonal } from '@/test/api';
import { renderizarRuta } from '@/test/renderizar';
import { servidor } from '@/test/servidor';

async function escribirCredenciales(usuario: ReturnType<typeof renderizarRuta>['usuario']) {
  await usuario.type(await screen.findByLabelText('Usuario'), 'capturista');
  await usuario.type(screen.getByLabelText('Contraseña'), 'una frase larga y segura');
  await usuario.click(screen.getByRole('button', { name: 'Entrar' }));
}

describe('Entrar al panel', () => {
  it('con CodigoMfa pide el código, lo envía sin espacios y abre la agenda', async () => {
    let enviado: unknown = null;
    servidor.use(
      api.entrar(() => ({ siguientePaso: 'CodigoMfa' })),
      api.entrarMfa((cuerpo) => {
        enviado = cuerpo;
        return null;
      }),
      api.yo(() => sesionPersonal()),
    );
    const { router, usuario } = renderizarRuta('/admin/entrar');

    await escribirCredenciales(usuario);
    expect(await screen.findByRole('heading', { name: 'Verificación en dos pasos' })).toHaveFocus();

    await usuario.type(screen.getByLabelText('Código de 6 dígitos'), '123 456');
    await usuario.click(screen.getByLabelText('Recordar este equipo por 14 días'));
    await usuario.click(screen.getByRole('button', { name: 'Verificar' }));

    expect(await screen.findByRole('heading', { name: 'Agenda del día' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/admin/agenda');
    expect(enviado).toEqual({ codigo: '123456', recordarEsteEquipo: true });
  });

  it('con CredencialesInvalidas avisa y no avanza', async () => {
    servidor.use(api.entrar(() => problema(401, 'CredencialesInvalidas')));
    const { router, usuario } = renderizarRuta('/admin/entrar');

    await escribirCredenciales(usuario);

    expect(await screen.findByText('Usuario o contraseña incorrectos.')).toBeInTheDocument();
    expect(screen.getByLabelText('Contraseña')).toHaveValue('');
    expect(router.state.location.pathname).toBe('/admin/entrar');
  });

  it('con CuentaBloqueada indica a qué hora de Sinaloa se desbloquea', async () => {
    servidor.use(
      api.entrar(() => problema(423, 'CuentaBloqueada', { desbloqueoEn: '2026-11-03T17:20:00Z' })),
    );
    const { usuario } = renderizarRuta('/admin/entrar');

    await escribirCredenciales(usuario);

    expect(
      await screen.findByText(
        'La cuenta está bloqueada por demasiados intentos. Podrás intentar de nuevo a las 10:20.',
      ),
    ).toBeInTheDocument();
  });

  it('con ConfigurarMfa lleva a configurar la verificación', async () => {
    servidor.use(
      api.entrar(() => ({ siguientePaso: 'ConfigurarMfa' })),
      api.yo(() => sesionPersonal({ mfaActivo: false })),
      api.claveMfa({
        claveCompartida: 'abcd efgh',
        uriAutenticador: 'otpauth://totp/TIIE:ana?secret=ABCDEFGH',
      }),
    );
    const { router, usuario } = renderizarRuta('/admin/entrar');

    await escribirCredenciales(usuario);

    expect(
      await screen.findByRole('heading', { name: 'Configura la verificación en dos pasos' }),
    ).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/admin/configurar-mfa');
  });

  it('vuelve a la ruta interna pedida y no a una externa', async () => {
    servidor.use(
      api.entrar(() => ({ siguientePaso: 'Listo' })),
      api.yo(() => sesionPersonal()),
    );
    const { router, usuario } = renderizarRuta('/admin/entrar?volver=%2Fadmin%2Fmi-cuenta');

    await escribirCredenciales(usuario);

    await waitFor(() => expect(router.state.location.pathname).toBe('/admin/mi-cuenta'));
  });

  it('valida el formato antes de llamar a la API', async () => {
    const { usuario } = renderizarRuta('/admin/entrar');
    await usuario.click(await screen.findByRole('button', { name: 'Entrar' }));

    const campo = screen.getByLabelText('Usuario');
    expect(await screen.findByText('Escribe tu usuario.')).toBeInTheDocument();
    expect(campo).toHaveAttribute('aria-invalid', 'true');
    expect(campo.getAttribute('aria-describedby')).toContain('-error');
  });

  it('«Olvidé mi contraseña» responde lo mismo exista o no el correo', async () => {
    servidor.use(api.olvide());
    const { usuario } = renderizarRuta('/admin/entrar');

    await usuario.click(await screen.findByRole('button', { name: '¿Olvidaste tu contraseña?' }));
    await usuario.type(screen.getByLabelText('Correo'), 'nadie@example.com');
    await usuario.click(screen.getByRole('button', { name: 'Enviar enlace' }));

    expect(await screen.findByRole('heading', { name: 'Revisa tu correo' })).toBeInTheDocument();
    expect(screen.getByText(/Si el correo está registrado/)).toBeInTheDocument();
  });
});
