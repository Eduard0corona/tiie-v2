import type { components } from '@/api/esquema';

type Esquemas = components['schemas'];

/** Los enumerados de la API viajan como identificadores; en pantalla van con ortografía completa. */
export const TEXTO_ESTADO: Record<Esquemas['EstadoCita'], string> = {
  Agendada: 'Agendada',
  Atendida: 'Atendida',
  NoAsistio: 'No asistió',
  Cancelada: 'Cancelada',
  Reprogramada: 'Reprogramada',
};

export const ESTADOS_CITA = Object.keys(TEXTO_ESTADO) as Esquemas['EstadoCita'][];

export const TEXTO_ORIGEN: Record<Esquemas['OrigenCita'], string> = {
  Web: 'En línea',
  Ventanilla: 'Ventanilla',
  Migracion: 'Migración',
};

export const TEXTO_TIPO_NOTIFICACION: Record<Esquemas['TipoNotificacion'], string> = {
  Confirmacion: 'Confirmación',
  Recordatorio: 'Recordatorio',
  Cancelacion: 'Cancelación',
  Reprogramacion: 'Reprogramación',
  EnlaceRecuperacion: 'Enlace de recuperación',
  CierrePorBloqueo: 'Cierre por bloqueo',
  AvisoMigracion: 'Aviso de migración',
};

export const TEXTO_ESTADO_NOTIFICACION: Record<Esquemas['EstadoNotificacion'], string> = {
  Pendiente: 'Pendiente',
  Enviada: 'Enviada',
  Fallida: 'Falló; se reintenta',
  Descartada: 'Descartada',
};

export const TEXTO_ALCANCE: Record<Esquemas['AlcanceBloqueo'], string> = {
  General: 'General (día inhábil)',
  Modulo: 'Módulo',
  Oferta: 'Oferta',
};

/** 0 = domingo … 6 = sábado, igual que la API; la semana se muestra de lunes a domingo. */
export const DIAS_SEMANA: readonly { valor: number; nombre: string }[] = [
  { valor: 1, nombre: 'Lunes' },
  { valor: 2, nombre: 'Martes' },
  { valor: 3, nombre: 'Miércoles' },
  { valor: 4, nombre: 'Jueves' },
  { valor: 5, nombre: 'Viernes' },
  { valor: 6, nombre: 'Sábado' },
  { valor: 0, nombre: 'Domingo' },
];

const moneda = new Intl.NumberFormat('es-MX', { style: 'currency', currency: 'MXN' });

export function formatearMoneda(valor: number | null | undefined): string {
  return valor ? moneda.format(valor) : 'Sin costo';
}
