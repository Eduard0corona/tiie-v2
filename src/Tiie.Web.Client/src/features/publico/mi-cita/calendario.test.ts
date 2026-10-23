import { crearIcs, intervalo, urlGoogleCalendar, type EventoCita } from './calendario';

const evento: EventoCita = {
  folio: '261103-1000042',
  fecha: '2026-11-12',
  hora: '10:30',
  tramite: 'Primera vez',
  sede: 'Módulo Culiacán Centro',
  direccion: 'Av. Ejemplo 123, Col. Centro, C.P. 80000',
  municipio: 'Culiacán',
};

describe('evento de calendario de la cita', () => {
  it('dura media hora en hora local de Sinaloa', () => {
    expect(intervalo(evento)).toEqual(['20261112T103000', '20261112T110000']);
  });

  it('pasa al día siguiente si la cita termina después de medianoche', () => {
    expect(intervalo({ ...evento, fecha: '2026-12-31', hora: '23:45' })).toEqual([
      '20261231T234500',
      '20270101T001500',
    ]);
  });

  it('el enlace de Google Calendar lleva la zona de Sinaloa y el folio', () => {
    const url = new URL(urlGoogleCalendar(evento));
    expect(url.origin).toBe('https://calendar.google.com');
    expect(url.searchParams.get('dates')).toBe('20261112T103000/20261112T110000');
    expect(url.searchParams.get('ctz')).toBe('America/Mazatlan');
    expect(url.searchParams.get('details')).toContain('261103-1000042');
  });

  it('el .ics escapa comas, dobla líneas largas y no lleva datos personales', () => {
    const ics = crearIcs(evento, Date.UTC(2026, 10, 3, 17, 4, 11));

    expect(ics).toContain('DTSTART;TZID=America/Mazatlan:20261112T103000\r\n');
    expect(ics).toContain('DTSTAMP:20261103T170411Z\r\n');
    expect(ics).toContain('UID:261103-1000042@citas-tiie\r\n');
    expect(ics.replace(/\r\n /g, '')).toContain(
      'LOCATION:Módulo Culiacán Centro\\, Av. Ejemplo 123\\, Col. Centro\\, C.P. 80000\\, Culiacán\\, Sinaloa',
    );
    const codificador = new TextEncoder();
    for (const linea of ics.split('\r\n')) expect(codificador.encode(linea).length).toBeLessThanOrEqual(75);
    expect(ics.endsWith('END:VCALENDAR\r\n')).toBe(true);
  });
});
