import { useLocation, useSearchParams } from 'react-router';
import { esFechaIso } from '@/lib/fechas';

export type Paso = 'sede' | 'fecha' | 'hora' | 'datos';

export const PASOS: readonly { paso: Paso; titulo: string }[] = [
  { paso: 'sede', titulo: 'Sede y trámite' },
  { paso: 'fecha', titulo: 'Fecha' },
  { paso: 'hora', titulo: 'Hora' },
  { paso: 'datos', titulo: 'Tus datos' },
];

/** Selección del asistente; vive en la URL para que Atrás funcione. Nunca lleva datos personales. */
export interface Seleccion {
  municipio: number | null;
  modulo: number | null;
  oferta: number | null;
  fecha: string | null;
  cupo: number | null;
}

type Clave = keyof Seleccion | 'paso';

/** Aviso que acompaña un regreso a un paso anterior, por ejemplo cuando el horario se llenó. */
export interface EstadoNavegacion {
  aviso?: string;
}

function numero(valor: string | null): number | null {
  const n = valor ? Number(valor) : Number.NaN;
  return Number.isInteger(n) && n > 0 ? n : null;
}

/** Paso que se puede mostrar: si falta una elección anterior, se vuelve al primer paso incompleto. */
function pasoEfectivo(pedido: string | null, s: Seleccion): Paso {
  const paso: Paso = PASOS.some((p) => p.paso === pedido) ? (pedido as Paso) : 'sede';
  if (s.oferta === null) return 'sede';
  if ((paso === 'hora' || paso === 'datos') && s.fecha === null) return 'fecha';
  if (paso === 'datos' && s.cupo === null) return 'hora';
  return paso;
}

export function useEstadoAsistente() {
  const [parametros, setParametros] = useSearchParams();
  const ubicacion = useLocation();
  const fecha = parametros.get('fecha');
  const seleccion: Seleccion = {
    municipio: numero(parametros.get('municipio')),
    modulo: numero(parametros.get('modulo')),
    oferta: numero(parametros.get('oferta')),
    fecha: esFechaIso(fecha) ? fecha : null,
    cupo: numero(parametros.get('cupo')),
  };

  /** Cambia la URL: un paso nuevo agrega una entrada al historial; una elección dentro del paso la reemplaza. */
  const ir = (cambios: Partial<Record<Clave, string | number | null>>, estado?: EstadoNavegacion) => {
    const siguiente = new URLSearchParams(parametros);
    for (const [clave, valor] of Object.entries(cambios)) {
      if (valor === null || valor === undefined) siguiente.delete(clave);
      else siguiente.set(clave, String(valor));
    }
    setParametros(siguiente, { replace: !('paso' in cambios), state: estado });
  };

  return {
    paso: pasoEfectivo(parametros.get('paso'), seleccion),
    seleccion,
    ir,
    aviso: (ubicacion.state as EstadoNavegacion | null)?.aviso ?? null,
  };
}
