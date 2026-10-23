import { screen } from '@testing-library/react';
import type { components } from '@/api/esquema';
import { admin, cupoAdmin } from '@/test/admin';
import { api, problema, sesionPersonal } from '@/test/api';
import { CURP_PRUEBA } from '@/test/publico';
import { renderizarRuta } from '@/test/renderizar';
import { servidor } from '@/test/servidor';

type CitaVentanilla = components['schemas']['CitaVentanilla'];

describe('Ventanilla', () => {
  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(Date.UTC(2026, 10, 3, 16, 0)); // 3 nov, 09:00 en Sinaloa
  });
  afterEach(() => vi.useRealTimers());

  function manejadoresBase() {
    return [
      api.yo(() => sesionPersonal()),
      admin.modulos(),
      admin.ofertas(),
      admin.cupos(({ desde, hasta }) => {
        expect([desde, hasta]).toEqual(['2026-11-03', '2026-11-03']);
        return [
          cupoAdmin({ cupoId: 899, hora: '08:30' }), // ya pasó
          cupoAdmin({ cupoId: 900, hora: '10:00', capacidad: 4, ocupados: 1 }),
          cupoAdmin({ cupoId: 901, hora: '11:00', capacidad: 4, ocupados: 4 }), // lleno
          cupoAdmin({ cupoId: 902, hora: '12:00', cerrado: true }),
        ];
      }),
    ];
  }

  it('registra una cita de hoy sin celular ni correo y muestra el folio', async () => {
    let cuerpo: CitaVentanilla | undefined;
    let clave: string | null = null;
    servidor.use(
      ...manejadoresBase(),
      admin.crearCita((c, k) => {
        cuerpo = c;
        clave = k;
        return { citaId: 5100, folio: '261103-1000099' };
      }),
    );
    const { usuario } = renderizarRuta('/admin/citas/nueva');

    await screen.findByRole('option', { name: 'Primera vez' });
    await usuario.selectOptions(screen.getByLabelText('Trámite'), 'Primera vez');
    const hora = await screen.findByRole('radio', { name: /10:00/ });
    expect(screen.getByText('3 lugares')).toBeInTheDocument();
    expect(screen.queryByRole('radio', { name: /08:30/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('radio', { name: /11:00/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('radio', { name: /12:00/ })).not.toBeInTheDocument();
    await usuario.click(hora);

    await usuario.type(screen.getByLabelText('CURP'), CURP_PRUEBA.toLowerCase());
    await usuario.type(screen.getByLabelText('Nombre o nombres'), 'Ana');
    await usuario.type(screen.getByLabelText('Primer apellido'), 'Gómez');
    await usuario.click(screen.getByRole('button', { name: 'Registrar cita' }));
    expect(
      await screen.findByText('Confirma que leíste el aviso de privacidad a la persona.'),
    ).toBeInTheDocument();
    expect(cuerpo).toBeUndefined();

    await usuario.click(screen.getByLabelText('Leí el aviso de privacidad simplificado a la persona'));
    await usuario.click(screen.getByRole('button', { name: 'Registrar cita' }));

    expect(await screen.findByRole('heading', { name: 'Cita registrada' })).toBeInTheDocument();
    expect(screen.getByText('Folio 261103-1000099')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Ver la cita' })).toHaveAttribute('href', '/admin/citas/5100');
    expect(screen.getByRole('link', { name: 'Descargar comprobante (PDF)' })).toHaveAttribute(
      'href',
      '/api/admin/citas/5100/comprobante.pdf',
    );
    expect(cuerpo).toEqual({
      cupoId: 900,
      curp: CURP_PRUEBA,
      nombre: 'Ana',
      apellidoPaterno: 'Gómez',
      apellidoMaterno: null,
      celular: null,
      correo: null,
      avisoLeido: true,
    });
    expect(clave).toMatch(/^[0-9a-f-]{36}$/);
  });

  it('con YaTieneCita ofrece buscar las citas de esa CURP', async () => {
    servidor.use(
      ...manejadoresBase(),
      admin.crearCita(() => problema(409, 'YaTieneCita')),
    );
    const { usuario } = renderizarRuta('/admin/citas/nueva');

    await screen.findByRole('option', { name: 'Primera vez' });
    await usuario.selectOptions(screen.getByLabelText('Trámite'), 'Primera vez');
    await usuario.click(await screen.findByRole('radio', { name: /10:00/ }));
    await usuario.type(screen.getByLabelText('CURP'), CURP_PRUEBA);
    await usuario.type(screen.getByLabelText('Nombre o nombres'), 'Ana');
    await usuario.type(screen.getByLabelText('Primer apellido'), 'Gómez');
    await usuario.click(screen.getByLabelText('Leí el aviso de privacidad simplificado a la persona'));
    await usuario.click(screen.getByRole('button', { name: 'Registrar cita' }));

    expect(await screen.findByText('Esta CURP ya tiene una cita agendada.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Ver las citas de esta CURP' })).toHaveAttribute(
      'href',
      `/admin/citas?busqueda=${CURP_PRUEBA}`,
    );
  });
});
