import { screen, waitFor } from '@testing-library/react';
import { api, problema, sesionPersonal } from '@/test/api';
import { renderizarRuta } from '@/test/renderizar';
import { servidor } from '@/test/servidor';

describe('Guarda del panel', () => {
  it('sin sesión (401) lleva a entrar con la ruta para volver', async () => {
    servidor.use(api.yo(() => problema(401, 'NoAutenticado')));
    const { router } = renderizarRuta('/admin/mi-cuenta');

    expect(await screen.findByRole('heading', { name: 'Entrar al panel' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/admin/entrar');
    expect(router.state.location.search).toBe('?volver=%2Fadmin%2Fmi-cuenta');
  });

  it('con una sesión sin MFA lleva a configurarla', async () => {
    servidor.use(
      api.yo(() => sesionPersonal({ mfaActivo: false })),
      api.claveMfa({
        claveCompartida: 'abcd efgh',
        uriAutenticador: 'otpauth://totp/TIIE:ana?secret=ABCDEFGH',
      }),
    );
    const { router } = renderizarRuta('/admin/agenda');

    expect(
      await screen.findByRole('heading', { name: 'Configura la verificación en dos pasos' }),
    ).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/admin/configurar-mfa');
  });

  it('/admin redirige a la agenda y muestra quién entró', async () => {
    servidor.use(api.yo(() => sesionPersonal()));
    const { router } = renderizarRuta('/admin');

    expect(await screen.findByRole('heading', { name: 'Agenda del día' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/admin/agenda');
    expect(screen.getByText('Ana Gómez Martínez')).toBeInTheDocument();
    expect(screen.getByText('Módulo Culiacán Centro')).toBeInTheDocument();
  });

  it('«Salir» cierra la sesión y vuelve a entrar', async () => {
    let sesionActiva = true;
    servidor.use(
      api.yo(() => (sesionActiva ? sesionPersonal() : problema(401, 'NoAutenticado'))),
      api.salir(),
    );
    const { router, usuario } = renderizarRuta('/admin/agenda');

    await screen.findByRole('heading', { name: 'Agenda del día' });
    sesionActiva = false;
    await usuario.click(screen.getByRole('button', { name: 'Salir' }));

    await waitFor(() => expect(router.state.location.pathname).toBe('/admin/entrar'));
    expect(await screen.findByRole('heading', { name: 'Entrar al panel' })).toBeInTheDocument();
  });
});
