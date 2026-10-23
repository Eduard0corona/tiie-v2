import { screen } from '@testing-library/react';
import type { components } from '@/api/esquema';
import { CURP_PRUEBA, publico } from '@/test/publico';
import { renderizarRuta } from '@/test/renderizar';
import { servidor } from '@/test/servidor';
import { MENSAJE_RECUPERACION } from './PaginaRecuperar';

vi.mock('@/components/Captcha', () => import('@/test/CaptchaSimulado'));

type Solicitud = components['schemas']['SolicitudRecuperacion'];

async function pedirEnlace(curp: string, correo: string) {
  const recibidas: Solicitud[] = [];
  servidor.use(
    ...publico.catalogo(),
    publico.recuperacion((cuerpo) => recibidas.push(cuerpo)),
  );
  const { usuario } = renderizarRuta('/recuperar');
  await usuario.type(await screen.findByLabelText('CURP'), curp);
  await usuario.type(screen.getByLabelText('Correo electrónico'), correo);
  await usuario.click(await screen.findByRole('button', { name: 'Simular captcha' }));
  await usuario.click(screen.getByRole('button', { name: 'Enviarme el enlace' }));
  const mensaje = await screen.findByText(MENSAJE_RECUPERACION);
  return { recibidas, mensaje: mensaje.textContent };
}

describe('Recuperar el enlace', () => {
  it('envía la CURP normalizada, el correo y el captcha, y responde con el mensaje fijo', async () => {
    const { recibidas } = await pedirEnlace(' goma080515mslrrna5 ', 'ana@example.com');

    expect(recibidas).toEqual([
      { curp: CURP_PRUEBA, correo: 'ana@example.com', tokenCaptcha: 'token-prueba' },
    ]);
  });

  it('el mensaje es el mismo exista o no la cita', async () => {
    const conCita = await pedirEnlace(CURP_PRUEBA, 'ana@example.com');
    document.body.innerHTML = '';
    const sinCita = await pedirEnlace('PERJ950101HSLRRN04', 'nadie@example.com');

    expect(sinCita.mensaje).toBe(conCita.mensaje);
  });

  it('sin captcha no envía la solicitud', async () => {
    const recibidas: Solicitud[] = [];
    servidor.use(
      ...publico.catalogo(),
      publico.recuperacion((cuerpo) => recibidas.push(cuerpo)),
    );
    const { usuario } = renderizarRuta('/recuperar');
    await usuario.type(await screen.findByLabelText('CURP'), CURP_PRUEBA);
    await usuario.type(screen.getByLabelText('Correo electrónico'), 'ana@example.com');
    await usuario.click(screen.getByRole('button', { name: 'Enviarme el enlace' }));

    expect(await screen.findByText('Confirma que no eres un robot.')).toBeInTheDocument();
    expect(recibidas).toHaveLength(0);
  });
});
