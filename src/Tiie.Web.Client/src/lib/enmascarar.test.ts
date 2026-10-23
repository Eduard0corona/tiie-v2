import { enmascararCelular, enmascararCorreo, enmascararCurp } from './enmascarar';

describe('enmascarar', () => {
  it('conserva el largo de la CURP con 4 caracteres al inicio y 2 al final', () => {
    expect(enmascararCurp('GOMA080515MSLRRNA5')).toBe('GOMA************A5');
  });

  it('deja solo la primera letra del correo', () => {
    expect(enmascararCorreo('ana@example.com')).toBe('a***@example.com');
    expect(enmascararCorreo('sin-arroba')).toBe('***');
  });

  it('deja los últimos 4 dígitos del celular', () => {
    expect(enmascararCelular('6671234567')).toBe('******4567');
  });

  it('devuelve vacío sin dato', () => {
    expect(enmascararCurp(null)).toBe('');
    expect(enmascararCorreo(undefined)).toBe('');
    expect(enmascararCelular('')).toBe('');
  });
});
