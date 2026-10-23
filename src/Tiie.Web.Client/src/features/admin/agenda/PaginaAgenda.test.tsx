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
});
