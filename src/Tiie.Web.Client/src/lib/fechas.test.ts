import {
  esFechaIso,
  fechaCalendario,
  formatearDiaSemana,
  formatearFechaCorta,
  formatearFechaLarga,
  formatearHoraDeInstante,
  formatearInstante,
  horaLocal,
  hoyLocal,
  inicioDeMes,
  isoDeCalendario,
  sumarDias,
} from './fechas';

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

describe('fechas del calendario', () => {
  it('valida fechas que existen', () => {
    expect(esFechaIso('2026-02-28')).toBe(true);
    expect(esFechaIso('2026-02-30')).toBe(false);
    expect(esFechaIso('2026-2-3')).toBe(false);
    expect(esFechaIso(null)).toBe(false);
  });

  it('ida y vuelta entre texto y el Date del calendario conserva el día', () => {
    const fecha = fechaCalendario('2026-11-12');
    expect(fecha.getDate()).toBe(12);
    expect(fecha.getMonth()).toBe(10);
    expect(isoDeCalendario(fecha)).toBe('2026-11-12');
  });

  it('escribe el día de la semana en español', () => {
    expect(formatearDiaSemana('2026-11-12')).toBe('jueves 12 de noviembre');
  });
});

describe('fechas del panel', () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  it('suma días sin recorrer la fecha por la zona horaria', () => {
    expect(sumarDias('2026-11-30', 1)).toBe('2026-12-01');
    expect(sumarDias('2026-03-01', -1)).toBe('2026-02-28');
  });

  it('la hora local y los instantes se muestran en hora de Sinaloa', () => {
    vi.useFakeTimers();
    vi.setSystemTime(Date.UTC(2026, 10, 3, 1, 30, 0));
    expect(horaLocal()).toBe('18:30');
    expect(formatearInstante('2026-11-03T17:04:11Z')).toBe('03/11/2026 10:04');
    expect(inicioDeMes('2026-11-23')).toBe('2026-11-01');
  });
});
