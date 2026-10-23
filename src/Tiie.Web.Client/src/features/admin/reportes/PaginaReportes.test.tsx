import { screen } from '@testing-library/react';
import { admin } from '@/test/admin';
import { api, sesionPersonal } from '@/test/api';
import { renderizarRuta } from '@/test/renderizar';
import { servidor } from '@/test/servidor';

describe('Reportes', () => {
  it('arma las descargas con los mismos filtros que la tabla y avisa si se truncó', async () => {
    const consultas: string[] = [];
    servidor.use(
      api.yo(() => sesionPersonal({ rol: 'Supervisor' })),
      admin.modulos(),
      admin.tiposTramite(),
      admin.reporte((parametros) => {
        consultas.push(parametros.toString());
        return {
          filtro: {
            desde: '2026-10-01',
            hasta: '2026-10-31',
            moduloId: 3,
            tipoTramiteId: null,
            estado: 'Atendida',
          },
          filas: [
            {
              folio: '261003-1000001',
              fecha: '2026-10-03',
              hora: '09:00',
              modulo: 'Módulo Culiacán Centro',
              tramite: 'Primera vez',
              estado: 'Atendida',
              origen: 'Ventanilla',
            },
          ],
          totales: [{ estado: 'Atendida', citas: 1 }],
          truncado: true,
        };
      }),
    );
    const { usuario } = renderizarRuta(
      '/admin/reportes?desde=2026-10-01&hasta=2026-10-31&moduloId=3&estado=Atendida',
    );

    expect(await screen.findByText('261003-1000001')).toBeInTheDocument();
    expect(screen.getByText('El reporte llegó al límite de filas.')).toBeInTheDocument();
    expect(consultas[0]).toBe('desde=2026-10-01&hasta=2026-10-31&moduloId=3&estado=Atendida');
    expect(screen.getByRole('link', { name: 'Descargar Excel' })).toHaveAttribute(
      'href',
      '/api/admin/reportes/citas?desde=2026-10-01&hasta=2026-10-31&moduloId=3&estado=Atendida&formato=xlsx',
    );
    expect(screen.getByRole('link', { name: 'Descargar PDF' })).toHaveAttribute(
      'href',
      '/api/admin/reportes/citas?desde=2026-10-01&hasta=2026-10-31&moduloId=3&estado=Atendida&formato=pdf',
    );

    await screen.findByRole('option', { name: 'Primera vez' });
    await usuario.selectOptions(screen.getByLabelText('Trámite'), 'Primera vez');
    await usuario.click(screen.getByRole('button', { name: 'Generar' }));

    expect(await screen.findByRole('link', { name: 'Descargar Excel' })).toHaveAttribute(
      'href',
      '/api/admin/reportes/citas?desde=2026-10-01&hasta=2026-10-31&moduloId=3&tipoTramiteId=1&estado=Atendida&formato=xlsx',
    );
  });
});
