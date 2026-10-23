import { screen, within } from '@testing-library/react';
import type { components } from '@/api/esquema';
import { admin } from '@/test/admin';
import { api, sesionPersonal } from '@/test/api';
import { renderizarRuta } from '@/test/renderizar';
import { servidor } from '@/test/servidor';

type CrearBloqueo = components['schemas']['CrearBloqueo'];

describe('Bloqueos', () => {
  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(Date.UTC(2026, 10, 3, 17, 0)); // 3 nov, 10:00 en Sinaloa
  });
  afterEach(() => vi.useRealTimers());

  it('crea un bloqueo de módulo, muestra las citas afectadas y las cancela con aviso', async () => {
    let creado: CrearBloqueo | undefined;
    let cancelacion: { bloqueoId: number; motivo: string | null } | undefined;
    servidor.use(
      api.yo(() => sesionPersonal({ rol: 'Supervisor' })),
      admin.modulos(),
      admin.bloqueos(),
      admin.crearBloqueo((cuerpo) => {
        creado = cuerpo;
        return {
          bloqueoId: 7,
          cuposCerrados: 48,
          citasAfectadas: [{ citaId: 5012, folio: '261103-1000042', fecha: '2026-11-03', hora: '10:30' }],
        };
      }),
      admin.cancelarCitasBloqueo((bloqueoId, cuerpo) => {
        cancelacion = { bloqueoId, motivo: cuerpo.motivo };
        return { citasCanceladas: 1 };
      }),
    );
    const { usuario } = renderizarRuta('/admin/bloqueos');

    const formulario = await screen.findByRole('form', { name: 'Nuevo bloqueo' });
    // Solo el administrador crea bloqueos generales.
    expect(
      within(formulario).queryByRole('radio', { name: 'General (día inhábil)' }),
    ).not.toBeInTheDocument();
    expect(within(formulario).getByRole('radio', { name: 'Módulo' })).toBeChecked();
    await within(formulario).findByRole('option', { name: 'Módulo Culiacán Centro' });
    await usuario.selectOptions(
      within(formulario).getByRole('combobox', { name: 'Módulo' }),
      'Módulo Culiacán Centro',
    );
    await usuario.type(within(formulario).getByLabelText('Motivo'), 'Fumigación');
    await usuario.click(within(formulario).getByRole('button', { name: 'Crear bloqueo' }));

    expect(await screen.findByText('Se cerraron 48 cupos.')).toBeInTheDocument();
    expect(creado).toEqual({
      desde: '2026-11-03',
      hasta: '2026-11-03',
      moduloId: 3,
      ofertaId: null,
      motivo: 'Fumigación',
    });
    expect(screen.getByText('1 cita agendada queda en las fechas bloqueadas')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: '261103-1000042' })).toHaveAttribute('href', '/admin/citas/5012');

    await usuario.click(screen.getByRole('button', { name: 'Cancelar y avisar' }));
    const dialogo = screen.getByRole('dialog', { name: '¿Cancelar las citas afectadas?' });
    await usuario.click(within(dialogo).getByRole('button', { name: 'Cancelar y avisar' }));
    expect(within(dialogo).getByText('Escribe el motivo.')).toBeInTheDocument();
    expect(cancelacion).toBeUndefined();

    await usuario.type(
      within(dialogo).getByLabelText('Motivo para las personas'),
      'El módulo cierra por fumigación',
    );
    await usuario.click(within(dialogo).getByRole('button', { name: 'Cancelar y avisar' }));

    expect(
      await screen.findByText('Se canceló 1 cita y se avisó a la persona por correo.'),
    ).toBeInTheDocument();
    expect(cancelacion).toEqual({ bloqueoId: 7, motivo: 'El módulo cierra por fumigación' });
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });
});
