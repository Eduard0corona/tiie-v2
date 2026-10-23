import { screen, within } from '@testing-library/react';
import { api, problema, sesionPersonal } from '@/test/api';
import { renderizarRuta } from '@/test/renderizar';
import { servidor } from '@/test/servidor';

const clave = {
  claveCompartida: 'jbsw y3dp ehpk 3pxp',
  uriAutenticador: 'otpauth://totp/TIIE:ana%40example.com?secret=JBSWY3DPEHPK3PXP&issuer=TIIE&digits=6',
};
const codigos = Array.from({ length: 10 }, (_, i) => `AB${i}CD-EF${i}GH`);

describe('Configurar la verificación en dos pasos', () => {
  it('muestra la clave, activa con el código y enseña los 10 códigos de recuperación', async () => {
    let mfaActivo = false;
    let enviado: unknown = null;
    servidor.use(
      api.yo(() => sesionPersonal({ mfaActivo })),
      api.claveMfa(clave),
      api.activarMfa((cuerpo) => {
        enviado = cuerpo;
        mfaActivo = true;
        return { codigosRecuperacion: codigos };
      }),
    );
    const { router, usuario } = renderizarRuta('/admin/configurar-mfa');

    expect(await screen.findByText('jbsw y3dp ehpk 3pxp')).toBeInTheDocument();
    expect(screen.getByTitle('Código QR para agregar TIIE a tu app autenticadora')).toBeInTheDocument();

    await usuario.type(screen.getByLabelText('Código de 6 dígitos'), '654-321');
    await usuario.click(screen.getByRole('button', { name: 'Activar' }));

    const lista = await screen.findByRole('list', { name: 'Códigos de recuperación' });
    expect(within(lista).getAllByRole('listitem')).toHaveLength(10);
    expect(enviado).toEqual({ codigo: '654321' });

    await usuario.click(screen.getByRole('button', { name: 'Ya los guardé, continuar' }));
    expect(await screen.findByRole('heading', { name: 'Agenda del día' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/admin/agenda');
  });

  it('marca el campo si la API rechaza el código', async () => {
    servidor.use(
      api.yo(() => sesionPersonal({ mfaActivo: false })),
      api.claveMfa(clave),
      api.activarMfa(() =>
        problema(422, 'Validacion', {
          errors: { codigo: ['El código no es correcto. Revisa que la hora de tu celular esté bien.'] },
        }),
      ),
    );
    const { usuario } = renderizarRuta('/admin/configurar-mfa');

    await screen.findByText('jbsw y3dp ehpk 3pxp');
    await usuario.type(screen.getByLabelText('Código de 6 dígitos'), '000000');
    await usuario.click(screen.getByRole('button', { name: 'Activar' }));

    expect(
      await screen.findByText('El código no es correcto. Revisa que la hora de tu celular esté bien.'),
    ).toBeInTheDocument();
    expect(screen.getByLabelText('Código de 6 dígitos')).toHaveAttribute('aria-invalid', 'true');
  });

  it('con MFA ya activa lleva a la agenda', async () => {
    servidor.use(api.yo(() => sesionPersonal()));
    const { router } = renderizarRuta('/admin/configurar-mfa');

    expect(await screen.findByRole('heading', { name: 'Agenda del día' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/admin/agenda');
  });
});
