import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { Link, useNavigate, useSearchParams } from 'react-router';
import { api, datos } from '@/api/cliente';
import { copiarErroresAlFormulario } from '@/api/errores';
import { RegionAnuncios } from '@/components/ui/Alerta';
import { Boton } from '@/components/ui/Boton';
import { Campo } from '@/components/ui/Campo';
import { Cargando, EncabezadoPaso } from '@/components/ui/Encabezado';
import { AreaTexto, Selector } from '@/components/ui/Selector';
import { celda, celdaEncabezado, ErrorPanel, TablaDesplazable } from '@/features/admin/comun/Componentes';
import { usePermisos } from '@/features/admin/comun/permisos';
import {
  useModulosAdmin,
  useOfertasAdmin,
  useTiposTramiteAdmin,
} from '@/features/admin/comun/useCatalogosAdmin';
import { idOpcional } from '@/features/admin/comun/useFiltrosUrl';
import { esquemaNuevaOferta, type ValoresNuevaOferta } from './esquemas';

/** Ofertas (trámite en un módulo) con su ventana; el supervisor las da de alta (RF-13). */
export function PaginaOfertas() {
  const { supervisor } = usePermisos();
  const [parametros, setParametros] = useSearchParams();
  const moduloId = idOpcional(parametros.get('moduloId'));
  const modulos = useModulosAdmin();
  const ofertas = useOfertasAdmin(moduloId);

  return (
    <section className="flex flex-col gap-4">
      <EncabezadoPaso>Ofertas y horarios</EncabezadoPaso>
      <Selector
        etiqueta="Módulo"
        className="max-w-md"
        value={moduloId ?? ''}
        onChange={(e) => setParametros(e.target.value ? { moduloId: e.target.value } : {}, { replace: true })}
      >
        <option value="">Todos mis módulos</option>
        {modulos.data?.map((m) => (
          <option key={m.moduloId} value={m.moduloId}>
            {m.nombre}
          </option>
        ))}
      </Selector>

      {ofertas.isPending && <Cargando texto="Cargando ofertas…" />}
      {ofertas.isError && <ErrorPanel error={ofertas.error} />}
      {ofertas.data?.length === 0 && <p className="text-slate-700">No hay ofertas.</p>}
      {ofertas.data && ofertas.data.length > 0 && (
        <TablaDesplazable etiqueta="Ofertas">
          <thead>
            <tr>
              <th scope="col" className={celdaEncabezado}>
                Trámite
              </th>
              <th scope="col" className={celdaEncabezado}>
                Módulo
              </th>
              <th scope="col" className={celdaEncabezado}>
                Ventana
              </th>
              <th scope="col" className={celdaEncabezado}>
                Estado
              </th>
            </tr>
          </thead>
          <tbody>
            {ofertas.data.map((o) => (
              <tr key={o.ofertaId}>
                <td className={celda}>
                  <Link to={`/admin/ofertas/${o.ofertaId}`} className="font-medium text-guinda-700 underline">
                    {o.tramite}
                  </Link>
                </td>
                <td className={celda}>{o.modulo}</td>
                <td className={celda}>{o.diasVentana} días</td>
                <td className={celda}>{o.activo ? 'Activa' : 'Inactiva'}</td>
              </tr>
            ))}
          </tbody>
        </TablaDesplazable>
      )}

      {supervisor && <FormularioNuevaOferta key={moduloId ?? 0} moduloId={moduloId} />}
    </section>
  );
}

function FormularioNuevaOferta({ moduloId }: { moduloId: number | undefined }) {
  const modulos = useModulosAdmin(true);
  const tipos = useTiposTramiteAdmin();
  const cliente = useQueryClient();
  const navegar = useNavigate();
  const formulario = useForm<ValoresNuevaOferta>({
    resolver: zodResolver(esquemaNuevaOferta),
    defaultValues: {
      moduloId: moduloId ? String(moduloId) : '',
      tipoTramiteId: '',
      diasVentana: '',
      notasSede: '',
    },
  });
  const { register, handleSubmit, formState, setError } = formulario;
  const errores = formState.errors;
  const [enCampos, setEnCampos] = useState(false);

  const crear = useMutation({
    mutationFn: async (v: ValoresNuevaOferta) =>
      datos(
        await api.POST('/api/admin/ofertas', {
          body: {
            moduloId: Number(v.moduloId),
            tipoTramiteId: Number(v.tipoTramiteId),
            diasVentana: v.diasVentana ? Number(v.diasVentana) : null,
            notasSede: v.notasSede.trim() || null,
          },
        }),
      ),
    onSuccess: (oferta) => {
      void cliente.invalidateQueries({ queryKey: ['admin', 'ofertas'] });
      void navegar(`/admin/ofertas/${oferta.ofertaId}`);
    },
    onError: (error) =>
      setEnCampos(
        copiarErroresAlFormulario(error, setError, ['moduloId', 'tipoTramiteId', 'diasVentana', 'notasSede']),
      ),
  });

  if (modulos.isPending || tipos.isPending) return <Cargando texto="Cargando catálogos…" />;

  return (
    <form
      noValidate
      onSubmit={handleSubmit((v) => crear.mutate(v))}
      className="flex flex-col gap-4 rounded-md border border-slate-200 bg-white p-4"
      aria-labelledby="titulo-nueva-oferta"
    >
      <h2 id="titulo-nueva-oferta" className="text-lg font-semibold">
        Nueva oferta
      </h2>
      <div className="grid gap-4 sm:grid-cols-2">
        <Selector etiqueta="Módulo" error={errores.moduloId?.message} {...register('moduloId')}>
          <option value="">Elige el módulo</option>
          {modulos.data?.map((m) => (
            <option key={m.moduloId} value={m.moduloId}>
              {m.nombre}
            </option>
          ))}
        </Selector>
        <Selector etiqueta="Trámite" error={errores.tipoTramiteId?.message} {...register('tipoTramiteId')}>
          <option value="">Elige el trámite</option>
          {tipos.data
            ?.filter((t) => t.activo)
            .map((t) => (
              <option key={t.tipoTramiteId} value={t.tipoTramiteId}>
                {t.nombre}
              </option>
            ))}
        </Selector>
        <Campo
          etiqueta="Días de ventana (opcional)"
          inputMode="numeric"
          ayuda="Cuántos días hacia adelante se puede agendar, de 1 a 120."
          error={errores.diasVentana?.message}
          {...register('diasVentana')}
        />
        <AreaTexto
          etiqueta="Notas de la sede (opcional)"
          error={errores.notasSede?.message}
          {...register('notasSede')}
        />
      </div>
      <RegionAnuncios>{crear.isError && !enCampos && <ErrorPanel error={crear.error} />}</RegionAnuncios>
      <Boton type="submit" ocupado={crear.isPending} textoOcupado="Creando…" className="self-start">
        Crear oferta
      </Boton>
    </form>
  );
}
