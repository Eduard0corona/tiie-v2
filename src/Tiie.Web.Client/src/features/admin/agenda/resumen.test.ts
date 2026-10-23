import { citaAgenda } from '@/test/admin';
import { filtrarAgenda, resumirAgenda, seguimientoDeHoy } from './resumen';

const dia = [
  citaAgenda({ citaId: 1, hora: '08:00', estado: 'Atendida', nombreCompleto: 'Ana Gómez Martínez' }),
  citaAgenda({ citaId: 2, hora: '09:00', estado: 'NoAsistio', folio: '261103-1000050' }),
  citaAgenda({ citaId: 3, hora: '09:30', estado: 'Agendada' }),
  citaAgenda({ citaId: 4, hora: '10:30', estado: 'Agendada' }),
  citaAgenda({ citaId: 5, hora: '11:00', estado: 'Cancelada' }),
  citaAgenda({ citaId: 6, hora: '12:00', estado: 'Reprogramada' }),
];

describe('Resumen de la agenda', () => {
  it('cuenta por filtro; canceladas y reprogramadas no cuentan como programadas', () => {
    const resumen = resumirAgenda(dia);
    expect(resumen.porFiltro).toEqual({ todas: 6, porAtender: 2, atendidas: 1, noAsistio: 1, canceladas: 2 });
    expect(resumen.programadas).toBe(4);
    expect(resumen.registradas).toBe(2);
  });

  it('filtra por estado y por texto sin importar acentos ni mayúsculas', () => {
    expect(filtrarAgenda(dia, 'porAtender', '').map((c) => c.citaId)).toEqual([3, 4]);
    expect(filtrarAgenda(dia, 'todas', 'gomez').map((c) => c.citaId)).toContain(1);
    expect(filtrarAgenda(dia, 'todas', '1000050').map((c) => c.citaId)).toEqual([2]);
    expect(filtrarAgenda(dia, 'atendidas', '1000050')).toEqual([]);
  });

  it('a las 10:00 la siguiente es la de 10:30 y la de 09:30 ya pasó su hora', () => {
    const { siguiente, atrasadas } = seguimientoDeHoy(dia, '10:00');
    expect(siguiente?.citaId).toBe(4);
    expect(atrasadas).toBe(1);
  });

  it('una cita a la hora exacta todavía es la siguiente', () => {
    expect(seguimientoDeHoy(dia, '10:30').siguiente?.citaId).toBe(4);
    expect(seguimientoDeHoy(dia, '10:31').siguiente).toBeUndefined();
  });
});
