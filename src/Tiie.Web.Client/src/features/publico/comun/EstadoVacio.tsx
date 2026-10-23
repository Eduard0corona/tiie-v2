import type { ReactNode } from 'react';
import { EncabezadoPaso } from '@/components/ui/Encabezado';
import { Contenedor } from '@/components/ui/Contenedor';
import { Icono, type NombreIcono } from '@/components/ui/Icono';

const tonos = {
  marca: 'bg-guinda-50 text-guinda-700 ring-guinda-100',
  exito: 'bg-emerald-50 text-emerald-700 ring-emerald-100',
  aviso: 'bg-amber-50 text-amber-700 ring-amber-100',
} as const;

/**
 * Pantalla de un solo mensaje (enlace vencido, correo enviado, página no encontrada): ícono, título que recibe
 * el foco, explicación y las acciones que siguen.
 */
export function EstadoVacio({
  icono,
  tono = 'marca',
  titulo,
  children,
  acciones,
}: {
  icono: NombreIcono;
  tono?: keyof typeof tonos;
  titulo: string;
  children: ReactNode;
  acciones?: ReactNode;
}) {
  return (
    <Contenedor ancho="angosto" className="py-12 sm:py-20">
      <section className="flex flex-col items-center gap-5 text-center">
        <span className={`grid size-16 place-items-center rounded-2xl ring-8 ${tonos[tono]}`}>
          <Icono nombre={icono} className="size-8" />
        </span>
        <EncabezadoPaso className="text-3xl tracking-tight">{titulo}</EncabezadoPaso>
        <div className="flex max-w-lg flex-col gap-3 text-lg text-slate-600">{children}</div>
        {acciones && (
          <div className="mt-3 flex w-full flex-col justify-center gap-3 sm:flex-row">{acciones}</div>
        )}
      </section>
    </Contenedor>
  );
}
