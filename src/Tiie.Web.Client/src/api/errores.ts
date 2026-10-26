import type { FieldValues, Path, UseFormSetError } from 'react-hook-form';
import type { components } from './esquema';

/** Código de error de la API (docs/06-api.md#errores); generado del OpenAPI. */
export type CodigoError = components['schemas']['CodigoError'];

/**
 * Problem Details (RFC 9457) con las extensiones de la API, generado del OpenAPI. Parcial: una respuesta de
 * error que no viene de la API (un proxy, por ejemplo) puede traer menos campos.
 */
export type ProblemaApi = Partial<components['schemas']['ProblemaApi']>;

/** Un mensaje por cada código: si el backend agrega uno, la verificación de tipos obliga a escribirlo. */
const MENSAJES: Record<CodigoError, string> = {
  SolicitudInvalida: 'La solicitud no es válida. Recarga la página e intenta de nuevo.',
  NoAutenticado: 'Tu sesión terminó. Vuelve a entrar.',
  CredencialesInvalidas: 'Usuario o contraseña incorrectos.',
  CodigoMfaInvalido: 'El código no es correcto. Revisa que la hora de tu celular esté bien.',
  Prohibido: 'No tienes permiso para esta acción.',
  MfaRequerido: 'Configura la verificación en dos pasos para continuar.',
  NoEncontrado: 'No encontramos lo que buscas.',
  SinCupo: 'Ese horario se acaba de llenar. Elige otro.',
  YaTieneCita: 'Esta CURP ya tiene una cita agendada.',
  Conflicto: 'Alguien más modificó este registro. Recarga la página para ver los cambios.',
  Validacion: 'Hay datos por corregir.',
  CaptchaInvalido: 'Confirma que no eres un robot.',
  FueraDeVentana: 'Esa fecha ya no está disponible.',
  CupoCerrado: 'Esa fecha ya no está disponible.',
  OfertaInactiva: 'Esa fecha ya no está disponible.',
  CitaNoModificable: 'La cita ya no se puede modificar.',
  EnlaceInvalido: 'Este enlace ya no es válido.',
  TokenInvalido: 'Este enlace ya no es válido o ya se usó.',
  CuentaBloqueada: 'La cuenta está bloqueada por demasiados intentos. Espera unos minutos.',
  DemasiadasSolicitudes: 'Demasiados intentos. Espera unos minutos.',
  ErrorInterno: 'Algo salió mal. Intenta de nuevo.',
  CorreoNoDisponible: 'No se pudo enviar el correo. Intenta más tarde.',
};

/** Todos los códigos del catálogo. */
export const CODIGOS_ERROR = Object.keys(MENSAJES) as CodigoError[];

const MENSAJE_GENERICO = 'Algo salió mal. Intenta de nuevo.';
const MENSAJE_SIN_CONEXION = 'No hay conexión con el servidor. Revisa tu internet e intenta de nuevo.';

function esCodigoError(valor: unknown): valor is CodigoError {
  return typeof valor === 'string' && Object.hasOwn(MENSAJES, valor);
}

/** Error de la API con su código HTTP, su `codigo` y los errores por campo. */
export class ErrorApi extends Error {
  readonly status: number;
  readonly codigo: CodigoError | null;
  readonly errors: Record<string, string[]>;
  readonly traceId: string | null;
  /** Segundos que pide esperar el servidor (cabecera Retry-After). */
  readonly retryAfter: number | null;
  readonly problema: ProblemaApi | null;

  constructor(status: number, problema: ProblemaApi | null, retryAfter?: string | null) {
    const codigo = esCodigoError(problema?.codigo) ? problema.codigo : null;
    super(codigo ?? `HTTP ${status}`);
    this.name = 'ErrorApi';
    this.status = status;
    this.codigo = codigo;
    this.errors = problema?.errors ?? {};
    this.traceId = problema?.traceId ?? null;
    const segundos = retryAfter != null ? Number.parseInt(retryAfter, 10) : Number.NaN;
    this.retryAfter = Number.isFinite(segundos) ? segundos : null;
    this.problema = problema;
  }

  /** Mensaje en español para mostrar en pantalla. */
  get mensaje(): string {
    return this.codigo ? MENSAJES[this.codigo] : MENSAJE_GENERICO;
  }
}

/** Mensaje para cualquier error de una consulta o mutación. */
export function mensajeDeError(error: unknown): string {
  if (error instanceof ErrorApi) return error.mensaje;
  if (error instanceof TypeError) return MENSAJE_SIN_CONEXION;
  return MENSAJE_GENERICO;
}

export function mensajeDeCodigo(codigo: CodigoError): string {
  return MENSAJES[codigo];
}

/**
 * Copia los errores por campo de un 422 `Validacion` al formulario. Devuelve `true` si marcó algún campo;
 * los campos que el formulario no conoce se ignoran.
 */
export function copiarErroresAlFormulario<T extends FieldValues>(
  error: unknown,
  setError: UseFormSetError<T>,
  campos: readonly Path<T>[],
): boolean {
  if (!(error instanceof ErrorApi)) return false;
  let marcado = false;
  for (const [campo, mensajes] of Object.entries(error.errors)) {
    const conocido = campos.find((c) => c === campo);
    if (conocido && mensajes.length > 0) {
      setError(conocido, { type: 'server', message: mensajes.join(' ') }, { shouldFocus: !marcado });
      marcado = true;
    }
  }
  return marcado;
}

/** Los errores de la API llegan como `requisitos[0].texto`; el formulario los nombra `requisitos.0.texto`. */
export function rutasDeFormulario(error: unknown): unknown {
  if (!(error instanceof ErrorApi)) return error;
  const errors = Object.fromEntries(
    Object.entries(error.errors).map(([campo, mensajes]) => [campo.replace(/\[(\d+)\]/g, '.$1'), mensajes]),
  );
  return new ErrorApi(error.status, { ...error.problema, errors });
}
