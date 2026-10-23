import { screen, within } from '@testing-library/react';
import { admin, citaAgenda } from '@/test/admin';
import { api, sesionPersonal } from '@/test/api';
import { renderizarRuta } from '@/test/renderizar';
import { servidor } from '@/test/servidor';

describe('Agenda del día', () => {
  beforeEach(() => {
    // Solo el reloj: los temporizadores reales siguen funcionando para MSW y user-event.
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(Date.UTC(2026, 10, 3, 17, 0)); // 3 nov, 10:00 en Sinaloa
  });
  afterEach(() => vi.useRealTimers());

  it('marca una cita como atendida y refresca la lista', async () => {
    let estado: 'Agendada' | 'Atendida' = 'Agendada';
    let consulta: { moduloId: string; fecha: string } | undefined;
    let asistio: boolean | undefined;
    servidor.use(
      api.yo(() => sesionPersonal()),
      admin.agenda((q) => {
        consulta = q;
        return [
          citaAgenda({ estado }),
          citaAgenda({
            citaId: 5013,
            folio: '261103-1000043',
            hora: '11:00',
            estado: 'NoAsistio',
            origen: 'Ventanilla',
          }),
        ];
      }),
      admin.asistencia((citaId, cuerpo) => {
        expect(citaId).toBe(5012);
        asistio = cuerpo.asistio;
        estado = 'Atendida';
        return { estado: 'Atendida' };
      }),
    );
    const { usuario } = renderizarRuta('/admin/agenda');

    const boton = await screen.findByRole('button', { name: 'Atendida: 261103-1000042' });
    expect(consulta).toEqual({ moduloId: '3', fecha: '2026-11-03' });
    expect(screen.getByText('martes 3 de noviembre de 2026')).toBeInTheDocument();
    // Solo las citas Agendada se marcan.
    expect(screen.queryByRole('button', { name: 'Atendida: 261103-1000043' })).not.toBeInTheDocument();
    // Los enumerados se muestran en español.
    const otra = within(screen.getByRole('row', { name: /261103-1000043/ }));
    expect(otra.getByText('No asistió')).toBeInTheDocument();
    expect(otra.getByText('Ventanilla')).toBeInTheDocument();

    await usuario.click(boton);

    expect(await screen.findByText('Cita 261103-1000042: Atendida.')).toBeInTheDocument();
    expect(asistio).toBe(true);
    expect(screen.queryByRole('button', { name: 'Atendida: 261103-1000042' })).not.toBeInTheDocument();
  });

  it('no ofrece marcar asistencia en un día futuro', async () => {
    servidor.use(
      api.yo(() => sesionPersonal()),
      admin.agenda(() => [citaAgenda()]),
    );
    renderizarRuta('/admin/agenda?fecha=2026-11-04');

    expect(await screen.findByText('261103-1000042')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Atendida/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /No asistió/ })).not.toBeInTheDocument();
  });

  it('muestra cómo va el día: por atender, la siguiente cita y el avance de la asistencia', async () => {
    servidor.use(
      api.yo(() => sesionPersonal()),
      admin.agenda(() => [
        citaAgenda({ citaId: 1, folio: '261103-1000001', hora: '08:00', estado: 'Atendida' }),
        citaAgenda({ citaId: 2, folio: '261103-1000002', hora: '09:00', estado: 'NoAsistio' }),
        citaAgenda({ citaId: 3, folio: '261103-1000003', hora: '09:30' }),
        citaAgenda({ citaId: 4, folio: '261103-1000004', hora: '10:30' }),
        citaAgenda({ citaId: 5, folio: '261103-1000005', hora: '11:00', estado: 'Cancelada' }),
      ]),
    );
    renderizarRuta('/admin/agenda');

    const resumen = await screen.findByRole('list', { name: 'Resumen del día' });
    const porAtender = within(resumen).getByText('Por atender').closest('li') as HTMLElement;
    expect(within(porAtender).getByText('2')).toBeInTheDocument();
    expect(within(porAtender).getByText('1 ya pasó su hora')).toBeInTheDocument();
    expect(within(resumen).getByText('Sin contar 1 cancelada')).toBeInTheDocument();
    expect(screen.getByText('Asistencia registrada: 2 de 4')).toBeInTheDocument();
    expect(
      within(screen.getByRole('row', { name: /261103-1000004/ })).getByText('Siguiente'),
    ).toBeInTheDocument();
    expect(
      within(screen.getByRole('row', { name: /261103-1000003/ })).getByText('Pasó su hora'),
    ).toBeInTheDocument();
  });

  it('filtra por estado y busca por nombre, folio o CURP', async () => {
    servidor.use(
      api.yo(() => sesionPersonal()),
      admin.agenda(() => [
        citaAgenda({ citaId: 1, folio: '261103-1000001', hora: '08:00', estado: 'Atendida' }),
        citaAgenda({ citaId: 2, folio: '261103-1000002', hora: '09:00', nombreCompleto: 'Luis Pérez Soto' }),
      ]),
    );
    const { usuario } = renderizarRuta('/admin/agenda');

    await usuario.click(await screen.findByRole('radio', { name: /Atendidas/ }));
    expect(screen.getByRole('row', { name: /261103-1000001/ })).toBeInTheDocument();
    expect(screen.queryByRole('row', { name: /261103-1000002/ })).not.toBeInTheDocument();

    await usuario.click(screen.getByRole('radio', { name: /Todas/ }));
    await usuario.type(screen.getByRole('searchbox', { name: 'Buscar en la agenda' }), 'perez');
    expect(screen.getByRole('row', { name: /261103-1000002/ })).toBeInTheDocument();
    expect(screen.queryByRole('row', { name: /261103-1000001/ })).not.toBeInTheDocument();

    await usuario.type(screen.getByRole('searchbox', { name: 'Buscar en la agenda' }), 'xyz');
    expect(screen.getByText('Ninguna cita coincide con el filtro.')).toBeInTheDocument();
  });

  it('cambia de día con las flechas y vuelve a hoy', async () => {
    servidor.use(
      api.yo(() => sesionPersonal()),
      admin.agenda(() => []),
    );
    const { router, usuario } = renderizarRuta('/admin/agenda');

    expect(await screen.findByRole('button', { name: 'Hoy' })).toBeDisabled();
    await usuario.click(screen.getByRole('button', { name: 'Día siguiente' }));
    expect(await screen.findByText('miércoles 4 de noviembre de 2026')).toBeInTheDocument();
    expect(router.state.location.search).toContain('fecha=2026-11-04');

    await usuario.click(screen.getByRole('button', { name: 'Hoy' }));
    expect(await screen.findByText('martes 3 de noviembre de 2026')).toBeInTheDocument();
    await usuario.click(screen.getByRole('button', { name: 'Día anterior' }));
    expect(router.state.location.search).toContain('fecha=2026-11-02');
  });
});
