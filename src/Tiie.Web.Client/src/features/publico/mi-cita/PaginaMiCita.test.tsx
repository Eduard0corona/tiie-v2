import { screen } from '@testing-library/react';
import { problema } from '@/test/api';
import { detalleMiCita, publico } from '@/test/publico';
import { renderizarRuta } from '@/test/renderizar';
import { servidor } from '@/test/servidor';

describe('Mi cita', () => {
  it('con nueva=1 confirma la cita y muestra sus datos enmascarados', async () => {
    servidor.use(publico.miCita(() => detalleMiCita()));
    renderizarRuta('/mi-cita?nueva=1');

    expect(await screen.findByText('Tu cita quedó agendada')).toBeInTheDocument();
    expect(screen.getByText('261103-1000042')).toBeInTheDocument();
    expect(screen.getByText('jueves 12 de noviembre de 2026')).toBeInTheDocument();
    expect(screen.getByText('CURP GOMA************A5')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Descargar comprobante (PDF)' })).toHaveAttribute(
      'href',
      '/api/mi-cita/comprobante.pdf',
    );
    expect(screen.getByRole('link', { name: /Cómo llegar a Módulo Culiacán Centro/ })).toHaveAttribute(
      'href',
      'https://www.google.com/maps/search/?api=1&query=24.8,-107.39',
    );
  });

  it('cancela tras confirmar en el diálogo y envía el motivo', async () => {
    let motivo: string | null | undefined;
    servidor.use(
      publico.miCita(() => detalleMiCita()),
      publico.cancelarMiCita((cuerpo) => {
        motivo = cuerpo.motivo;
        return detalleMiCita({ estado: 'Cancelada', puedeModificar: false });
      }),
    );
    const { usuario } = renderizarRuta('/mi-cita');

    await usuario.click(await screen.findByRole('button', { name: 'Cancelar cita' }));
    const dialogo = await screen.findByRole('dialog', { name: '¿Cancelar tu cita?' });
    expect(dialogo).toBeInTheDocument();
    await usuario.type(screen.getByLabelText('Motivo (opcional)'), 'Ya no puedo ir');
    await usuario.click(screen.getByRole('button', { name: 'Sí, cancelar mi cita' }));

    expect(
      await screen.findByText('Tu cita fue cancelada. Te enviamos un correo de confirmación.'),
    ).toBeInTheDocument();
    expect(motivo).toBe('Ya no puedo ir');
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(screen.getByText('Cancelada')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Cancelar cita' })).not.toBeInTheDocument();
  });

  it('el diálogo se cierra con Escape sin cancelar', async () => {
    servidor.use(publico.miCita(() => detalleMiCita()));
    const { usuario } = renderizarRuta('/mi-cita');

    await usuario.click(await screen.findByRole('button', { name: 'Cancelar cita' }));
    await screen.findByRole('dialog');
    await usuario.keyboard('{Escape}');

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Cancelar cita' })).toHaveFocus();
  });

  it('fuera de plazo no ofrece cancelar ni cambiar y explica hasta cuándo se podía', async () => {
    servidor.use(
      publico.miCita(() => detalleMiCita({ puedeModificar: false, modificableHasta: '2026-11-11' })),
    );
    renderizarRuta('/mi-cita');

    expect(
      await screen.findByText(
        /El plazo para cancelar o cambiar tu cita en línea terminó el miércoles 11 de noviembre de 2026/,
      ),
    ).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Cancelar cita' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Cambiar fecha u hora' })).not.toBeInTheDocument();
  });

  it('reprograma a otro día y hora de la misma oferta', async () => {
    let cupoEnviado = 0;
    servidor.use(
      publico.miCita(() => detalleMiCita()),
      publico.disponibilidad(),
      publico.cupos(),
      publico.reprogramarMiCita((cuerpo) => {
        cupoEnviado = cuerpo.cupoId;
        return detalleMiCita({ folio: '261105-1000050', fecha: '2026-11-13', hora: '11:00' });
      }),
    );
    const { usuario } = renderizarRuta('/mi-cita');

    await usuario.click(await screen.findByRole('button', { name: 'Cambiar fecha u hora' }));
    await usuario.click(
      await screen.findByRole('button', { name: 'viernes 13 de noviembre, alta disponibilidad' }),
    );
    await usuario.click(await screen.findByLabelText(/11:00/));
    await usuario.click(screen.getByRole('button', { name: 'Confirmar el cambio' }));

    expect(
      await screen.findByText('Tu cita cambió. Te enviamos un correo con el comprobante nuevo.'),
    ).toBeInTheDocument();
    expect(cupoEnviado).toBe(81235);
    expect(screen.getByText('261105-1000050')).toBeInTheDocument();
  });

  it('con error=enlace-invalido ofrece recuperar el enlace', async () => {
    renderizarRuta('/mi-cita?error=enlace-invalido');

    expect(await screen.findByRole('heading', { name: 'Este enlace ya no es válido' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Recuperar mi enlace' })).toHaveAttribute('href', '/recuperar');
  });

  it('sin cookie (401) muestra lo mismo que un enlace inválido', async () => {
    servidor.use(publico.miCita(() => problema(401, 'NoAutenticado')));
    renderizarRuta('/mi-cita');

    expect(await screen.findByRole('heading', { name: 'Este enlace ya no es válido' })).toBeInTheDocument();
  });
});
