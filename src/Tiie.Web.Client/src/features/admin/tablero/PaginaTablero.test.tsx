import { screen, within } from '@testing-library/react';
import { admin } from '@/test/admin';
import { api, sesionPersonal } from '@/test/api';
import { renderizarRuta } from '@/test/renderizar';
import { servidor } from '@/test/servidor';

describe('Tablero', () => {
  it('muestra el total de hoy, la ocupación con los días sin servicio y la inasistencia de mayor a menor', async () => {
    servidor.use(
      api.yo(() => sesionPersonal()),
      admin.modulos(),
      admin.tablero({
        hoy: [
          { estado: 'Agendada', citas: 4 },
          { estado: 'Atendida', citas: 3 },
          { estado: 'NoAsistio', citas: 1 },
          { estado: 'Cancelada', citas: 2 },
        ],
        proximos7Dias: [
          { fecha: '2026-11-04', capacidad: 100, ocupados: 80 },
          { fecha: '2026-11-08', capacidad: 0, ocupados: 0 },
        ],
        inasistencia30Dias: [
          { moduloId: 3, modulo: 'Módulo Culiacán Centro', citas: 800, inasistencias: 96, porcentaje: 12 },
          {
            moduloId: 4,
            modulo: 'Módulo Ciudad Universitaria',
            citas: 400,
            inasistencias: 72,
            porcentaje: 18,
          },
        ],
      }),
    );
    renderizarRuta('/admin/tablero');

    expect(await screen.findByText('10')).toBeInTheDocument();
    // Las canceladas no cuentan para la asistencia registrada.
    expect(screen.getByText('4 de 8 con asistencia registrada')).toBeInTheDocument();
    expect(screen.getByText('80 de 100 lugares ocupados en la semana (80 %).')).toBeInTheDocument();

    const ocupacion = screen.getByRole('table', { name: 'Ocupación de los próximos 7 días' });
    expect(within(ocupacion).getByRole('row', { name: /domingo 8 de noviembre/i })).toHaveTextContent(
      'Sin servicio',
    );
    expect(within(ocupacion).getByText('80 %')).toBeInTheDocument();

    const inasistencia = screen.getByRole('table', {
      name: 'Inasistencia de los últimos 30 días por módulo',
    });
    const filas = within(inasistencia).getAllByRole('row').slice(1);
    expect(filas[0]).toHaveTextContent('Módulo Ciudad Universitaria');
    expect(filas[1]).toHaveTextContent('Módulo Culiacán Centro');
  });
});
