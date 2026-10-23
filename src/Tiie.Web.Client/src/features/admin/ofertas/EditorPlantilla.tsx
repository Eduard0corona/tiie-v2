import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { api, datos } from '@/api/cliente';
import type { components } from '@/api/esquema';
import { Alerta, RegionAnuncios } from '@/components/ui/Alerta';
import { Boton } from '@/components/ui/Boton';
import { Casilla } from '@/components/ui/Campo';
import { Dialogo } from '@/components/ui/Dialogo';
import { ErrorPanel } from '@/features/admin/comun/Componentes';
import { DIAS_SEMANA } from '@/features/admin/comun/textos';

type PlantillaSemanal = components['schemas']['PlantillaSemanal'];
type HoraPlantilla = components['schemas']['HoraPlantilla'];
type Semana = Record<number, HoraPlantilla[]>;

const FORMATO_HORA = /^([01]\d|2[0-3]):[0-5]\d$/;

function semanaDe(plantilla: PlantillaSemanal): Semana {
  const semana: Semana = { 0: [], 1: [], 2: [], 3: [], 4: [], 5: [], 6: [] };
  for (const dia of plantilla.dias)
    semana[dia.diaSemana] = [...dia.horas].sort((a, b) => a.hora.localeCompare(b.hora));
  return semana;
}

function plantillaDe(semana: Semana): PlantillaSemanal {
  return {
    dias: DIAS_SEMANA.map(({ valor }) => ({
      diaSemana: valor,
      horas: [...(semana[valor] ?? [])].sort((a, b) => a.hora.localeCompare(b.hora)),
    })).filter((d) => d.horas.length > 0),
  };
}

/** Media hora después de la última, sin pasar de las 23:30. */
function siguienteHora(hora: string | undefined): string {
  if (!hora || !FORMATO_HORA.test(hora)) return '08:00';
  const [h, m] = hora.split(':').map(Number) as [number, number];
  const minutos = Math.min(h * 60 + m + 30, 23 * 60 + 30);
  return `${String(Math.floor(minutos / 60)).padStart(2, '0')}:${String(minutos % 60).padStart(2, '0')}`;
}

/** Revisa formato y horas repetidas; las reglas de cupos las aplica la API al regenerar. */
function revisar(semana: Semana): string | null {
  for (const { valor, nombre } of DIAS_SEMANA) {
    const horas = semana[valor] ?? [];
    if (horas.some((h) => !FORMATO_HORA.test(h.hora))) return `${nombre}: escribe cada hora como HH:mm.`;
    if (horas.some((h) => !Number.isInteger(h.turnos) || h.turnos < 1 || h.turnos > 99)) {
      return `${nombre}: los turnos van de 1 a 99.`;
    }
    if (new Set(horas.map((h) => h.hora)).size !== horas.length) return `${nombre}: hay horas repetidas.`;
  }
  return null;
}

const entrada = 'min-h-11 w-full rounded-md border border-slate-400 bg-white px-2 py-1 text-base';

export interface EditorPlantillaProps {
  ofertaId: number;
  plantilla: PlantillaSemanal;
  editable: boolean;
}

/**
 * Plantilla semanal de una oferta: horas y turnos por día. Al guardar, la API reemplaza la semana y regenera
 * los cupos de la ventana; los cupos con citas no cambian (RN-05).
 */
export function EditorPlantilla({ ofertaId, plantilla, editable }: EditorPlantillaProps) {
  const [semana, setSemana] = useState<Semana>(() => semanaDe(plantilla));
  const [copiandoDe, setCopiandoDe] = useState<number | null>(null);
  const [problema, setProblema] = useState<string | null>(null);
  const cliente = useQueryClient();

  const guardar = useMutation({
    mutationFn: async () =>
      datos(
        await api.PUT('/api/admin/ofertas/{ofertaId}/plantilla', {
          params: { path: { ofertaId } },
          body: plantillaDe(semana),
        }),
      ),
    onSuccess: () => {
      void cliente.invalidateQueries({ queryKey: ['admin', 'cupos', ofertaId] });
      void cliente.invalidateQueries({ queryKey: ['publico', 'disponibilidad', ofertaId] });
      void cliente.invalidateQueries({ queryKey: ['publico', 'cupos', ofertaId] });
    },
  });

  const cambiar = (dia: number, horas: HoraPlantilla[]) => {
    setSemana((s) => ({ ...s, [dia]: horas }));
    setProblema(null);
    guardar.reset();
  };

  const enviar = () => {
    const encontrado = revisar(semana);
    setProblema(encontrado);
    if (!encontrado) guardar.mutate();
  };

  return (
    <div className="flex flex-col gap-4">
      <h2 className="text-lg font-semibold">Plantilla semanal</h2>
      <p className="text-sm text-slate-600">
        Cada hora tiene un número de turnos: cuántas personas se atienden a esa hora.
      </p>
      <div className="grid gap-4 md:grid-cols-2">
        {DIAS_SEMANA.map(({ valor, nombre }) => {
          const horas = semana[valor] ?? [];
          const diaMinusculas = nombre.toLowerCase();
          return (
            <fieldset
              key={valor}
              className="flex flex-col gap-2 rounded-xl border border-slate-200 bg-white p-3 shadow-sm"
            >
              <legend className="px-1 font-semibold">{nombre}</legend>
              {horas.length === 0 && <p className="text-sm text-slate-600">Sin servicio.</p>}
              {horas.map((h, i) => (
                <div key={i} className="grid grid-cols-[1fr_6rem_auto] items-center gap-2">
                  <input
                    type="time"
                    aria-label={`Hora ${i + 1} del ${diaMinusculas}`}
                    className={entrada}
                    value={h.hora}
                    disabled={!editable}
                    onChange={(e) =>
                      cambiar(
                        valor,
                        horas.map((x, j) => (j === i ? { ...x, hora: e.target.value } : x)),
                      )
                    }
                  />
                  <input
                    type="number"
                    min={1}
                    max={99}
                    aria-label={`Turnos de la hora ${i + 1} del ${diaMinusculas}`}
                    className={entrada}
                    value={Number.isNaN(h.turnos) ? '' : h.turnos}
                    disabled={!editable}
                    onChange={(e) =>
                      cambiar(
                        valor,
                        horas.map((x, j) => (j === i ? { ...x, turnos: e.target.valueAsNumber } : x)),
                      )
                    }
                  />
                  {editable && (
                    <Boton
                      variante="secundario"
                      aria-label={`Quitar la hora ${i + 1} del ${diaMinusculas}`}
                      onClick={() =>
                        cambiar(
                          valor,
                          horas.filter((_, j) => j !== i),
                        )
                      }
                    >
                      Quitar
                    </Boton>
                  )}
                </div>
              ))}
              {editable && (
                <div className="flex flex-wrap gap-2">
                  <Boton
                    variante="secundario"
                    aria-label={`Agregar hora al ${diaMinusculas}`}
                    onClick={() => {
                      const ultima = horas.at(-1);
                      cambiar(valor, [
                        ...horas,
                        { hora: siguienteHora(ultima?.hora), turnos: ultima?.turnos ?? 1 },
                      ]);
                    }}
                  >
                    Agregar hora
                  </Boton>
                  {horas.length > 0 && (
                    <Boton
                      variante="secundario"
                      aria-label={`Copiar el ${diaMinusculas} a otros días`}
                      onClick={() => setCopiandoDe(valor)}
                    >
                      Copiar a otros días
                    </Boton>
                  )}
                </div>
              )}
            </fieldset>
          );
        })}
      </div>

      <RegionAnuncios>
        {problema && <Alerta tono="error">{problema}</Alerta>}
        {guardar.isError && <ErrorPanel error={guardar.error} />}
        {guardar.isSuccess && (
          <Alerta tono="exito" titulo="La plantilla se guardó y los cupos se regeneraron.">
            <ul className="list-disc pl-5">
              <li>Cupos creados: {guardar.data.cuposCreados}</li>
              <li>Cupos actualizados: {guardar.data.cuposActualizados}</li>
              <li>Cupos eliminados: {guardar.data.cuposEliminados}</li>
              <li>Cupos con citas que no cambiaron: {guardar.data.cuposConCitasSinCambio}</li>
            </ul>
            {guardar.data.cuposConCitasSinCambio > 0 && (
              <p className="mt-2">Revisa esos cupos en la tabla de cupos si necesitas ajustarlos.</p>
            )}
          </Alerta>
        )}
      </RegionAnuncios>

      {editable && (
        <Boton className="self-start" ocupado={guardar.isPending} textoOcupado="Guardando…" onClick={enviar}>
          Guardar plantilla
        </Boton>
      )}

      {copiandoDe !== null && (
        <DialogoCopiarDia
          origen={copiandoDe}
          alCopiar={(destinos) => {
            const horas = semana[copiandoDe] ?? [];
            setSemana((s) => {
              const siguiente = { ...s };
              for (const d of destinos) siguiente[d] = horas.map((h) => ({ ...h }));
              return siguiente;
            });
            guardar.reset();
            setCopiandoDe(null);
          }}
          alCerrar={() => setCopiandoDe(null)}
        />
      )}
    </div>
  );
}

function DialogoCopiarDia({
  origen,
  alCopiar,
  alCerrar,
}: {
  origen: number;
  alCopiar: (destinos: number[]) => void;
  alCerrar: () => void;
}) {
  const [destinos, setDestinos] = useState<number[]>([]);
  const nombre = DIAS_SEMANA.find((d) => d.valor === origen)?.nombre.toLowerCase() ?? '';
  return (
    <Dialogo titulo={`Copiar el ${nombre} a…`} alCerrar={alCerrar}>
      <p className="text-slate-700">Las horas de los días que elijas se reemplazan por las del {nombre}.</p>
      <fieldset>
        <legend className="sr-only">Días de destino</legend>
        {DIAS_SEMANA.filter((d) => d.valor !== origen).map((d) => (
          <Casilla
            key={d.valor}
            etiqueta={d.nombre}
            checked={destinos.includes(d.valor)}
            onChange={(e) =>
              setDestinos((actuales) =>
                e.target.checked ? [...actuales, d.valor] : actuales.filter((x) => x !== d.valor),
              )
            }
          />
        ))}
      </fieldset>
      <div className="flex flex-wrap justify-end gap-2">
        <Boton variante="secundario" onClick={alCerrar}>
          Volver
        </Boton>
        <Boton disabled={destinos.length === 0} onClick={() => alCopiar(destinos)}>
          Copiar
        </Boton>
      </div>
    </Dialogo>
  );
}
