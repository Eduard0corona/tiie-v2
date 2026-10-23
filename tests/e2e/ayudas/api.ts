import { randomUUID } from 'node:crypto';
import { expect, type APIRequestContext, type APIResponse } from '@playwright/test';
import { sumarDias } from './fechas';
import type { Persona } from './persona';

// Preparación de datos por la API. Con `page.request`, las cookies (tiie_cita, tiie_sesion) quedan en el
// mismo contexto que el navegador de la prueba.

export const CSRF = { 'X-Tiie-Csrf': '1' };

/**
 * Token que entregan las claves de sitio de prueba de Turnstile. Solo lo acepta la clave secreta de prueba
 * (`Captcha:ClaveSecreta` en Development y, durante E2E y carga, en QA).
 */
export const TOKEN_CAPTCHA_PRUEBA = 'XXXX.DUMMY.TOKEN.XXXX';

export interface OfertaPublica {
  municipioId: number;
  municipio: string;
  moduloId: number;
  modulo: string;
  ofertaId: number;
  tramite: string;
}

export interface DiaDisponible {
  fecha: string;
  estado: 'Alta' | 'Poca' | 'Llena' | 'Cerrado' | 'SinServicio';
  libres: number;
  capacidad: number;
}

export interface Disponibilidad {
  ofertaId: number;
  desde: string;
  hasta: string;
  dias: DiaDisponible[];
}

export interface CupoDisponible {
  cupoId: number;
  hora: string;
  libres: number;
}

export interface CupoAdmin {
  cupoId: number;
  fecha: string;
  hora: string;
  capacidad: number;
  ocupados: number;
  cerrado: boolean;
  version: string;
}

export interface CitaReservada {
  folio: string;
  estado: string;
  fecha: string;
  hora: string;
}

export interface PlantillaSemanal {
  dias: { diaSemana: number; horas: { hora: string; turnos: number }[] }[];
}

export async function json<T>(respuesta: APIResponse, contexto: string): Promise<T> {
  expect(respuesta.ok(), `${contexto}: HTTP ${respuesta.status()} ${await respuesta.text()}`).toBeTruthy();
  return (await respuesta.json()) as T;
}

export async function ofertasPublicas(api: APIRequestContext): Promise<OfertaPublica[]> {
  const modulos = await json<{ moduloId: number; nombre: string; municipioId: number; municipio: string }[]>(
    await api.get('/api/publico/modulos'),
    'módulos públicos',
  );
  const todas: OfertaPublica[] = [];
  for (const m of modulos) {
    const ofertas = await json<{ ofertaId: number; nombre: string }[]>(
      await api.get(`/api/publico/modulos/${m.moduloId}/ofertas`),
      'ofertas públicas',
    );
    for (const o of ofertas) {
      todas.push({
        municipioId: m.municipioId,
        municipio: m.municipio,
        moduloId: m.moduloId,
        modulo: m.nombre,
        ofertaId: o.ofertaId,
        tramite: o.nombre,
      });
    }
  }
  expect(todas.length, 'No hay ofertas activas en el sitio.').toBeGreaterThan(0);
  return todas;
}

export async function disponibilidad(api: APIRequestContext, ofertaId: number): Promise<Disponibilidad> {
  return json(await api.get(`/api/publico/ofertas/${ofertaId}/disponibilidad`), 'disponibilidad');
}

export async function cuposDelDia(api: APIRequestContext, ofertaId: number, fecha: string): Promise<CupoDisponible[]> {
  return json(await api.get(`/api/publico/ofertas/${ofertaId}/cupos?fecha=${fecha}`), 'cupos del día');
}

/** Hoy en Sinaloa según el servidor: en línea se reserva desde mañana (RN-03). */
export function hoySegun(d: Disponibilidad): string {
  return sumarDias(d.desde, -1);
}

export interface Lugar {
  oferta: OfertaPublica;
  fecha: string;
  cupo: CupoDisponible;
}

export interface BusquedaLugar {
  /** Días después de hoy como mínimo; 2 deja cancelar y reprogramar en línea (RN-13). */
  diasMinimos?: number;
  /** Buscar desde el final de la ventana, lejos de las fechas que usan las demás pruebas. */
  desdeElFinal?: boolean;
  ofertaId?: number;
  excluirFechas?: string[];
  libresMinimos?: number;
}

/** Un cupo con lugar en cualquier oferta pública que cumpla la búsqueda. */
export async function buscarLugar(api: APIRequestContext, busqueda: BusquedaLugar = {}): Promise<Lugar> {
  const { diasMinimos = 1, desdeElFinal = false, ofertaId, excluirFechas = [], libresMinimos = 1 } = busqueda;
  const ofertas = (await ofertasPublicas(api)).filter((o) => ofertaId === undefined || o.ofertaId === ofertaId);
  for (const oferta of ofertas) {
    const d = await disponibilidad(api, oferta.ofertaId);
    const minimo = sumarDias(hoySegun(d), diasMinimos);
    const dias = d.dias.filter(
      (x) => (x.estado === 'Alta' || x.estado === 'Poca') && x.fecha >= minimo && !excluirFechas.includes(x.fecha),
    );
    if (desdeElFinal) dias.reverse();
    for (const dia of dias) {
      const cupo = (await cuposDelDia(api, oferta.ofertaId, dia.fecha)).find((c) => c.libres >= libresMinimos);
      if (cupo) return { oferta, fecha: dia.fecha, cupo };
    }
  }
  throw new Error('No hay cupos con lugar para preparar la prueba. ¿Corrió el proceso GeneracionCupos?');
}

export function reservar(api: APIRequestContext, cupoId: number, persona: Persona): Promise<APIResponse> {
  return api.post('/api/publico/citas', {
    headers: { ...CSRF, 'Idempotency-Key': randomUUID() },
    data: {
      cupoId,
      curp: persona.curp,
      nombre: persona.nombre,
      apellidoPaterno: persona.apellidoPaterno,
      apellidoMaterno: persona.apellidoMaterno,
      celular: persona.celular,
      correo: persona.correo,
      aceptaAvisoPrivacidad: true,
      tokenCaptcha: TOKEN_CAPTCHA_PRUEBA,
    },
  });
}

/** Reserva en línea; con `page.request`, deja la cookie de Mi cita en el navegador de la prueba. */
export async function reservarCita(api: APIRequestContext, cupoId: number, persona: Persona): Promise<CitaReservada> {
  const respuesta = await reservar(api, cupoId, persona);
  expect(respuesta.status(), `reserva: ${await respuesta.text()}`).toBe(201);
  return (await respuesta.json()) as CitaReservada;
}

// Panel (requieren la sesión del personal en el contexto).

export async function cuposAdmin(api: APIRequestContext, ofertaId: number, fecha: string): Promise<CupoAdmin[]> {
  return json(
    await api.get(`/api/admin/ofertas/${ofertaId}/cupos?desde=${fecha}&hasta=${fecha}`),
    'cupos del panel',
  );
}

export async function ajustarCupo(
  api: APIRequestContext,
  cupo: CupoAdmin,
  ajuste: { capacidad?: number; cerrado?: boolean },
): Promise<CupoAdmin> {
  return json(
    await api.patch(`/api/admin/cupos/${cupo.cupoId}`, {
      headers: CSRF,
      data: { capacidad: ajuste.capacidad ?? null, cerrado: ajuste.cerrado ?? null, version: cupo.version },
    }),
    'ajuste de cupo',
  );
}

export async function leerPlantilla(api: APIRequestContext, ofertaId: number): Promise<PlantillaSemanal> {
  return json(await api.get(`/api/admin/ofertas/${ofertaId}/plantilla`), 'plantilla');
}

export async function guardarPlantilla(api: APIRequestContext, ofertaId: number, plantilla: PlantillaSemanal) {
  return json(
    await api.put(`/api/admin/ofertas/${ofertaId}/plantilla`, { headers: CSRF, data: plantilla }),
    'guardar plantilla',
  );
}

export async function eliminarBloqueo(api: APIRequestContext, bloqueoId: number): Promise<void> {
  const respuesta = await api.delete(`/api/admin/bloqueos/${bloqueoId}`, { headers: CSRF });
  expect([204, 404], `eliminar bloqueo: HTTP ${respuesta.status()}`).toContain(respuesta.status());
}

/** Cita en ventanilla (RF-10): puede ser de hoy, en una hora que no ha pasado. */
export function citaVentanilla(api: APIRequestContext, cupoId: number, persona: Persona): Promise<APIResponse> {
  return api.post('/api/admin/citas', {
    headers: { ...CSRF, 'Idempotency-Key': randomUUID() },
    data: {
      cupoId,
      curp: persona.curp,
      nombre: persona.nombre,
      apellidoPaterno: persona.apellidoPaterno,
      apellidoMaterno: persona.apellidoMaterno,
      celular: persona.celular,
      correo: persona.correo,
      avisoLeido: true,
    },
  });
}
