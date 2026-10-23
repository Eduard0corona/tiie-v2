import { http, HttpResponse } from 'msw';
import type { ProblemaApi } from '@/api/errores';
import type { components } from '@/api/esquema';

/** Datos y manejadores de MSW del sitio público, tipados con el OpenAPI. Las CURP son ficticias. */
type Esquemas = components['schemas'];

export const CURP_PRUEBA = 'GOMA080515MSLRRNA5';

export const configuracion: Esquemas['ConfiguracionPublica'] = {
  claveSitioCaptcha: '1x00000000000000000000AA',
  versionAvisoPrivacidad: '2026-01',
  zonaHoraria: 'America/Mazatlan',
};

export const municipios: Esquemas['MunicipioPublico'][] = [
  { municipioId: 6, nombre: 'Culiacán' },
  { municipioId: 12, nombre: 'Mazatlán' },
];

export const modulos: Esquemas['ModuloPublico'][] = [
  {
    moduloId: 3,
    nombre: 'Módulo Culiacán Centro',
    municipioId: 6,
    municipio: 'Culiacán',
    direccion: 'Av. Ejemplo 123, Col. Centro, C.P. 80000',
    horarioAtencion: 'Lunes a viernes de 8:00 a 15:00',
    latitud: 24.8,
    longitud: -107.39,
  },
];

export const ofertas: Esquemas['OfertaPublica'][] = [
  { ofertaId: 12, tipoTramiteId: 1, nombre: 'Primera vez', costo: 0, notasSede: null },
];

export const tiposTramite: Esquemas['TipoTramitePublico'][] = [
  {
    tipoTramiteId: 1,
    nombre: 'Primera vez',
    descripcion: 'Estudiantes que nunca han tenido TIIE.',
    costo: 0,
    requisitos: [
      { nivelEscolar: null, texto: 'CURP impresa.' },
      { nivelEscolar: 'Medio superior y superior', texto: 'Ficha de inscripción del ciclo vigente.' },
    ],
  },
  {
    tipoTramiteId: 2,
    nombre: 'Sustitución',
    descripcion: null,
    costo: 150,
    requisitos: [],
  },
];

export const disponibilidad: Esquemas['DisponibilidadOferta'] = {
  ofertaId: 12,
  desde: '2026-11-10',
  hasta: '2026-11-16',
  dias: [
    { fecha: '2026-11-10', estado: 'Llena', libres: 0, capacidad: 20, motivo: null },
    { fecha: '2026-11-11', estado: 'Poca', libres: 2, capacidad: 20, motivo: null },
    { fecha: '2026-11-12', estado: 'Alta', libres: 18, capacidad: 20, motivo: null },
    { fecha: '2026-11-13', estado: 'Alta', libres: 20, capacidad: 20, motivo: null },
    { fecha: '2026-11-14', estado: 'Alta', libres: 9, capacidad: 9, motivo: null },
    { fecha: '2026-11-15', estado: 'SinServicio', libres: 0, capacidad: 0, motivo: null },
    { fecha: '2026-11-16', estado: 'Cerrado', libres: 0, capacidad: 0, motivo: 'Día inhábil' },
  ],
};

export const cupos: Esquemas['CupoDisponible'][] = [
  { cupoId: 81234, hora: '10:30', libres: 3 },
  { cupoId: 81235, hora: '11:00', libres: 1 },
];

export const citaReservada: Esquemas['CitaReservada'] = {
  folio: '261103-1000042',
  estado: 'Agendada',
  fecha: '2026-11-12',
  hora: '10:30',
  modulo: {
    moduloId: 3,
    nombre: 'Módulo Culiacán Centro',
    direccion: 'Av. Ejemplo 123, Col. Centro, C.P. 80000',
  },
  tramite: { tipoTramiteId: 1, nombre: 'Primera vez' },
};

export function detalleMiCita(parcial: Partial<Esquemas['DetalleMiCita']> = {}): Esquemas['DetalleMiCita'] {
  return {
    folio: '261103-1000042',
    folioAnterior: null,
    estado: 'Agendada',
    fecha: '2026-11-12',
    hora: '10:30',
    ofertaId: 12,
    modulo: {
      nombre: 'Módulo Culiacán Centro',
      direccion: 'Av. Ejemplo 123, Col. Centro, C.P. 80000',
      municipio: 'Culiacán',
      horarioAtencion: 'Lunes a viernes de 8:00 a 15:00',
      latitud: 24.8,
      longitud: -107.39,
    },
    tramite: {
      nombre: 'Primera vez',
      costo: 0,
      notas: 'Preséntate 10 minutos antes.',
      notasSede: null,
      requisitos: [{ nivelEscolar: null, texto: 'CURP impresa.' }],
    },
    solicitante: {
      nombreCompleto: 'Ana Gómez Martínez',
      curp: 'GOMA************A5',
      correo: 'a***@example.com',
    },
    puedeModificar: true,
    modificableHasta: '2026-11-11',
    ...parcial,
  };
}

function json<T>(r: T | Response) {
  return r instanceof Response ? r : HttpResponse.json(r as never);
}

export interface SolicitudRegistrada<T> {
  cuerpo: T;
  idempotencia: string | null;
}

export const publico = {
  catalogo: () => [
    http.get<never, never, Esquemas['ConfiguracionPublica']>('/api/publico/configuracion', () =>
      HttpResponse.json(configuracion),
    ),
    http.get<never, never, Esquemas['MunicipioPublico'][]>('/api/publico/municipios', () =>
      HttpResponse.json(municipios),
    ),
    http.get<never, never, Esquemas['ModuloPublico'][]>('/api/publico/modulos', () =>
      HttpResponse.json(modulos),
    ),
    http.get<never, never, Esquemas['ModuloPublico'][]>('/api/publico/municipios/:municipioId/modulos', () =>
      HttpResponse.json(modulos),
    ),
    http.get<never, never, Esquemas['OfertaPublica'][]>('/api/publico/modulos/:moduloId/ofertas', () =>
      HttpResponse.json(ofertas),
    ),
    http.get<never, never, Esquemas['TipoTramitePublico'][]>('/api/publico/tipos-tramite', () =>
      HttpResponse.json(tiposTramite),
    ),
  ],

  disponibilidad: (respuesta: () => Esquemas['DisponibilidadOferta'] = () => disponibilidad) =>
    http.get<never, never, Esquemas['DisponibilidadOferta']>(
      '/api/publico/ofertas/:ofertaId/disponibilidad',
      () => HttpResponse.json(respuesta()),
    ),

  cupos: (respuesta: () => Esquemas['CupoDisponible'][] = () => cupos) =>
    http.get<never, never, Esquemas['CupoDisponible'][]>('/api/publico/ofertas/:ofertaId/cupos', () =>
      HttpResponse.json(respuesta()),
    ),

  crearCita: (
    respuesta: (s: SolicitudRegistrada<Esquemas['SolicitudCita']>) => Esquemas['CitaReservada'] | Response,
  ) =>
    http.post<never, Esquemas['SolicitudCita'], Esquemas['CitaReservada'] | ProblemaApi>(
      '/api/publico/citas',
      async ({ request }) => {
        const r = respuesta({
          cuerpo: await request.json(),
          idempotencia: request.headers.get('Idempotency-Key'),
        });
        return r instanceof Response ? r : HttpResponse.json(r, { status: 201 });
      },
    ),

  recuperacion: (alRecibir: (cuerpo: Esquemas['SolicitudRecuperacion']) => void = () => {}) =>
    http.post<never, Esquemas['SolicitudRecuperacion']>(
      '/api/publico/citas/recuperacion',
      async ({ request }) => {
        alRecibir(await request.json());
        return new HttpResponse(null, { status: 202 });
      },
    ),

  miCita: (respuesta: () => Esquemas['DetalleMiCita'] | Response) =>
    http.get<never, never, Esquemas['DetalleMiCita'] | ProblemaApi>('/api/mi-cita', () => json(respuesta())),

  cancelarMiCita: (
    respuesta: (cuerpo: Esquemas['SolicitudCancelacionMiCita']) => Esquemas['DetalleMiCita'] | Response,
  ) =>
    http.post<never, Esquemas['SolicitudCancelacionMiCita'], Esquemas['DetalleMiCita'] | ProblemaApi>(
      '/api/mi-cita/cancelacion',
      async ({ request }) => json(respuesta(await request.json())),
    ),

  reprogramarMiCita: (
    respuesta: (cuerpo: Esquemas['SolicitudReprogramacionMiCita']) => Esquemas['DetalleMiCita'] | Response,
  ) =>
    http.post<never, Esquemas['SolicitudReprogramacionMiCita'], Esquemas['DetalleMiCita'] | ProblemaApi>(
      '/api/mi-cita/reprogramacion',
      async ({ request }) => json(respuesta(await request.json())),
    ),

  salirMiCita: () => http.post('/api/mi-cita/salida', () => new HttpResponse(null, { status: 204 })),
};
