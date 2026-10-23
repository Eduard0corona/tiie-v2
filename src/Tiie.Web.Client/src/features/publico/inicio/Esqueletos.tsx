/** Tarjetas vacías mientras llegan los datos: la página no salta cuando aparecen. */
export function Esqueletos({ texto, cantidad = 3 }: { texto: string; cantidad?: number }) {
  return (
    <div role="status" className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
      <span className="sr-only">{texto}</span>
      {Array.from({ length: cantidad }, (_, i) => (
        <div key={i} aria-hidden="true" className="h-40 animate-pulse rounded-2xl bg-slate-200/70" />
      ))}
    </div>
  );
}
