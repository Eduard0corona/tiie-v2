import { FormularioContrasenaConToken } from './FormularioContrasenaConToken';

/** Enlace del correo de restablecimiento: `/admin/restablecer?usuarioId=…&token=…`. */
export function PaginaRestablecer() {
  return <FormularioContrasenaConToken proposito="restablecimiento" />;
}
