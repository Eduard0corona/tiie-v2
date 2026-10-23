import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link, useSearchParams } from 'react-router';
import { api, datos } from '@/api/cliente';
import { Alerta, RegionAnuncios } from '@/components/ui/Alerta';
import { Boton } from '@/components/ui/Boton';
import { Campo } from '@/components/ui/Campo';
import { Cargando, EncabezadoPaso } from '@/components/ui/Encabezado';
import { Selector } from '@/components/ui/Selector';
import {
  celda,
  celdaEncabezado,
  ErrorPanel,
  EtiquetaEstado,
  TablaDesplazable,
} from '@/features/admin/comun/Componentes';
import { usePermisos } from '@/features/admin/comun/permisos';
import { TEXTO_ESTADO, TEXTO_ORIGEN } from '@/features/admin/comun/textos';
import { clavesAdmin } from '@/features/admin/comun/useCatalogosAdmin';
import { esFechaIso, formatearFechaLarga, hoyLocal } from '@/lib/fechas';

/**
 * Agenda del día de un módulo y registro de asistencia (RF-09). Por omisión, hoy y el primer módulo del
 * usuario. Solo se marca asistencia en citas Agendada de hoy o anteriores; la API lo exige también.
 */
export function PaginaAgenda() {
  const { modulos } = usePermisos();
  const [parametros, setParametros] = useSearchParams();
  const hoy = hoyLocal();
  const fechaUrl = parametros.get('fecha');
  const fecha = esFechaIso(fechaUrl) ? fechaUrl : hoy;
  const moduloId = Number(parametros.get('modulo')) || modulos[0]?.moduloId || 0;
  const cliente = useQueryClient();

  const agenda = useQuery({
    queryKey: clavesAdmin.agenda(moduloId, fecha),
    queryFn: async () =>
      datos(await api.GET('/api/admin/agenda', { params: { query: { moduloId, fecha } } })),
    enabled: moduloId > 0,
  });

  const marcar = useMutation({
    mutationFn: async ({ citaId, asistio }: { citaId: number; asistio: boolean; folio: string }) =>
      datos(
        await api.POST('/api/admin/citas/{citaId}/asistencia', {
          params: { path: { citaId } },
          body: { asistio },
        }),
      ),
    onSuccess: () => cliente.invalidateQueries({ queryKey: ['admin', 'agenda'] }),
  });

  const cambiar = (clave: string, valor: string) =>
    setParametros(
      (p) => {
        p.set(clave, valor);
        return p;
      },
      { replace: true },
    );

  return (
    <section className="flex flex-col gap-4">
      <EncabezadoPaso>Agenda del día</EncabezadoPaso>
      {modulos.length === 0 ? (
        <Alerta>No tienes módulos asignados. Pide a un administrador que te asigne uno.</Alerta>
      ) : (
        <div className="grid gap-3 sm:grid-cols-2">
          <Selector etiqueta="Módulo" value={moduloId} onChange={(e) => cambiar('modulo', e.target.value)}>
            {modulos.map((m) => (
              <option key={m.moduloId} value={m.moduloId}>
                {m.nombre}
              </option>
            ))}
          </Selector>
          <Campo
            etiqueta="Fecha"
            type="date"
            value={fecha}
            onChange={(e) => cambiar('fecha', e.target.value || hoy)}
          />
        </div>
      )}

      <RegionAnuncios>
        {marcar.isSuccess && (
          <Alerta tono="exito">
            Cita {marcar.variables.folio}: {TEXTO_ESTADO[marcar.data.estado]}.
          </Alerta>
        )}
        {marcar.isError && <ErrorPanel error={marcar.error} />}
      </RegionAnuncios>

      {moduloId > 0 && (
        <h2 className="text-lg font-semibold first-letter:uppercase">{formatearFechaLarga(fecha)}</h2>
      )}
      {agenda.isPending && moduloId > 0 && <Cargando texto="Cargando la agenda…" />}
      {agenda.isError && <ErrorPanel error={agenda.error} />}
      {agenda.data && agenda.data.length === 0 && (
        <p className="text-slate-700">No hay citas para este día.</p>
      )}
      {agenda.data && agenda.data.length > 0 && (
        <TablaDesplazable etiqueta={`Citas del ${formatearFechaLarga(fecha)}`}>
          <thead>
            <tr>
              <th scope="col" className={celdaEncabezado}>
                Hora
              </th>
              <th scope="col" className={celdaEncabezado}>
                Folio
              </th>
              <th scope="col" className={celdaEncabezado}>
                Persona
              </th>
              <th scope="col" className={celdaEncabezado}>
                Trámite
              </th>
              <th scope="col" className={celdaEncabezado}>
                Estado
              </th>
              <th scope="col" className={celdaEncabezado}>
                Asistencia
              </th>
            </tr>
          </thead>
          <tbody>
            {agenda.data.map((cita) => {
              const marcable = cita.estado === 'Agendada' && fecha <= hoy;
              return (
                <tr key={cita.citaId}>
                  <td className={`${celda} font-semibold`}>{cita.hora}</td>
                  <td className={celda}>
                    <Link to={`/admin/citas/${cita.citaId}`} className="text-guinda-700 underline">
                      {cita.folio}
                    </Link>
                    <div className="text-xs text-slate-600">{TEXTO_ORIGEN[cita.origen]}</div>
                  </td>
                  <td className={celda}>
                    {cita.nombreCompleto}
                    <div className="font-mono text-xs text-slate-600">{cita.curp}</div>
                  </td>
                  <td className={celda}>{cita.tramite}</td>
                  <td className={celda}>
                    <EtiquetaEstado estado={cita.estado} />
                  </td>
                  <td className={celda}>
                    {marcable ? (
                      <div className="flex flex-wrap gap-2">
                        <Boton
                          variante="secundario"
                          aria-label={`Atendida: ${cita.folio}`}
                          disabled={marcar.isPending}
                          onClick={() =>
                            marcar.mutate({ citaId: cita.citaId, asistio: true, folio: cita.folio })
                          }
                        >
                          Atendida
                        </Boton>
                        <Boton
                          variante="secundario"
                          aria-label={`No asistió: ${cita.folio}`}
                          disabled={marcar.isPending}
                          onClick={() =>
                            marcar.mutate({ citaId: cita.citaId, asistio: false, folio: cita.folio })
                          }
                        >
                          No asistió
                        </Boton>
                      </div>
                    ) : (
                      <span className="text-slate-500">—</span>
                    )}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </TablaDesplazable>
      )}
    </section>
  );
}
