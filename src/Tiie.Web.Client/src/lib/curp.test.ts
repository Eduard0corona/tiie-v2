import vectores from '../../../../tests/vectores-curp.json';
import { calcularDigitoVerificador, esCurpValida, interpretarCurp, normalizarCurp } from './curp';

interface Vector {
  curp: string;
  valida: boolean;
  fechaNacimiento?: string;
  sexo?: string;
  nota: string;
}

describe('RN15 CURP con los vectores compartidos con el backend', () => {
  it('hay vectores válidos e inválidos', () => {
    const lista = vectores as Vector[];
    expect(lista.some((v) => v.valida)).toBe(true);
    expect(lista.some((v) => !v.valida)).toBe(true);
  });

  it.each((vectores as Vector[]).map((v) => [v.nota, v] as const))('RN15 %s', (_nota, vector) => {
    const datos = interpretarCurp(vector.curp);
    expect(datos !== null).toBe(vector.valida);
    expect(esCurpValida(vector.curp)).toBe(vector.valida);
    if (vector.valida) {
      expect(datos?.curp).toBe(normalizarCurp(vector.curp));
      expect(datos?.fechaNacimiento).toBe(vector.fechaNacimiento);
      expect(datos?.sexo).toBe(vector.sexo);
    }
  });
});

describe('CURP', () => {
  it('RN15 normaliza mayúsculas y espacios', () => {
    expect(normalizarCurp(' goma 080515 msl ')).toBe('GOMA080515MSL');
  });

  it('RN15 calcula el dígito verificador del ejemplo de la documentación', () => {
    expect(calcularDigitoVerificador('GOMA080515MSLRRNA')).toBe(5);
  });

  it('RN15 rechaza vacío, nulo e indefinido', () => {
    expect(interpretarCurp('')).toBeNull();
    expect(interpretarCurp(null)).toBeNull();
    expect(interpretarCurp(undefined)).toBeNull();
  });
});
