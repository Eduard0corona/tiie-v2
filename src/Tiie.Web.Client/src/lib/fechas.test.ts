import { formatearFechaCorta, formatearFechaLarga, formatearHoraDeInstante, hoyLocal } from './fechas';

describe('fechas en la zona de Sinaloa', () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  it('RN03 a las 18:00 de Sinaloa (01:00 UTC del día siguiente) hoy sigue siendo el día local', () => {
    vi.useFakeTimers();
    vi.setSystemTime(Date.UTC(2026, 10, 3, 1, 0, 0)); // 2026-11-03T01:00:00Z = 2 nov, 18:00 en Sinaloa
    expect(hoyLocal()).toBe('2026-11-02');
  });

  it('a las 07:00 UTC ya es el día siguiente en Sinaloa', () => {
    vi.useFakeTimers();
    vi.setSystemTime(Date.UTC(2026, 10, 3, 7, 0, 0)); // 00:00 del 3 nov en Sinaloa
    expect(hoyLocal()).toBe('2026-11-03');
  });

  it('formatea la fecha sin recorrerla un día', () => {
    expect(formatearFechaLarga('2026-11-12')).toBe('jueves 12 de noviembre de 2026');
    expect(formatearFechaCorta('2026-01-01')).toBe('01/01/2026');
  });

  it('rechaza fechas con formato distinto de yyyy-MM-dd', () => {
    expect(() => formatearFechaLarga('12/11/2026')).toThrow();
  });

  it('convierte un instante UTC a la hora de Sinaloa', () => {
    expect(formatearHoraDeInstante('2026-11-03T17:04:11Z')).toBe('10:04');
  });
});
