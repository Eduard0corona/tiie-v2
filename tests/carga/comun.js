// Ayudas compartidas por los escenarios de carga (docs/12-pruebas.md#carga). Se ejecutan con k6, no con Node.
import crypto from 'k6/crypto';
import http from 'k6/http';

export const URL = (__ENV.K6_URL || 'https://localhost:7180').replace(/\/$/, '');
export const LOCAL = /^https?:\/\/localhost[:/]/.test(`${URL}/`);

/** Token de las claves de prueba de Turnstile: QA debe usar la clave secreta de prueba durante la carga. */
export const TOKEN_CAPTCHA_PRUEBA = 'XXXX.DUMMY.TOKEN.XXXX';

const JSON_CSRF = { 'Content-Type': 'application/json', 'X-Tiie-Csrf': '1' };

const DICCIONARIO = '0123456789ABCDEFGHIJKLMNÑOPQRSTUVWXYZ';
const LETRAS = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ';
const CONSONANTES = 'BCDFGHJKLMNPQRSTVWXYZ';

function una(de) {
  return de.charAt(Math.floor(Math.random() * de.length));
}

function dos(n) {
  return String(n).padStart(2, '0');
}

/** CURP válida y casi siempre nueva (RN-15); misma regla que src/Tiie.Dominio/Solicitantes/Curp.cs. */
export function curpNueva() {
  const base =
    una(LETRAS) +
    una('AEIOU') +
    una(LETRAS) +
    una(LETRAS) +
    dos(5 + Math.floor(Math.random() * 11)) +
    dos(1 + Math.floor(Math.random() * 12)) +
    dos(1 + Math.floor(Math.random() * 28)) +
    una('HM') +
    'SL' +
    una(CONSONANTES) +
    una(CONSONANTES) +
    una(CONSONANTES) +
    una(LETRAS);
  let suma = 0;
  for (let i = 0; i < 17; i++) suma += DICCIONARIO.indexOf(base.charAt(i)) * (18 - i);
  return base + ((10 - (suma % 10)) % 10);
}

export function uuid() {
  const h = crypto.hexEncode(crypto.randomBytes(16));
  return `${h.slice(0, 8)}-${h.slice(8, 12)}-4${h.slice(13, 16)}-a${h.slice(17, 20)}-${h.slice(20, 32)}`;
}

/** Ofertas del escenario: K6_OFERTAS="1,2" o las primeras `cuantas` del sitio público. */
export function ofertasDelEscenario(cuantas) {
  if (__ENV.K6_OFERTAS) return __ENV.K6_OFERTAS.split(',').map((x) => Number(x.trim()));
  const ofertas = [];
  for (const modulo of http.get(`${URL}/api/publico/modulos`).json()) {
    for (const oferta of http.get(`${URL}/api/publico/modulos/${modulo.moduloId}/ofertas`).json()) {
      ofertas.push(oferta.ofertaId);
      if (ofertas.length === cuantas) return ofertas;
    }
  }
  if (ofertas.length === 0) throw new Error('El sitio no tiene ofertas públicas.');
  return ofertas;
}

export function reservar(cupoId) {
  const curp = curpNueva();
  return http.post(
    `${URL}/api/publico/citas`,
    JSON.stringify({
      cupoId,
      curp,
      nombre: 'Carga',
      apellidoPaterno: 'Prueba',
      apellidoMaterno: null,
      celular: '6670000000',
      correo: `carga.${curp.toLowerCase()}@example.com`,
      aceptaAvisoPrivacidad: true,
      tokenCaptcha: TOKEN_CAPTCHA_PRUEBA,
    }),
    {
      headers: { ...JSON_CSRF, 'Idempotency-Key': uuid() },
      tags: { endpoint: 'reserva' },
      // SinCupo y YaTieneCita (409) son respuestas correctas cuando muchos piden el mismo lugar.
      responseCallback: http.expectedStatuses(201, 409),
    },
  );
}

/** RFC 6238 con SHA-1, 30 segundos y 6 dígitos, para entrar al panel con MFA. */
function totp(secretoBase32) {
  const alfabeto = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ234567';
  let bits = '';
  for (const c of secretoBase32.replace(/[\s=-]/g, '').toUpperCase())
    bits += alfabeto.indexOf(c).toString(2).padStart(5, '0');
  const llave = new Uint8Array((bits.match(/.{8}/g) || []).map((b) => parseInt(b, 2)));
  const contador = new Uint8Array(8);
  let paso = Math.floor(Date.now() / 30000);
  for (let i = 7; i >= 0; i--) {
    contador[i] = paso & 0xff;
    paso = Math.floor(paso / 256);
  }
  const hmac = new Uint8Array(crypto.hmac('sha1', llave.buffer, contador.buffer, 'binary'));
  const d = hmac[hmac.length - 1] & 0xf;
  const valor =
    (((hmac[d] & 0x7f) << 24) | (hmac[d + 1] << 16) | (hmac[d + 2] << 8) | hmac[d + 3]) % 1000000;
  return String(valor).padStart(6, '0');
}

/**
 * Entra al panel si hay K6_USUARIO, K6_CONTRASENA y K6_TOTP (un usuario de pruebas con rol Supervisor o
 * Administrador). Devuelve false si no hay credenciales.
 */
export function entrarAlPanel() {
  const { K6_USUARIO: usuario, K6_CONTRASENA: contrasena, K6_TOTP: secreto } = __ENV;
  if (!usuario || !contrasena || !secreto) return false;
  const entrar = http.post(`${URL}/api/admin/auth/entrar`, JSON.stringify({ usuario, contrasena }), {
    headers: JSON_CSRF,
  });
  if (entrar.status !== 200 || entrar.json('siguientePaso') !== 'CodigoMfa')
    throw new Error(`No se pudo entrar al panel: HTTP ${entrar.status}.`);
  const mfa = http.post(
    `${URL}/api/admin/auth/entrar/mfa`,
    JSON.stringify({ codigo: totp(secreto), recordarEsteEquipo: false }),
    { headers: JSON_CSRF },
  );
  if (mfa.status !== 204) throw new Error(`El código MFA no fue aceptado: HTTP ${mfa.status}.`);
  return true;
}

/**
 * Cupos sobrevendidos de las ofertas (RN-02): con sesión del panel, cada cupo de la ventana con
 * Ocupados > Capacidad; sin ella, solo los días cuya disponibilidad pública queda en negativo.
 */
export function cuposSobrevendidos(ofertas, conPanel) {
  const encontrados = [];
  for (const ofertaId of ofertas) {
    if (conPanel) {
      const cupos = http.get(`${URL}/api/admin/ofertas/${ofertaId}/cupos`).json();
      for (const c of cupos)
        if (c.ocupados > c.capacidad) encontrados.push(`oferta ${ofertaId} ${c.fecha} ${c.hora}: ${c.ocupados}/${c.capacidad}`);
    } else {
      const dias = http.get(`${URL}/api/publico/ofertas/${ofertaId}/disponibilidad`).json('dias');
      for (const d of dias) if (d.libres < 0) encontrados.push(`oferta ${ofertaId} ${d.fecha}: ${d.libres} libres`);
    }
  }
  return encontrados;
}
