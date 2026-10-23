import { EncabezadoPaso } from '@/components/ui/Encabezado';
import { useSesion } from '@/features/admin/sesion/useSesion';

/** Provisional: la agenda del día y la asistencia llegan en la Fase 3 (RF-09). */
export function PaginaAgenda() {
  const { data: sesion } = useSesion();
  return (
    <section className="flex flex-col gap-4">
      <EncabezadoPaso>Agenda del día</EncabezadoPaso>
      <p className="text-slate-700">Disponible en la Fase 3.</p>
      {sesion && sesion.modulos.length > 0 && (
        <div>
          <h2 className="font-semibold">Tus módulos</h2>
          <ul className="mt-1 list-disc pl-5 text-slate-700">
            {sesion.modulos.map((m) => (
              <li key={m.moduloId}>{m.nombre}</li>
            ))}
          </ul>
        </div>
      )}
    </section>
  );
}
