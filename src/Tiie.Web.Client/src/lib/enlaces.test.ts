import { destinoPermitido, tramosDeTexto } from './enlaces';

describe('enlaces del contenido editable', () => {
  it('separa el texto y los enlaces internos y externos', () => {
    expect(
      tramosDeTexto('Consulta el [aviso](/aviso-de-privacidad) o [gob.mx/curp](https://www.gob.mx/curp/).'),
    ).toEqual([
      { tipo: 'texto', texto: 'Consulta el ' },
      { tipo: 'enlace', texto: 'aviso', destino: '/aviso-de-privacidad', interno: true },
      { tipo: 'texto', texto: ' o ' },
      { tipo: 'enlace', texto: 'gob.mx/curp', destino: 'https://www.gob.mx/curp/', interno: false },
      { tipo: 'texto', texto: '.' },
    ]);
  });

  it('un destino no permitido se queda como texto', () => {
    expect(tramosDeTexto('Haz [clic](javascript:void0) ya')).toEqual([
      { tipo: 'texto', texto: 'Haz [clic](javascript:void0) ya' },
    ]);
  });

  it('permite las mismas rutas que la API', () => {
    for (const destino of ['/recuperar', '/#preguntas', 'https://a.mx', 'mailto:a@b.mx', 'tel:6671234567']) {
      expect(destinoPermitido(destino)).toBe(true);
    }
    for (const destino of [
      'javascript:x',
      'http://a.mx',
      '//a.mx',
      '/\\a.mx',
      'data:text/html,x',
      'recuperar',
    ]) {
      expect(destinoPermitido(destino)).toBe(false);
    }
  });
});
