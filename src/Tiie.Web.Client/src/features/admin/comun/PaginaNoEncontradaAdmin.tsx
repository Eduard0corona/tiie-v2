import { Link } from 'react-router';
import { estiloBoton } from '@/components/ui/Boton';
import { EncabezadoPaso } from '@/components/ui/Encabezado';
import { Icono } from '@/components/ui/Icono';

/** Una dirección de /admin que no existe: se queda en el panel en lugar de mostrar el 404 público. */
export function PaginaNoEncontradaAdmin() {
  return (
    <section className="mx-auto flex max-w-lg flex-col items-center gap-4 py-12 text-center">
      <span className="grid size-14 place-items-center rounded-2xl bg-guinda-50 text-guinda-700 ring-8 ring-guinda-50/60">
        <Icono nombre="ubicacion" className="size-7" />
      </span>
      <EncabezadoPaso className="text-2xl tracking-tight">Página no encontrada</EncabezadoPaso>
      <p className="text-slate-600">Esa sección del panel no existe o cambió de dirección.</p>
      <Link to="/admin/agenda" className={estiloBoton()}>
        Ir a la agenda
      </Link>
    </section>
  );
}
