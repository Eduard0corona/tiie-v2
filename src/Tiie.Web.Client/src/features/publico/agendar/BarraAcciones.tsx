import type { ReactNode } from 'react';

/**
 * Acciones de un paso. En celular queda fija al pie de la pantalla, en una sola fila: «Continuar» siempre está a
 * la mano aunque la lista de opciones sea larga. Volver va a la izquierda y la acción principal a la derecha,
 * junto al pulgar, en el mismo orden que el foco. En pantallas medianas vuelve a su lugar en el flujo.
 */
export function BarraAcciones({ nota, children }: { nota?: ReactNode; children: ReactNode }) {
  return (
    <div className="sticky bottom-0 z-20 -mx-4 border-t border-slate-200 bg-white/95 px-4 pt-3 pb-[max(0.75rem,env(safe-area-inset-bottom))] shadow-[0_-8px_24px_-12px_rgb(15_23_42/0.15)] backdrop-blur sm:static sm:mx-0 sm:border-0 sm:bg-transparent sm:p-0 sm:shadow-none sm:backdrop-blur-none">
      {nota && <p className="mb-2 text-sm text-slate-600">{nota}</p>}
      <div className="flex gap-2 sm:gap-3">{children}</div>
    </div>
  );
}
