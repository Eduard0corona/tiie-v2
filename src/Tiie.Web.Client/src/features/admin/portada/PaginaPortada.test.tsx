import { screen, within } from '@testing-library/react';
import type { components } from '@/api/esquema';
import { admin, portadaAdmin } from '@/test/admin';
import { api, problema, sesionPersonal } from '@/test/api';
import { renderizarRuta } from '@/test/renderizar';
import { servidor } from '@/test/servidor';

type DatosPortada = components['schemas']['DatosPortada'];

const sesionAdministrador = () => sesionPersonal({ rol: 'Administrador', usuario: 'admin', modulos: [] });

describe('Portada del sitio', () => {
  it('el administrador edita el encabezado, programa un aviso, reordena preguntas y guarda', async () => {
    let cuerpo: DatosPortada | undefined;
    servidor.use(
      api.yo(sesionAdministrador),
      admin.portada(() => portadaAdmin()),
      admin.guardarPortada((c) => {
        cuerpo = c;
        return portadaAdmin({
          titulo: c.titulo,
          avisoTexto: c.avisoTexto,
          avisoTono: c.avisoTono,
          avisoDesde: c.avisoDesde,
          estadoAviso: 'Programado',
          preguntas: c.preguntas,
          version: 'AAAAAAAAB9E=',
          modificadoEn: '2026-11-02T17:00:00Z',
        });
      }),
    );
    const { usuario } = renderizarRuta('/admin/portada');

    const titulo = await screen.findByRole('textbox', { name: 'Título' });
    await usuario.clear(titulo);
    await usuario.type(titulo, 'Agenda tu TIIE');
    await usuario.clear(screen.getByRole('textbox', { name: 'Continuación del título (opcional)' }));
    await usuario.type(
      screen.getByRole('textbox', { name: 'Texto del aviso (opcional)' }),
      'Cerrado el lunes.',
    );
    await usuario.click(screen.getByRole('radio', { name: /Importante/ }));
    await usuario.type(screen.getByLabelText('Mostrar desde (opcional)'), '2026-11-09');
    await usuario.click(screen.getByRole('button', { name: 'Bajar la pregunta 1' }));
    expect(screen.getByText('Hay cambios sin guardar.')).toBeInTheDocument();
    await usuario.click(screen.getByRole('button', { name: 'Guardar cambios' }));

    expect(await screen.findByText('Los cambios se guardaron y ya se ven en el sitio.')).toBeInTheDocument();
    expect(cuerpo).toEqual({
      etiqueta: 'Trámite presencial en los módulos de Sinaloa',
      titulo: 'Agenda tu TIIE',
      tituloDestacado: null,
      descripcion: 'Elige la sede, el día y la hora desde tu celular.',
      avisoTexto: 'Cerrado el lunes.',
      avisoTono: 'Importante',
      avisoDesde: '2026-11-09',
      avisoHasta: null,
      preguntas: [
        { pregunta: '¿Cuántas citas puedo tener?', respuesta: 'Una a la vez.' },
        { pregunta: '¿Necesito crear una cuenta?', respuesta: 'No.' },
      ],
      version: null,
    });
    expect(screen.getByText('Programado')).toBeInTheDocument();
    expect(screen.getByText(/Guardado el 02\/11\/2026 10:00/)).toBeInTheDocument();
  });

  it('la vista previa muestra el contenido sin guardar con los componentes del sitio', async () => {
    servidor.use(
      api.yo(sesionAdministrador),
      admin.portada(() => portadaAdmin()),
    );
    const { usuario } = renderizarRuta('/admin/portada');

    const descripcion = await screen.findByRole('textbox', { name: 'Descripción' });
    await usuario.clear(descripcion);
    await usuario.type(descripcion, 'Texto nuevo de prueba.');
    await usuario.type(
      screen.getByRole('textbox', { name: 'Texto del aviso (opcional)' }),
      // user-event lee «[» como tecla especial: se escribe doble.
      'Consulta [[las sedes](/#sedes).',
    );
    await usuario.click(screen.getByRole('button', { name: 'Vista previa' }));

    expect(screen.getByRole('button', { name: 'Vista previa' })).toHaveAttribute('aria-pressed', 'true');
    const vista = screen.getByRole('heading', { name: 'Vista previa' }).closest('section') as HTMLElement;
    // La vista previa es inerte: se revisa el contenido, no los roles.
    expect(vista).toHaveTextContent('Agenda tu cita para la Tarjeta Inteligente de Identidad Estudiantil');
    expect(vista).toHaveTextContent('Texto nuevo de prueba.');
    expect(vista).toHaveTextContent('Consulta las sedes.');
    expect(vista).toHaveTextContent('¿Cuántas citas puedo tener?');
    expect(screen.queryByRole('textbox', { name: 'Título' })).not.toBeInTheDocument();
  });

  it('valida el formato antes de enviar y muestra junto a cada campo los errores de la API', async () => {
    servidor.use(
      api.yo(sesionAdministrador),
      admin.portada(() => portadaAdmin()),
      admin.guardarPortada(() =>
        problema(422, 'Validacion', {
          errors: {
            'preguntas[1].respuesta': ['Los enlaces deben empezar con /, https://, mailto: o tel:.'],
          },
        }),
      ),
    );
    const { usuario } = renderizarRuta('/admin/portada');

    const titulo = await screen.findByRole('textbox', { name: 'Título' });
    await usuario.clear(titulo);
    await usuario.type(
      screen.getByRole('textbox', { name: 'Respuesta 1' }),
      ' Ver [[aquí](javascript:void0)',
    );
    await usuario.click(screen.getByRole('button', { name: 'Guardar cambios' }));

    expect(await screen.findByText('Escribe el título.')).toBeInTheDocument();
    expect(
      screen.getByText('Los enlaces deben empezar con /, https://, mailto: o tel:.'),
    ).toBeInTheDocument();

    await usuario.type(titulo, 'Agenda tu cita');
    await usuario.clear(screen.getByRole('textbox', { name: 'Respuesta 1' }));
    await usuario.type(screen.getByRole('textbox', { name: 'Respuesta 1' }), 'No.');
    await usuario.click(screen.getByRole('button', { name: 'Guardar cambios' }));

    const respuesta2 = screen.getByRole('textbox', { name: 'Respuesta 2' });
    expect(
      await screen.findByText('Los enlaces deben empezar con /, https://, mailto: o tel:.'),
    ).toBeInTheDocument();
    expect(respuesta2).toHaveAttribute('aria-invalid', 'true');
  });

  it('si alguien más guardó antes, ofrece recargar la versión nueva', async () => {
    let lecturas = 0;
    servidor.use(
      api.yo(sesionAdministrador),
      admin.portada(() => {
        lecturas += 1;
        return lecturas === 1
          ? portadaAdmin({ version: 'AAAA', modificadoEn: '2026-11-01T17:00:00Z' })
          : portadaAdmin({
              titulo: 'Título de otra persona',
              version: 'BBBB',
              modificadoEn: '2026-11-02T17:00:00Z',
            });
      }),
      admin.guardarPortada(() => problema(409, 'Conflicto')),
    );
    const { usuario } = renderizarRuta('/admin/portada');

    await screen.findByRole('textbox', { name: 'Título' });
    await usuario.click(screen.getByRole('button', { name: 'Guardar cambios' }));
    await usuario.click(await screen.findByRole('button', { name: 'Recargar' }));

    expect(await screen.findByDisplayValue('Título de otra persona')).toBeInTheDocument();
  });

  it('solo el administrador la ve en el menú y puede editarla', async () => {
    servidor.use(api.yo(() => sesionPersonal({ rol: 'Supervisor' })));
    renderizarRuta('/admin/portada');

    expect(await screen.findByText('Solo el administrador edita la portada.')).toBeInTheDocument();
    const menu = screen.getByRole('navigation', { name: 'Panel' });
    expect(within(menu).queryByRole('link', { name: 'Portada del sitio' })).not.toBeInTheDocument();
  });
});
