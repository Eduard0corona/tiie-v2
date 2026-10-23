import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router';
import { api, datos } from '@/api/cliente';
import { claves } from '@/app/consultas';

/** Sesión del personal (`GET /api/admin/auth/yo`); responde aunque la sesión aún no tenga MFA. */
export function useSesion() {
  return useQuery({
    queryKey: claves.sesion,
    queryFn: async () => datos(await api.GET('/api/admin/auth/yo')),
    staleTime: 5 * 60_000,
    retry: false,
  });
}

/** Cierra la sesión, borra la caché del panel y lleva a entrar. */
export function useSalir() {
  const cliente = useQueryClient();
  const navegar = useNavigate();
  return useMutation({
    mutationFn: async () => {
      await api.POST('/api/admin/auth/salir');
    },
    onSettled: () => {
      cliente.removeQueries({ queryKey: claves.admin });
      void navegar('/admin/entrar', { replace: true });
    },
  });
}

/** Quita espacios y guiones: «123 456» y «123-456» son el mismo código. */
export function limpiarCodigo(codigo: string): string {
  return codigo.replace(/[\s-]/g, '');
}
