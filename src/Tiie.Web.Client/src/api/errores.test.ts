import {
  CODIGOS_ERROR,
  ErrorApi,
  copiarErroresAlFormulario,
  mensajeDeError,
  type ProblemaApi,
} from './errores';

describe('ErrorApi', () => {
  it.each(CODIGOS_ERROR)('el código %s produce un mensaje propio en español', (codigo) => {
    const error = new ErrorApi(400, { codigo });
    expect(error.codigo).toBe(codigo);
    expect(error.mensaje).not.toBe('');
    if (codigo !== 'ErrorInterno') expect(error.mensaje).not.toBe('Algo salió mal. Intenta de nuevo.');
    expect(mensajeDeError(error)).toBe(error.mensaje);
  });

  it('usa los mensajes de docs/08 para la reserva', () => {
    expect(new ErrorApi(409, { codigo: 'SinCupo' }).mensaje).toBe(
      'Ese horario se acaba de llenar. Elige otro.',
    );
    expect(new ErrorApi(409, { codigo: 'YaTieneCita' }).mensaje).toBe(
      'Esta CURP ya tiene una cita agendada.',
    );
    expect(new ErrorApi(422, { codigo: 'FueraDeVentana' }).mensaje).toBe('Esa fecha ya no está disponible.');
    expect(new ErrorApi(429, { codigo: 'DemasiadasSolicitudes' }).mensaje).toBe(
      'Demasiados intentos. Espera unos minutos.',
    );
  });

  it('un código desconocido o sin cuerpo da el mensaje genérico', () => {
    expect(new ErrorApi(500, null).mensaje).toBe('Algo salió mal. Intenta de nuevo.');
    // La respuesta llega del servidor sin validar: puede traer un código que esta versión no conoce.
    const desconocido = { codigo: 'Inventado' } as unknown as ProblemaApi;
    expect(new ErrorApi(418, desconocido).codigo).toBeNull();
  });

  it('conserva traceId, errores por campo y Retry-After', () => {
    const error = new ErrorApi(
      422,
      { codigo: 'Validacion', traceId: '00-abc', errors: { curp: ['La CURP no es válida.'] } },
      '30',
    );
    expect(error.traceId).toBe('00-abc');
    expect(error.errors).toEqual({ curp: ['La CURP no es válida.'] });
    expect(error.retryAfter).toBe(30);
  });

  it('un error de red se explica como falta de conexión', () => {
    expect(mensajeDeError(new TypeError('Failed to fetch'))).toMatch(/conexión/);
  });
});

describe('copiarErroresAlFormulario', () => {
  it('marca solo los campos que el formulario conoce', () => {
    const setError = vi.fn();
    const error = new ErrorApi(422, {
      codigo: 'Validacion',
      errors: { nueva: ['La contraseña debe tener al menos 12 caracteres.'], otro: ['x'] },
    });
    const marcado = copiarErroresAlFormulario<{ actual: string; nueva: string }>(error, setError, [
      'actual',
      'nueva',
    ]);
    expect(marcado).toBe(true);
    expect(setError).toHaveBeenCalledTimes(1);
    expect(setError).toHaveBeenCalledWith(
      'nueva',
      { type: 'server', message: 'La contraseña debe tener al menos 12 caracteres.' },
      { shouldFocus: true },
    );
  });
});
