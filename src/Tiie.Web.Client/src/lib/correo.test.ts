import { sugerirCorreo } from './correo';

describe('sugerencia de correo', () => {
  it.each([
    ['ana@gmial.com', 'ana@gmail.com'],
    ['ana@gmail.co', 'ana@gmail.com'],
    ['ana@hotmial.com', 'ana@hotmail.com'],
    ['Ana.Gomez@Hotmal.com', 'ana.gomez@hotmail.com'],
    ['ana@outlok.com', 'ana@outlook.com'],
    ['ana@yahoo.com.mz', 'ana@yahoo.com.mx'],
  ])('%s → %s', (escrito, sugerido) => {
    expect(sugerirCorreo(escrito)).toBe(sugerido);
  });

  it.each(['ana@gmail.com', 'ana@escuela.edu.mx', 'ana@gmx.com', 'ana@', 'ana', '@gmail.com', ''])(
    'no sugiere nada para %j',
    (escrito) => {
      expect(sugerirCorreo(escrito)).toBeNull();
    },
  );
});
