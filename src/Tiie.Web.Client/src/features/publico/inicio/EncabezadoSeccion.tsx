/** Título de una sección de la portada: antetítulo, h2 y entrada. El id del h2 nombra la sección. */
export function EncabezadoSeccion({
  id,
  antetitulo,
  titulo,
  descripcion,
  centrado = false,
}: {
  id: string;
  antetitulo: string;
  titulo: string;
  descripcion?: string;
  centrado?: boolean;
}) {
  return (
    <div className={`flex max-w-2xl flex-col gap-3 ${centrado ? 'mx-auto items-center text-center' : ''}`}>
      <p className="text-sm font-bold tracking-widest text-guinda-700 uppercase">{antetitulo}</p>
      <h2 id={id} className="text-3xl font-bold tracking-tight text-slate-950 sm:text-4xl">
        {titulo}
      </h2>
      {descripcion && <p className="text-lg text-slate-600">{descripcion}</p>}
    </div>
  );
}
