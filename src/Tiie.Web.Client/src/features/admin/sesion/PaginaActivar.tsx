import { FormularioContrasenaConToken } from './FormularioContrasenaConToken';

/** Enlace de invitación: `/admin/activar?usuarioId=…&token=…`. */
export function PaginaActivar() {
  return <FormularioContrasenaConToken proposito="activacion" />;
}
