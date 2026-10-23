import { useSearchParams } from 'react-router';

/**
 * Filtros y página de una lista guardados en la URL: se pueden compartir y sobreviven a recargar.
 * Aplicar filtros vuelve a la página 1.
 */
export function useFiltrosUrl<K extends string>(claves: readonly K[]) {
  const [parametros, setParametros] = useSearchParams();
  const filtros = Object.fromEntries(claves.map((c) => [c, parametros.get(c) ?? ''])) as Record<K, string>;
  const pagina = Math.max(1, Number.parseInt(parametros.get('pagina') ?? '', 10) || 1);

  const aplicar = (formulario: HTMLFormElement) => {
    const valores = new FormData(formulario);
    const siguientes = new URLSearchParams();
    for (const clave of claves) {
      const valor = String(valores.get(clave) ?? '').trim();
      if (valor) siguientes.set(clave, valor);
    }
    setParametros(siguientes);
  };

  const irAPagina = (numero: number) =>
    setParametros((actuales) => {
      const siguientes = new URLSearchParams(actuales);
      siguientes.set('pagina', String(numero));
      return siguientes;
    });

  return { filtros, pagina, aplicar, irAPagina, firma: parametros.toString() };
}

/** Identificador de un filtro de texto; vacío o inválido es `undefined`. */
export function idOpcional(texto: string | null | undefined): number | undefined {
  const numero = Number(texto);
  return texto && Number.isInteger(numero) && numero > 0 ? numero : undefined;
}
