import { screen, waitFor, within } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { admin, citaAgenda } from '@/test/admin';
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

  it('una dirección del panel que no existe muestra un 404 dentro del panel', async () => {
    servidor.use(api.yo(() => sesionPersonal()));
    renderizarRuta('/admin/no-existe');

    expect(await screen.findByRole('heading', { name: 'Página no encontrada' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Ir a la agenda' })).toHaveAttribute('href', '/admin/agenda');
  });

  it('/admin redirige a la agenda y muestra quién entró', async () => {
    servidor.use(
      api.yo(() => sesionPersonal()),
      admin.agenda(() => []),
    );
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
      admin.agenda(() => []),
    );
    const { router, usuario } = renderizarRuta('/admin/agenda');

    await screen.findByRole('heading', { name: 'Agenda del día' });
    sesionActiva = false;
    await usuario.click(screen.getByRole('button', { name: 'Salir' }));

    await waitFor(() => expect(router.state.location.pathname).toBe('/admin/entrar'));
    expect(await screen.findByRole('heading', { name: 'Entrar al panel' })).toBeInTheDocument();
  });

  it('el menú depende del rol: una capturista no ve Usuarios, Reportes ni Auditoría', async () => {
    servidor.use(
      api.yo(() => sesionPersonal()),
      admin.agenda(() => []),
    );
    renderizarRuta('/admin/agenda');

    const menu = await screen.findByRole('navigation', { name: 'Panel' });
    for (const texto of ['Agenda', 'Citas', 'Ventanilla', 'Tablero', 'Bloqueos', 'Mi cuenta']) {
      expect(within(menu).getByRole('link', { name: texto })).toBeInTheDocument();
    }
    for (const texto of ['Usuarios', 'Reportes', 'Auditoría']) {
      expect(within(menu).queryByRole('link', { name: texto })).not.toBeInTheDocument();
    }
  });

  it('el supervisor ve Reportes pero no Usuarios', async () => {
    servidor.use(
      api.yo(() => sesionPersonal({ rol: 'Supervisor' })),
      admin.agenda(() => []),
    );
    renderizarRuta('/admin/agenda');
    const menu = await screen.findByRole('navigation', { name: 'Panel' });
    expect(within(menu).getByRole('link', { name: 'Reportes' })).toHaveAttribute('href', '/admin/reportes');
    expect(within(menu).queryByRole('link', { name: 'Usuarios' })).not.toBeInTheDocument();
  });

  it('el administrador ve Usuarios y Auditoría', async () => {
    servidor.use(
      api.yo(() => sesionPersonal({ rol: 'Administrador' })),
      admin.agenda(() => []),
    );
    renderizarRuta('/admin/agenda');
    const menu = await screen.findByRole('navigation', { name: 'Panel' });
    expect(within(menu).getByRole('link', { name: 'Usuarios' })).toHaveAttribute('href', '/admin/usuarios');
    expect(within(menu).getByRole('link', { name: 'Auditoría' })).toBeInTheDocument();
    expect(within(menu).getByRole('link', { name: 'Reportes' })).toBeInTheDocument();
  });

  it('en celular, «Menú» abre y cierra la navegación', async () => {
    servidor.use(
      api.yo(() => sesionPersonal()),
      admin.agenda(() => []),
    );
    const { usuario } = renderizarRuta('/admin/agenda');

    const boton = await screen.findByRole('button', { name: 'Menú' });
    expect(boton).toHaveAttribute('aria-expanded', 'false');
    await usuario.click(boton);
    expect(boton).toHaveAttribute('aria-expanded', 'true');
    expect(boton).toHaveAttribute('aria-controls', 'menu-panel');
  });

  it('Escape cierra el menú en celular', async () => {
    servidor.use(
      api.yo(() => sesionPersonal()),
      admin.agenda(() => []),
    );
    const { usuario } = renderizarRuta('/admin/agenda');

    const boton = await screen.findByRole('button', { name: 'Menú' });
    await usuario.click(boton);
    expect(boton).toHaveAttribute('aria-expanded', 'true');
    await usuario.keyboard('{Escape}');
    expect(boton).toHaveAttribute('aria-expanded', 'false');
  });

  it('el menú agrupa por tarea y marca la pantalla actual', async () => {
    servidor.use(
      api.yo(() => sesionPersonal({ rol: 'Administrador' })),
      admin.agenda(() => []),
    );
    renderizarRuta('/admin/agenda');

    const menu = await screen.findByRole('navigation', { name: 'Panel' });
    const operacion = within(menu).getByRole('list', { name: 'Operación' });
    expect(within(operacion).getByRole('link', { name: 'Agenda' })).toHaveAttribute('aria-current', 'page');
    expect(within(operacion).getByRole('link', { name: 'Ventanilla' })).not.toHaveAttribute('aria-current');
    expect(within(menu).getByRole('list', { name: 'Configuración' })).toBeInTheDocument();
    expect(
      within(within(menu).getByRole('list', { name: 'Administración' })).getByRole('link', {
        name: 'Usuarios',
      }),
    ).toBeInTheDocument();
  });

  it('la búsqueda global lleva a Citas con el folio o la CURP ya aplicados', async () => {
    let busqueda: string | null = null;
    servidor.use(
      api.yo(() => sesionPersonal()),
      admin.agenda(() => []),
      admin.modulos(),
      admin.tiposTramite(),
      http.get('/api/admin/citas', ({ request }) => {
        busqueda = new URL(request.url).searchParams.get('busqueda');
        return HttpResponse.json({ elementos: [], total: 0, pagina: 1, tamano: 20 });
      }),
    );
    const { router, usuario } = renderizarRuta('/admin/agenda');

    await screen.findByRole('heading', { name: 'Agenda del día' });
    await usuario.type(
      screen.getByRole('searchbox', { name: 'Buscar cita por folio o CURP' }),
      ' 261103-1000042 {Enter}',
    );

    await waitFor(() => expect(router.state.location.pathname).toBe('/admin/citas'));
    expect(router.state.location.search).toBe('?busqueda=261103-1000042');
    expect(await screen.findByRole('heading', { name: 'Citas' })).toBeInTheDocument();
    await waitFor(() => expect(busqueda).toBe('261103-1000042'));
    expect(screen.getByLabelText('Folio o CURP')).toHaveValue('261103-1000042');
  });

  it('«/» enfoca la búsqueda global, salvo cuando ya se escribe en un campo', async () => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(Date.UTC(2026, 10, 3, 17, 0));
    try {
      servidor.use(
        api.yo(() => sesionPersonal()),
        admin.agenda(() => [citaAgenda()]),
      );
      const { usuario } = renderizarRuta('/admin/agenda');
      const global = await screen.findByRole('searchbox', { name: 'Buscar cita por folio o CURP' });
      const enAgenda = await screen.findByRole('searchbox', { name: 'Buscar en la agenda' });

      await usuario.click(enAgenda);
      await usuario.keyboard('/');
      expect(enAgenda).toHaveValue('/');
      expect(global).not.toHaveFocus();

      enAgenda.blur();
      await usuario.keyboard('/');
      expect(global).toHaveFocus();
      expect(global).toHaveValue('');
    } finally {
      vi.useRealTimers();
    }
  });
});
