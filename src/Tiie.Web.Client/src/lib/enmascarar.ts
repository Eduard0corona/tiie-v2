/**
 * Enmascarado de datos personales, igual que src/Tiie.Dominio/Privacidad/Enmascarar.cs.
 * La API ya envía los datos del ciudadano enmascarados; esto sirve para lo que se escribe en pantalla.
 */

/** 'GOMA080515MSLRRNA5' → 'GOMA************A5'. */
export function enmascararCurp(curp: string | null | undefined): string {
  if (!curp) return '';
  if (curp.length <= 6) return '*'.repeat(curp.length);
  return `${curp.slice(0, 4)}${'*'.repeat(curp.length - 6)}${curp.slice(-2)}`;
}

/** 'ana@example.com' → 'a***@example.com'. */
export function enmascararCorreo(correo: string | null | undefined): string {
  if (!correo) return '';
  const arroba = correo.lastIndexOf('@');
  if (arroba <= 0) return '***';
  return `${correo.charAt(0)}***${correo.slice(arroba)}`;
}

/** '6671234567' → '******4567'. */
export function enmascararCelular(celular: string | null | undefined): string {
  if (!celular) return '';
  if (celular.length <= 4) return '*'.repeat(celular.length);
  return `${'*'.repeat(celular.length - 4)}${celular.slice(-4)}`;
}
