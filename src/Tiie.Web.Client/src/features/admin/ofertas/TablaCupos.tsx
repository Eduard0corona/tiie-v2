import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { api, datos } from '@/api/cliente';
import type { components } from '@/api/esquema';
import { Alerta, RegionAnuncios } from '@/components/ui/Alerta';
import { Boton } from '@/components/ui/Boton';
import { Campo } from '@/components/ui/Campo';
import { Cargando } from '@/components/ui/Encabezado';
import { celda, celdaEncabezado, ErrorPanel, TablaDesplazable } from '@/features/admin/comun/Componentes';
import { clavesAdmin } from '@/features/admin/comun/useCatalogosAdmin';
import { esFechaIso, formatearFechaCorta, hoyLocal, sumarDias } from '@/lib/fechas';

type CupoAdmin = components['schemas']['CupoAdmin'];
type AjusteCupo = components['schemas']['AjusteCupo'];

/** Cupos generados de una oferta en un rango; el supervisor ajusta capacidad o los cierra (RF-14). */
export function TablaCupos({ ofertaId, editable }: { ofertaId: number; editable: boolean }) {
  const hoy = hoyLocal();
  const [desde, setDesde] = useState(hoy);
  const [hasta, setHasta] = useState(() => sumarDias(hoy, 6));
  const rangoValido = esFechaIso(desde) && esFechaIso(hasta) && desde <= hasta;
  const cliente = useQueryClient();

  const cupos = useQuery({
    queryKey: clavesAdmin.cupos(ofertaId, desde, hasta),
    queryFn: async () =>
      datos(
        await api.GET('/api/admin/ofertas/{ofertaId}/cupos', {
          params: { path: { ofertaId }, query: { desde, hasta } },
        }),
      ),
    enabled: rangoValido,
  });

  const ajustar = useMutation({
    mutationFn: async ({ cupo, ajuste }: { cupo: CupoAdmin; ajuste: AjusteCupo }) =>
      datos(
        await api.PATCH('/api/admin/cupos/{cupoId}', {
          params: { path: { cupoId: cupo.cupoId } },
          body: ajuste,
        }),
      ),
    onSuccess: () => cliente.invalidateQueries({ queryKey: ['admin', 'cupos', ofertaId] }),
  });

  const descripcion = (cupo: CupoAdmin) => `${formatearFechaCorta(cupo.fecha)} ${cupo.hora}`;

  return (
    <div className="flex flex-col gap-4">
      <h2 className="text-lg font-semibold">Cupos</h2>
      <div className="grid max-w-md grid-cols-2 gap-3">
        <Campo etiqueta="Desde" type="date" value={desde} onChange={(e) => setDesde(e.target.value)} />
        <Campo
          etiqueta="Hasta"
          type="date"
          value={hasta}
          error={rangoValido || !esFechaIso(hasta) ? undefined : 'Debe ser igual o posterior a «Desde».'}
          onChange={(e) => setHasta(e.target.value)}
        />
      </div>

      <RegionAnuncios>
        {ajustar.isSuccess && (
          <Alerta tono="exito">Cupo del {descripcion(ajustar.variables.cupo)} guardado.</Alerta>
        )}
        {ajustar.isError && (
          <ErrorPanel
            error={ajustar.error}
            alRecargar={() => {
              ajustar.reset();
              void cupos.refetch();
            }}
          />
        )}
      </RegionAnuncios>

      {rangoValido && cupos.isPending && <Cargando texto="Cargando cupos…" />}
      {cupos.isError && <ErrorPanel error={cupos.error} />}
      {cupos.data?.length === 0 && <p className="text-slate-700">No hay cupos en esas fechas.</p>}
      {cupos.data && cupos.data.length > 0 && (
        <TablaDesplazable etiqueta="Cupos de la oferta">
          <thead>
            <tr>
              <th scope="col" className={celdaEncabezado}>
                Fecha y hora
              </th>
              <th scope="col" className={celdaEncabezado}>
                Ocupados
              </th>
              <th scope="col" className={celdaEncabezado}>
                Capacidad
              </th>
              <th scope="col" className={celdaEncabezado}>
                Cerrado
              </th>
              {editable && (
                <th scope="col" className={celdaEncabezado}>
                  <span className="sr-only">Acciones</span>
                </th>
              )}
            </tr>
          </thead>
          <tbody>
            {cupos.data.map((cupo) => (
              <FilaCupo
                key={`${cupo.cupoId}-${cupo.version}`}
                cupo={cupo}
                editable={editable}
                ocupado={ajustar.isPending && ajustar.variables.cupo.cupoId === cupo.cupoId}
                alGuardar={(ajuste) => ajustar.mutate({ cupo, ajuste })}
                descripcion={descripcion(cupo)}
              />
            ))}
          </tbody>
        </TablaDesplazable>
      )}
    </div>
  );
}

interface FilaCupoProps {
  cupo: CupoAdmin;
  editable: boolean;
  ocupado: boolean;
  descripcion: string;
  alGuardar: (ajuste: AjusteCupo) => void;
}

function FilaCupo({ cupo, editable, ocupado, descripcion, alGuardar }: FilaCupoProps) {
  const [capacidad, setCapacidad] = useState(String(cupo.capacidad));
  const [cerrado, setCerrado] = useState(cupo.cerrado);
  const capacidadNueva = Number(capacidad);
  // La API pide al menos un lugar y no menos de los ocupados.
  const minimo = Math.max(1, cupo.ocupados);
  const capacidadValida = /^\d+$/.test(capacidad) && capacidadNueva >= minimo;
  const cambio = capacidadNueva !== cupo.capacidad || cerrado !== cupo.cerrado;

  return (
    <tr>
      <td className={`${celda} font-medium`}>{descripcion}</td>
      <td className={celda}>{cupo.ocupados}</td>
      <td className={celda}>
        {editable ? (
          <>
            <input
              type="number"
              min={minimo}
              aria-label={`Capacidad del ${descripcion}`}
              aria-invalid={capacidadValida ? undefined : true}
              className="min-h-11 w-24 rounded-md border border-slate-400 bg-white px-2"
              value={capacidad}
              onChange={(e) => setCapacidad(e.target.value)}
            />
            {!capacidadValida && <p className="text-xs font-medium text-red-800">Mínimo {minimo}.</p>}
          </>
        ) : (
          cupo.capacidad
        )}
      </td>
      <td className={celda}>
        {editable ? (
          <input
            type="checkbox"
            aria-label={`Cerrar el ${descripcion}`}
            className="size-5 accent-guinda-700"
            checked={cerrado}
            onChange={(e) => setCerrado(e.target.checked)}
          />
        ) : cupo.cerrado ? (
          'Sí'
        ) : (
          'No'
        )}
      </td>
      {editable && (
        <td className={celda}>
          <Boton
            variante="secundario"
            disabled={!cambio || !capacidadValida}
            ocupado={ocupado}
            textoOcupado="Guardando…"
            aria-label={`Guardar el cupo del ${descripcion}`}
            onClick={() =>
              alGuardar({
                capacidad: capacidadNueva !== cupo.capacidad ? capacidadNueva : null,
                cerrado: cerrado !== cupo.cerrado ? cerrado : null,
                version: cupo.version,
              })
            }
          >
            Guardar
          </Boton>
        </td>
      )}
    </tr>
  );
}
