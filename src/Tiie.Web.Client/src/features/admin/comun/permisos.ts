import type { components } from '@/api/esquema';
import { useSesion } from '@/features/admin/sesion/useSesion';

type Sesion = components['schemas']['SesionPersonal'];

export const ROLES = ['Administrador', 'Supervisor', 'Capturista'] as const;

export function esAdministrador(sesion: Pick<Sesion, 'rol'> | undefined): boolean {
  return sesion?.rol === 'Administrador';
}

/** Supervisor o Administrador: ofertas, plantillas, bloqueos, reportes y correcciones (docs/07#matriz-de-permisos). */
export function esSupervisor(sesion: Pick<Sesion, 'rol'> | undefined): boolean {
  return sesion?.rol === 'Administrador' || sesion?.rol === 'Supervisor';
}

/**
 * Permisos de la sesión para mostrar u ocultar acciones. Es solo comodidad: la API decide el acceso y
 * responde 403 o 404 aunque la pantalla muestre el botón.
 */
export function usePermisos() {
  const { data: sesion } = useSesion();
  return {
    sesion,
    administrador: esAdministrador(sesion),
    supervisor: esSupervisor(sesion),
    modulos: sesion?.modulos ?? [],
  };
}
