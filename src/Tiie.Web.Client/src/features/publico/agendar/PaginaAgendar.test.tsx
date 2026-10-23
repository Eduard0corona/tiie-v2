import { screen, within } from '@testing-library/react';
import { captchaSimulado } from '@/test/CaptchaSimulado';
import { problema } from '@/test/api';
import { CURP_PRUEBA, citaReservada, detalleMiCita, publico, type SolicitudRegistrada } from '@/test/publico';
import type { components } from '@/api/esquema';
import { renderizarRuta } from '@/test/renderizar';
import { servidor } from '@/test/servidor';

vi.mock('@/components/Captcha', () => import('@/test/CaptchaSimulado'));

type Usuario = ReturnType<typeof renderizarRuta>['usuario'];
type Solicitud = SolicitudRegistrada<components['schemas']['SolicitudCita']>;

async function llegarADatos(usuario: Usuario) {
  await usuario.click(await screen.findByLabelText('Culiacán'));
  await usuario.click(await screen.findByLabelText(/Módulo Culiacán Centro/));
  await usuario.click(await screen.findByLabelText(/Primera vez/));
  await usuario.click(screen.getByRole('button', { name: 'Continuar' }));

  expect(await screen.findByRole('heading', { name: 'Elige el día' })).toHaveFocus();
  expect(screen.getByRole('button', { name: 'martes 10 de noviembre, sin lugares' })).toBeDisabled();
  expect(screen.getByRole('button', { name: 'lunes 16 de noviembre, cerrado: Día inhábil' })).toBeDisabled();
  await usuario.click(screen.getByRole('button', { name: 'jueves 12 de noviembre, alta disponibilidad' }));

  expect(await screen.findByRole('heading', { name: 'Elige la hora' })).toHaveFocus();
  await usuario.click(await screen.findByLabelText(/10:30/));
  await usuario.click(screen.getByRole('button', { name: 'Continuar' }));
  expect(await screen.findByRole('heading', { name: 'Tus datos' })).toHaveFocus();
}

async function llenarDatos(usuario: Usuario) {
  await usuario.type(screen.getByLabelText('CURP'), 'goma080515mslrrna5');
  await usuario.type(screen.getByLabelText('Nombre o nombres'), 'Ana');
  await usuario.type(screen.getByLabelText('Primer apellido'), 'Gómez');
  await usuario.type(screen.getByLabelText('Celular'), '6671234567');
  await usuario.type(screen.getByLabelText('Correo electrónico'), 'ana@example.com');
  await usuario.click(screen.getByLabelText('Leí y acepto el aviso de privacidad'));
  await usuario.click(screen.getByRole('button', { name: 'Simular captcha' }));
}

beforeEach(() => {
  servidor.use(...publico.catalogo(), publico.disponibilidad(), publico.cupos());
  captchaSimulado.reinicios = 0;
});

describe('Asistente de reserva', () => {
  it('completa los cuatro pasos, crea la cita con Idempotency-Key y abre Mi cita', async () => {
    const recibidas: Solicitud[] = [];
    servidor.use(
      publico.crearCita((s) => {
        recibidas.push(s);
        return citaReservada;
      }),
      publico.miCita(() => detalleMiCita()),
    );
    const { router, usuario } = renderizarRuta('/agendar');

    await llegarADatos(usuario);
    expect(router.state.location.search).toBe(
      '?municipio=6&modulo=3&oferta=12&paso=datos&fecha=2026-11-12&cupo=81234',
    );
    await llenarDatos(usuario);
    await usuario.click(screen.getByRole('button', { name: 'Agendar cita' }));

    expect(await screen.findByText('Tu cita quedó agendada')).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/mi-cita');
    expect(router.state.location.search).toBe('?nueva=1');
    expect(recibidas).toHaveLength(1);
    expect(recibidas[0]?.idempotencia).toMatch(/^[0-9a-f-]{36}$/);
    expect(recibidas[0]?.cuerpo).toEqual({
      cupoId: 81234,
      curp: CURP_PRUEBA,
      nombre: 'Ana',
      apellidoPaterno: 'Gómez',
      apellidoMaterno: null,
      celular: '6671234567',
      correo: 'ana@example.com',
      aceptaAvisoPrivacidad: true,
      tokenCaptcha: 'token-prueba',
    });
  });

  it('con SinCupo regresa a la hora con el aviso y sin el horario lleno', async () => {
    servidor.use(publico.crearCita(() => problema(409, 'SinCupo')));
    const { router, usuario } = renderizarRuta('/agendar');

    await llegarADatos(usuario);
    await llenarDatos(usuario);
    await usuario.click(screen.getByRole('button', { name: 'Agendar cita' }));

    expect(await screen.findByRole('heading', { name: 'Elige la hora' })).toBeInTheDocument();
    expect(screen.getByText('Ese horario se acaba de llenar. Elige otro.')).toBeInTheDocument();
    expect(router.state.location.search).not.toContain('cupo=');
  });

  it('con YaTieneCita ofrece recibir el enlace y lleva la CURP sin ponerla en la URL', async () => {
    servidor.use(publico.crearCita(() => problema(409, 'YaTieneCita')));
    const { router, usuario } = renderizarRuta('/agendar');

    await llegarADatos(usuario);
    await llenarDatos(usuario);
    await usuario.click(screen.getByRole('button', { name: 'Agendar cita' }));
    expect(await screen.findByText('Esta CURP ya tiene una cita agendada.')).toBeInTheDocument();
    await usuario.click(screen.getByRole('button', { name: 'Recibir mi enlace por correo' }));

    expect(await screen.findByRole('heading', { name: 'Recupera el enlace de tu cita' })).toBeInTheDocument();
    expect(screen.getByLabelText('CURP')).toHaveValue(CURP_PRUEBA);
    expect(router.state.location.pathname).toBe('/recuperar');
    expect(router.state.location.search).toBe('');
  });

  it('con CaptchaInvalido reinicia el captcha y reintenta con la misma clave', async () => {
    const recibidas: Solicitud[] = [];
    servidor.use(
      publico.crearCita((s) => {
        recibidas.push(s);
        return recibidas.length === 1 ? problema(422, 'CaptchaInvalido') : citaReservada;
      }),
      publico.miCita(() => detalleMiCita()),
    );
    const { usuario } = renderizarRuta('/agendar');

    await llegarADatos(usuario);
    await llenarDatos(usuario);
    await usuario.click(screen.getByRole('button', { name: 'Agendar cita' }));
    expect(await screen.findByText('Confirma que no eres un robot.')).toBeInTheDocument();
    expect(captchaSimulado.reinicios).toBe(1);

    // Sin un token nuevo no se envía.
    await usuario.click(screen.getByRole('button', { name: 'Agendar cita' }));
    expect(recibidas).toHaveLength(1);

    await usuario.click(screen.getByRole('button', { name: 'Simular captcha' }));
    await usuario.click(screen.getByRole('button', { name: 'Agendar cita' }));
    expect(await screen.findByText('Tu cita quedó agendada')).toBeInTheDocument();
    expect(recibidas).toHaveLength(2);
    expect(recibidas[1]?.idempotencia).toBe(recibidas[0]?.idempotencia);
  });

  it('valida el formato antes de enviar y marca los errores de la API por campo', async () => {
    servidor.use(
      publico.crearCita(() =>
        problema(422, 'Validacion', { errors: { correo: ['El correo no es válido.'] } }),
      ),
    );
    const { usuario } = renderizarRuta('/agendar');

    await llegarADatos(usuario);
    await usuario.type(screen.getByLabelText('CURP'), 'GOMA080515MSLRRNA6');
    await usuario.click(screen.getByRole('button', { name: 'Agendar cita' }));
    expect(
      await screen.findByText('La CURP no es válida. Revisa las 18 letras y números.'),
    ).toBeInTheDocument();
    expect(screen.getByLabelText('CURP')).toHaveAttribute('aria-invalid', 'true');

    await usuario.clear(screen.getByLabelText('CURP'));
    await llenarDatos(usuario);
    await usuario.click(screen.getByRole('button', { name: 'Agendar cita' }));
    const correo = screen.getByLabelText('Correo electrónico');
    expect(
      await within(correo.parentElement as HTMLElement).findByText('El correo no es válido.'),
    ).toBeInTheDocument();
  });

  it('sin una oferta elegida la URL de un paso posterior muestra el primer paso', async () => {
    renderizarRuta('/agendar?paso=datos&fecha=2026-11-12');
    expect(await screen.findByRole('heading', { name: '¿Dónde quieres tu cita?' })).toBeInTheDocument();
  });
});
