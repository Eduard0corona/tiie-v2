import { screen, within } from '@testing-library/react';
import { publico } from '@/test/publico';
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

  it('agrupa las sedes por municipio con un enlace al mapa en una pestaña nueva', async () => {
    servidor.use(...publico.catalogo());
    renderizarRuta('/');

    expect(await screen.findByRole('heading', { name: 'Culiacán' })).toBeInTheDocument();
    const enlace = screen.getByRole('link', { name: /Cómo llegar a Módulo Culiacán Centro/ });
    expect(enlace).toHaveAttribute('target', '_blank');
    expect(enlace).toHaveAttribute('rel', 'noopener noreferrer');
    expect(screen.getByRole('link', { name: 'Agendar cita' })).toHaveAttribute('href', '/agendar');
  });
});
