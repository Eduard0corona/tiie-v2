import { screen, within } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { modulos, publico } from '@/test/publico';
import { renderizarRuta } from '@/test/renderizar';
import { servidor } from '@/test/servidor';

describe('Inicio', () => {
  it('muestra los trámites con su costo y los requisitos agrupados por nivel escolar', async () => {
    servidor.use(...publico.catalogo());
    renderizarRuta('/');

    const primeraVez = (await screen.findByRole('heading', { name: 'Primera vez' })).closest(
      'article',
    ) as HTMLElement;
    expect(within(primeraVez).getByText('Sin costo')).toBeInTheDocument();
    expect(within(primeraVez).getByText('Para todos')).toBeInTheDocument();
    expect(within(primeraVez).getByText('Medio superior y superior')).toBeInTheDocument();
    expect(within(primeraVez).getByText('CURP impresa.')).toBeInTheDocument();

    const sustitucion = screen
      .getByRole('heading', { name: 'Sustitución' })
      .closest('article') as HTMLElement;
    expect(within(sustitucion).getByText('$150.00')).toBeInTheDocument();
  });

  it('cada sede lleva al asistente con la sede ya elegida y a su mapa en una pestaña nueva', async () => {
    servidor.use(...publico.catalogo());
    renderizarRuta('/');

    const agendarAqui = await screen.findByRole('link', { name: 'Agendar aquí en Módulo Culiacán Centro' });
    expect(agendarAqui).toHaveAttribute('href', '/agendar?municipio=6&modulo=3');
    const sede = agendarAqui.closest('li') as HTMLElement;
    expect(within(sede).getByText('Culiacán')).toBeInTheDocument();
    const enlace = within(sede).getByRole('link', { name: /Cómo llegar a Módulo Culiacán Centro/ });
    expect(enlace).toHaveAttribute('target', '_blank');
    expect(enlace).toHaveAttribute('rel', 'noopener noreferrer');
  });

  it('la portada ofrece agendar y consultar una cita existente', async () => {
    servidor.use(...publico.catalogo());
    renderizarRuta('/');

    expect(
      await screen.findByRole('heading', {
        level: 1,
        name: 'Agenda tu cita para la Tarjeta Inteligente de Identidad Estudiantil',
      }),
    ).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Agendar cita' })).toHaveAttribute('href', '/agendar');
    expect(screen.getByRole('link', { name: 'Ya tengo una cita' })).toHaveAttribute('href', '/mi-cita');
    expect(screen.getByRole('heading', { name: 'Preguntas frecuentes' })).toBeInTheDocument();
  });

  it('con sedes en varios municipios filtra por municipio', async () => {
    // MSW usa el primer manejador que coincide: el de varios municipios va antes que el catálogo.
    servidor.use(
      http.get('/api/publico/modulos', () =>
        HttpResponse.json([
          ...modulos,
          ...modulos.map((m) => ({
            ...m,
            moduloId: 7,
            nombre: 'Módulo Mazatlán',
            municipioId: 12,
            municipio: 'Mazatlán',
          })),
        ]),
      ),
      ...publico.catalogo(),
    );
    const { usuario } = renderizarRuta('/');

    const filtros = await screen.findByRole('group', { name: 'Filtrar sedes por municipio' });
    expect(within(filtros).getByRole('button', { name: 'Todas' })).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByRole('heading', { name: 'Módulo Culiacán Centro' })).toBeInTheDocument();

    await usuario.click(within(filtros).getByRole('button', { name: 'Mazatlán' }));
    expect(within(filtros).getByRole('button', { name: 'Mazatlán' })).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByRole('heading', { name: 'Módulo Mazatlán' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Módulo Culiacán Centro' })).not.toBeInTheDocument();
  });
});
