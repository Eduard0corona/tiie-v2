import { screen } from '@testing-library/react';
import { detalleMiCita, publico } from '@/test/publico';
import { renderizarRuta } from '@/test/renderizar';
import { servidor } from '@/test/servidor';

vi.mock('@/components/Captcha', () => import('@/test/CaptchaSimulado'));

const aviso = { texto: 'El lunes 9 no habrá servicio en ningún módulo.', tono: 'Importante' } as const;

describe('Aviso del sitio', () => {
  it.each([
    ['la portada', '/'],
    ['el asistente para agendar', '/agendar'],
    ['Mi cita', '/mi-cita'],
  ])('aparece una sola vez en %s', async (_, ruta) => {
    servidor.use(
      publico.portadaConAviso(aviso),
      publico.miCita(() => detalleMiCita()),
      ...publico.catalogo(),
    );
    renderizarRuta(ruta);

    expect(await screen.findByRole('complementary', { name: 'Aviso importante' })).toHaveTextContent(
      aviso.texto,
    );
    expect(screen.getAllByRole('complementary', { name: 'Aviso importante' })).toHaveLength(1);
  });

  it('va dentro del contenido principal, para que «Ir al contenido» no lo salte', async () => {
    servidor.use(publico.portadaConAviso(aviso), ...publico.catalogo());
    renderizarRuta('/agendar');

    const franja = await screen.findByRole('complementary', { name: 'Aviso importante' });
    expect(screen.getByRole('main')).toContainElement(franja);
  });

  it('no aparece en otras páginas, como recuperar el enlace', async () => {
    servidor.use(publico.portadaConAviso(aviso), ...publico.catalogo());
    renderizarRuta('/recuperar');

    expect(await screen.findByRole('heading', { level: 1 })).toBeInTheDocument();
    expect(screen.queryByRole('complementary', { name: 'Aviso importante' })).not.toBeInTheDocument();
  });
});
