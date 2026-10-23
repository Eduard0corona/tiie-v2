import { screen } from '@testing-library/react';
import { api, problema } from '@/test/api';
import { renderizarRuta } from '@/test/renderizar';
import { servidor } from '@/test/servidor';

describe('Restablecer la contraseña con el enlace del correo', () => {
  it('sin usuario o token en el enlace avisa que no es válido', async () => {
    renderizarRuta('/admin/restablecer');
    expect(await screen.findByRole('heading', { name: 'Este enlace ya no es válido' })).toBeInTheDocument();
  });

  it('exige 12 caracteres y que coincidan', async () => {
    const { usuario } = renderizarRuta('/admin/restablecer?usuarioId=7&token=abc');

    await usuario.type(await screen.findByLabelText('Contraseña nueva'), 'corta');
    await usuario.type(screen.getByLabelText('Confirma la contraseña'), 'otra');
    await usuario.click(screen.getByRole('button', { name: 'Guardar contraseña' }));

    expect(await screen.findByText('Usa al menos 12 caracteres.')).toBeInTheDocument();
    expect(screen.getByText('Las contraseñas no coinciden.')).toBeInTheDocument();
  });

  it('con TokenInvalido ofrece pedir un enlace nuevo', async () => {
    servidor.use(api.restablecimiento(() => problema(422, 'TokenInvalido')));
    const { usuario } = renderizarRuta('/admin/restablecer?usuarioId=7&token=abc');

    await usuario.type(await screen.findByLabelText('Contraseña nueva'), 'una frase larga y segura');
    await usuario.type(screen.getByLabelText('Confirma la contraseña'), 'una frase larga y segura');
    await usuario.click(screen.getByRole('button', { name: 'Guardar contraseña' }));

    expect(await screen.findByRole('heading', { name: 'Este enlace ya no es válido' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Pide un enlace nuevo/ })).toHaveAttribute(
      'href',
      '/admin/entrar?olvide=1',
    );
  });

  it('al guardar invita a entrar', async () => {
    servidor.use(api.restablecimiento(() => null));
    const { usuario } = renderizarRuta('/admin/restablecer?usuarioId=7&token=abc');

    await usuario.type(await screen.findByLabelText('Contraseña nueva'), 'una frase larga y segura');
    await usuario.type(screen.getByLabelText('Confirma la contraseña'), 'una frase larga y segura');
    await usuario.click(screen.getByRole('button', { name: 'Guardar contraseña' }));

    expect(await screen.findByText('Tu contraseña quedó guardada. Ya puedes entrar.')).toBeInTheDocument();
  });
});
