import { screen } from '@testing-library/react';
import { admin, moduloDetalle } from '@/test/admin';
import { api, problema, sesionPersonal } from '@/test/api';
import { renderizarRuta } from '@/test/renderizar';
import { servidor } from '@/test/servidor';

describe('Edición de un módulo', () => {
  it('un 409 Conflicto ofrece recargar y muestra la versión nueva', async () => {
    let lecturas = 0;
    let versionEnviada: string | null | undefined;
    servidor.use(
      api.yo(() => sesionPersonal({ rol: 'Administrador' })),
      admin.catalogos(),
      admin.modulo(() => {
        lecturas += 1;
        return lecturas === 1
          ? moduloDetalle()
          : moduloDetalle({ horarioAtencion: 'Lunes a sábado de 8:00 a 14:00', version: 'AAAAAAAAB9F=' });
      }),
      admin.actualizarModulo((cuerpo) => {
        versionEnviada = cuerpo.version;
        return problema(409, 'Conflicto');
      }),
    );
    const { usuario } = renderizarRuta('/admin/modulos/3');

    const horario = await screen.findByLabelText('Horario de atención (opcional)');
    expect(horario).toHaveValue('Lunes a viernes de 8:00 a 15:00');
    await usuario.clear(horario);
    await usuario.type(horario, 'Lunes a viernes de 9:00 a 15:00');
    await usuario.click(screen.getByRole('button', { name: 'Guardar cambios' }));

    expect(
      await screen.findByText('Alguien más modificó este registro. Recarga la página para ver los cambios.'),
    ).toBeInTheDocument();
    expect(versionEnviada).toBe('AAAAAAAAB9E=');

    await usuario.click(screen.getByRole('button', { name: 'Recargar' }));

    expect(await screen.findByDisplayValue('Lunes a sábado de 8:00 a 14:00')).toBeInTheDocument();
    expect(screen.queryByText(/Alguien más modificó este registro/)).not.toBeInTheDocument();
  });

  it('una capturista ve los datos sin poder editarlos', async () => {
    servidor.use(
      api.yo(() => sesionPersonal()),
      admin.catalogos(),
      admin.modulo(() => moduloDetalle()),
    );
    renderizarRuta('/admin/modulos/3');

    expect(await screen.findByLabelText('Nombre')).toBeDisabled();
    expect(screen.getByLabelText('Municipio')).toHaveValue('6');
    expect(screen.queryByRole('button', { name: 'Guardar cambios' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Desactivar módulo' })).not.toBeInTheDocument();
  });
});
