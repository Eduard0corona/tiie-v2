import { screen } from '@testing-library/react';
import type { components } from '@/api/esquema';
import { admin } from '@/test/admin';
import { api, problema, sesionPersonal } from '@/test/api';
import { renderizarRuta } from '@/test/renderizar';
import { servidor } from '@/test/servidor';

type CrearUsuario = components['schemas']['CrearUsuario'];

describe('Alta de usuarios', () => {
  it('con 503 CorreoNoDisponible explica que la cuenta se creó y cómo reenviar la invitación', async () => {
    let cuerpo: CrearUsuario | undefined;
    servidor.use(
      api.yo(() => sesionPersonal({ rol: 'Administrador' })),
      admin.catalogos(),
      admin.modulos(),
      admin.crearUsuario((c) => {
        cuerpo = c;
        return problema(503, 'CorreoNoDisponible', { usuarioId: 41 });
      }),
    );
    const { usuario } = renderizarRuta('/admin/usuarios/nuevo');

    await usuario.type(await screen.findByLabelText('Usuario'), 'lperez');
    await usuario.type(screen.getByLabelText('Correo'), 'lperez@example.com');
    await usuario.type(screen.getByLabelText('Nombre o nombres'), 'Luis');
    await usuario.type(screen.getByLabelText('Primer apellido'), 'Pérez');
    await usuario.selectOptions(screen.getByLabelText('Rol'), 'Capturista');
    await usuario.click(screen.getByLabelText('Módulo Culiacán Centro'));
    await usuario.click(screen.getByRole('button', { name: 'Crear usuario y enviar invitación' }));

    expect(await screen.findByText('La cuenta se creó, pero la invitación no salió.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Abrir la cuenta lperez' })).toHaveAttribute(
      'href',
      '/admin/usuarios/41',
    );
    // No se puede volver a enviar: la cuenta ya existe.
    expect(
      screen.queryByRole('button', { name: 'Crear usuario y enviar invitación' }),
    ).not.toBeInTheDocument();
    expect(cuerpo).toEqual({
      usuario: 'lperez',
      correo: 'lperez@example.com',
      nombre: 'Luis',
      apellidoPaterno: 'Pérez',
      apellidoMaterno: null,
      rol: 'Capturista',
      moduloIds: [3],
    });
  });
});
