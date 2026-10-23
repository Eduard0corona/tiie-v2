import { http, HttpResponse, type DefaultBodyType } from 'msw';
import type { ProblemaApi } from '@/api/errores';
import type { components } from '@/api/esquema';

/** Datos y manejadores de MSW del panel, tipados con el OpenAPI. Las CURP y los nombres son ficticios. */
type Esquemas = components['schemas'];
type Respuesta<T> = T | Response;

function json<T extends DefaultBodyType>(r: Respuesta<T>, status = 200): Response {
  return r instanceof Response ? r : HttpResponse.json(r, { status });
}

export const catalogos: Esquemas['CatalogosAdmin'] = {
  municipios: [
    { municipioId: 6, nombre: 'Culiacán' },
    { municipioId: 12, nombre: 'Mazatlán' },
  ],
  nivelesEscolares: [
    { nivelEscolarId: 1, nombre: 'Básico' },
    { nivelEscolarId: 2, nombre: 'Medio superior y superior' },
  ],
  roles: ['Administrador', 'Supervisor', 'Capturista'],
};

export const modulos: Esquemas['ModuloResumen'][] = [
  { moduloId: 3, nombre: 'Módulo Culiacán Centro', municipioId: 6, municipio: 'Culiacán', activo: true },
];

export const tiposTramite: Esquemas['TipoTramiteResumen'][] = [
  { tipoTramiteId: 1, nombre: 'Primera vez', costo: 0, activo: true, ofertas: 1 },
];

export const ofertaPrimeraVez: Esquemas['OfertaAdmin'] = {
  ofertaId: 12,
  moduloId: 3,
  modulo: 'Módulo Culiacán Centro',
  tipoTramiteId: 1,
  tramite: 'Primera vez',
  diasVentana: 30,
  notasSede: null,
  activo: true,
  version: 'AAAAAAAAB9E=',
};

export const ofertas: Esquemas['OfertaAdmin'][] = [ofertaPrimeraVez];

export function moduloDetalle(parcial: Partial<Esquemas['ModuloDetalle']> = {}): Esquemas['ModuloDetalle'] {
  return {
    moduloId: 3,
    municipioId: 6,
    nombre: 'Módulo Culiacán Centro',
    calle: 'Av. Ejemplo',
    numeroExterior: '123',
    numeroInterior: null,
    colonia: 'Centro',
    codigoPostal: '80000',
    localidad: null,
    telefono: null,
    correo: null,
    horarioAtencion: 'Lunes a viernes de 8:00 a 15:00',
    latitud: 24.8,
    longitud: -107.39,
    activo: true,
    version: 'AAAAAAAAB9E=',
    ...parcial,
  };
}

export function citaAgenda(parcial: Partial<Esquemas['CitaAgenda']> = {}): Esquemas['CitaAgenda'] {
  return {
    citaId: 5012,
    folio: '261103-1000042',
    hora: '10:30',
    tramite: 'Primera vez',
    nombreCompleto: 'Ana Gómez Martínez',
    curp: 'GOMA080515MSLRRNA5',
    estado: 'Agendada',
    origen: 'Web',
    ...parcial,
  };
}

export function cupoAdmin(parcial: Partial<Esquemas['CupoAdmin']> = {}): Esquemas['CupoAdmin'] {
  return {
    cupoId: 900,
    fecha: '2026-11-03',
    hora: '10:00',
    capacidad: 4,
    ocupados: 1,
    cerrado: false,
    version: 'AAAAAAAAC1A=',
    ...parcial,
  };
}

export const admin = {
  catalogos: () => http.get('/api/admin/catalogos', () => HttpResponse.json(catalogos)),

  modulos: (lista: Esquemas['ModuloResumen'][] = modulos) =>
    http.get<never, never, Esquemas['ModuloResumen'][]>('/api/admin/modulos', () => HttpResponse.json(lista)),

  modulo: (respuesta: () => Respuesta<Esquemas['ModuloDetalle']>) =>
    http.get<{ moduloId: string }, never, Esquemas['ModuloDetalle'] | ProblemaApi>(
      '/api/admin/modulos/:moduloId',
      () => json(respuesta()),
    ),

  actualizarModulo: (respuesta: (cuerpo: Esquemas['DatosModulo']) => Respuesta<Esquemas['ModuloDetalle']>) =>
    http.put<{ moduloId: string }, Esquemas['DatosModulo'], Esquemas['ModuloDetalle'] | ProblemaApi>(
      '/api/admin/modulos/:moduloId',
      async ({ request }) => json(respuesta(await request.json())),
    ),

  tiposTramite: () =>
    http.get<never, never, Esquemas['TipoTramiteResumen'][]>('/api/admin/tipos-tramite', () =>
      HttpResponse.json(tiposTramite),
    ),

  ofertas: (lista: Esquemas['OfertaAdmin'][] = ofertas) =>
    http.get<never, never, Esquemas['OfertaAdmin'][]>('/api/admin/ofertas', () => HttpResponse.json(lista)),

  oferta: (oferta: Esquemas['OfertaAdmin']) =>
    http.get<{ ofertaId: string }, never, Esquemas['OfertaAdmin']>('/api/admin/ofertas/:ofertaId', () =>
      HttpResponse.json(oferta),
    ),

  plantilla: (plantilla: Esquemas['PlantillaSemanal']) =>
    http.get<{ ofertaId: string }, never, Esquemas['PlantillaSemanal']>(
      '/api/admin/ofertas/:ofertaId/plantilla',
      () => HttpResponse.json(plantilla),
    ),

  guardarPlantilla: (
    respuesta: (cuerpo: Esquemas['PlantillaSemanal']) => Respuesta<Esquemas['ResumenGeneracion']>,
  ) =>
    http.put<{ ofertaId: string }, Esquemas['PlantillaSemanal'], Esquemas['ResumenGeneracion'] | ProblemaApi>(
      '/api/admin/ofertas/:ofertaId/plantilla',
      async ({ request }) => json(respuesta(await request.json())),
    ),

  cupos: (respuesta: (consulta: { desde: string; hasta: string }) => Esquemas['CupoAdmin'][]) =>
    http.get<{ ofertaId: string }, never, Esquemas['CupoAdmin'][]>(
      '/api/admin/ofertas/:ofertaId/cupos',
      ({ request }) => {
        const url = new URL(request.url);
        return HttpResponse.json(
          respuesta({
            desde: url.searchParams.get('desde') ?? '',
            hasta: url.searchParams.get('hasta') ?? '',
          }),
        );
      },
    ),

  agenda: (
    respuesta: (consulta: { moduloId: string; fecha: string }) => Respuesta<Esquemas['CitaAgenda'][]>,
  ) =>
    http.get<never, never, Esquemas['CitaAgenda'][] | ProblemaApi>('/api/admin/agenda', ({ request }) => {
      const url = new URL(request.url);
      return json(
        respuesta({
          moduloId: url.searchParams.get('moduloId') ?? '',
          fecha: url.searchParams.get('fecha') ?? '',
        }),
      );
    }),

  asistencia: (
    respuesta: (
      citaId: number,
      cuerpo: Esquemas['SolicitudAsistencia'],
    ) => Respuesta<Esquemas['EstadoResultante']>,
  ) =>
    http.post<
      { citaId: string },
      Esquemas['SolicitudAsistencia'],
      Esquemas['EstadoResultante'] | ProblemaApi
    >('/api/admin/citas/:citaId/asistencia', async ({ request, params }) =>
      json(respuesta(Number(params.citaId), await request.json())),
    ),

  crearCita: (
    respuesta: (
      cuerpo: Esquemas['CitaVentanilla'],
      clave: string | null,
    ) => Respuesta<Esquemas['CitaRegistrada']>,
  ) =>
    http.post<never, Esquemas['CitaVentanilla'], Esquemas['CitaRegistrada'] | ProblemaApi>(
      '/api/admin/citas',
      async ({ request }) =>
        json(respuesta(await request.json(), request.headers.get('Idempotency-Key')), 201),
    ),

  bloqueos: (lista: Esquemas['BloqueoResumen'][] = []) =>
    http.get<never, never, Esquemas['BloqueoResumen'][]>('/api/admin/bloqueos', () =>
      HttpResponse.json(lista),
    ),

  crearBloqueo: (respuesta: (cuerpo: Esquemas['CrearBloqueo']) => Respuesta<Esquemas['BloqueoCreado']>) =>
    http.post<never, Esquemas['CrearBloqueo'], Esquemas['BloqueoCreado'] | ProblemaApi>(
      '/api/admin/bloqueos',
      async ({ request }) => json(respuesta(await request.json()), 201),
    ),

  cancelarCitasBloqueo: (
    respuesta: (
      bloqueoId: number,
      cuerpo: Esquemas['SolicitudMotivo'],
    ) => Esquemas['CitasCanceladasPorBloqueo'],
  ) =>
    http.post<{ bloqueoId: string }, Esquemas['SolicitudMotivo'], Esquemas['CitasCanceladasPorBloqueo']>(
      '/api/admin/bloqueos/:bloqueoId/cancelacion-citas',
      async ({ request, params }) =>
        HttpResponse.json(respuesta(Number(params.bloqueoId), await request.json())),
    ),

  crearUsuario: (respuesta: (cuerpo: Esquemas['CrearUsuario']) => Respuesta<Esquemas['UsuarioDetalle']>) =>
    http.post<never, Esquemas['CrearUsuario'], Esquemas['UsuarioDetalle'] | ProblemaApi>(
      '/api/admin/usuarios',
      async ({ request }) => json(respuesta(await request.json()), 201),
    ),

  usuarios: (lista: Esquemas['UsuarioResumen'][] = []) =>
    http.get<never, never, Esquemas['ListaPaginadaOfUsuarioResumen']>('/api/admin/usuarios', () =>
      HttpResponse.json({ elementos: lista, total: lista.length, pagina: 1, tamano: 20 }),
    ),

  reporte: (respuesta: (consulta: URLSearchParams) => Esquemas['ReporteCitas']) =>
    http.get<never, never, Esquemas['ReporteCitas']>('/api/admin/reportes/citas', ({ request }) =>
      HttpResponse.json(respuesta(new URL(request.url).searchParams)),
    ),

  tablero: (tablero: Esquemas['Tablero']) =>
    http.get<never, never, Esquemas['Tablero']>('/api/admin/tablero', () => HttpResponse.json(tablero)),
};
