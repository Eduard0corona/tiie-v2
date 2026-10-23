import { screen, within } from '@testing-library/react';
import type { components } from '@/api/esquema';
import { admin, ofertaPrimeraVez } from '@/test/admin';
import { api, sesionPersonal } from '@/test/api';
import { renderizarRuta } from '@/test/renderizar';
import { servidor } from '@/test/servidor';

type PlantillaSemanal = components['schemas']['PlantillaSemanal'];

describe('Plantilla semanal', () => {
  it('agrega una hora, copia el lunes al martes, guarda y muestra el resumen', async () => {
    let enviada: PlantillaSemanal | undefined;
    servidor.use(
      api.yo(() => sesionPersonal({ rol: 'Supervisor' })),
      admin.oferta(ofertaPrimeraVez),
      admin.plantilla({ dias: [{ diaSemana: 1, horas: [{ hora: '08:00', turnos: 4 }] }] }),
      admin.cupos(() => []),
      admin.guardarPlantilla((cuerpo) => {
        enviada = cuerpo;
        return { cuposCreados: 120, cuposActualizados: 36, cuposEliminados: 12, cuposConCitasSinCambio: 3 };
      }),
    );
    const { usuario } = renderizarRuta('/admin/ofertas/12');

    await usuario.click(await screen.findByRole('button', { name: 'Agregar hora al lunes' }));
    expect(screen.getByLabelText('Hora 2 del lunes')).toHaveValue('08:30');
    expect(screen.getByLabelText('Turnos de la hora 2 del lunes')).toHaveValue(4);

    await usuario.click(screen.getByRole('button', { name: 'Copiar el lunes a otros días' }));
    const dialogo = screen.getByRole('dialog', { name: 'Copiar el lunes a…' });
    await usuario.click(within(dialogo).getByLabelText('Martes'));
    await usuario.click(within(dialogo).getByRole('button', { name: 'Copiar' }));
    expect(screen.getByLabelText('Hora 2 del martes')).toHaveValue('08:30');

    await usuario.click(screen.getByRole('button', { name: 'Guardar plantilla' }));

    expect(await screen.findByText('La plantilla se guardó y los cupos se regeneraron.')).toBeInTheDocument();
    expect(screen.getByText('Cupos creados: 120')).toBeInTheDocument();
    expect(screen.getByText('Cupos con citas que no cambiaron: 3')).toBeInTheDocument();
    const horas = [
      { hora: '08:00', turnos: 4 },
      { hora: '08:30', turnos: 4 },
    ];
    expect(enviada).toEqual({
      dias: [
        { diaSemana: 1, horas },
        { diaSemana: 2, horas },
      ],
    });
  });

  it('una capturista ve la plantilla sin poder editarla', async () => {
    servidor.use(
      api.yo(() => sesionPersonal()),
      admin.oferta(ofertaPrimeraVez),
      admin.plantilla({ dias: [{ diaSemana: 1, horas: [{ hora: '08:00', turnos: 4 }] }] }),
      admin.cupos(() => []),
    );
    renderizarRuta('/admin/ofertas/12');

    expect(await screen.findByLabelText('Hora 1 del lunes')).toBeDisabled();
    expect(screen.queryByRole('button', { name: 'Guardar plantilla' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Agregar hora al lunes' })).not.toBeInTheDocument();
  });
});
