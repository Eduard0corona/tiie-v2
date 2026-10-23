// Prueba de humo de un despliegue (docs/13-despliegue-y-operacion.md#ranuras-y-reversión):
// inicio, salud, disponibilidad de una oferta y entrada al panel con un usuario de pruebas.
//
//   node tests/humo/humo.mjs https://app-tiie-prod-staging.azurewebsites.net
//
// Para el panel: HUMO_USUARIO, HUMO_CONTRASENA y HUMO_TOTP (secreto Base32 del autenticador).
// Sin ellas, ese paso se omite con una advertencia. No usa dependencias: solo Node 24.
import { createHmac } from 'node:crypto'

const base = (process.argv[2] ?? process.env.HUMO_URL ?? '').replace(/\/$/, '')
if (!base) {
  console.error('Uso: node tests/humo/humo.mjs <url-base>')
  process.exit(2)
}

// Un script no es un navegador: declara el mismo origen para pasar el filtro CSRF (docs/07#csrf).
const cabecerasEscritura = { 'Content-Type': 'application/json', 'X-Tiie-Csrf': '1', 'Sec-Fetch-Site': 'same-origin' }
const cookies = new Map()
let fallas = 0

async function pedir(ruta, opciones = {}) {
  const respuesta = await fetch(base + ruta, {
    ...opciones,
    redirect: 'manual',
    headers: { ...opciones.headers, Cookie: [...cookies].map(([k, v]) => `${k}=${v}`).join('; ') },
    signal: AbortSignal.timeout(30_000),
  })
  for (const c of respuesta.headers.getSetCookie()) {
    const [par] = c.split(';')
    const i = par.indexOf('=')
    cookies.set(par.slice(0, i), par.slice(i + 1))
  }
  return respuesta
}

async function paso(nombre, fn) {
  const inicio = performance.now()
  try {
    await fn()
    console.log(`ok   ${nombre} (${Math.round(performance.now() - inicio)} ms)`)
  } catch (error) {
    fallas++
    console.log(`FALLA ${nombre}: ${error.message}`)
  }
}

function exigir(condicion, mensaje) {
  if (!condicion) throw new Error(mensaje)
}

async function json(respuesta, esperado = 200) {
  exigir(respuesta.status === esperado, `HTTP ${respuesta.status}, se esperaba ${esperado}`)
  return respuesta.json()
}

/** RFC 6238 con SHA-1, 30 segundos y 6 dígitos, como los autenticadores y ASP.NET Core Identity. */
function totp(secretoBase32, ahora = Date.now()) {
  const alfabeto = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ234567'
  let bits = ''
  for (const c of secretoBase32.replace(/[\s=]/g, '').toUpperCase())
    bits += alfabeto.indexOf(c).toString(2).padStart(5, '0')
  const llave = Buffer.from(bits.match(/.{8}/g).map((b) => parseInt(b, 2)))
  const contador = Buffer.alloc(8)
  contador.writeBigUInt64BE(BigInt(Math.floor(ahora / 30_000)))
  const hmac = createHmac('sha1', llave).update(contador).digest()
  const desplazamiento = hmac[hmac.length - 1] & 0xf
  const valor = (hmac.readUInt32BE(desplazamiento) & 0x7fffffff) % 1_000_000
  return valor.toString().padStart(6, '0')
}

console.log(`Prueba de humo contra ${base}`)

await paso('salud', async () => {
  const r = await pedir('/salud')
  exigir(r.status === 200, `HTTP ${r.status}`)
})

await paso('inicio', async () => {
  const r = await pedir('/')
  exigir(r.status === 200, `HTTP ${r.status}`)
  exigir((await r.text()).includes('id="raiz"'), 'index.html sin el contenedor de la SPA')
})

await paso('configuración pública', async () => {
  await json(await pedir('/api/publico/configuracion'))
})

await paso('disponibilidad de una oferta', async () => {
  const modulos = await json(await pedir('/api/publico/modulos'))
  exigir(modulos.length > 0, 'no hay módulos activos')
  for (const modulo of modulos) {
    const ofertas = await json(await pedir(`/api/publico/modulos/${modulo.moduloId}/ofertas`))
    if (ofertas.length === 0) continue
    const disponibilidad = await json(await pedir(`/api/publico/ofertas/${ofertas[0].ofertaId}/disponibilidad`))
    exigir(Array.isArray(disponibilidad.dias), 'respuesta sin días')
    return
  }
  throw new Error('ningún módulo tiene ofertas activas')
})

const { HUMO_USUARIO: usuario, HUMO_CONTRASENA: contrasena, HUMO_TOTP: secretoTotp } = process.env
if (usuario && contrasena && secretoTotp) {
  await paso('entrada al panel con MFA', async () => {
    const entrar = await json(
      await pedir('/api/admin/auth/entrar', {
        method: 'POST',
        headers: cabecerasEscritura,
        body: JSON.stringify({ usuario, contrasena }),
      }),
    )
    exigir(entrar.siguientePaso === 'CodigoMfa', `siguiente paso inesperado: ${entrar.siguientePaso}`)
    const mfa = await pedir('/api/admin/auth/entrar/mfa', {
      method: 'POST',
      headers: cabecerasEscritura,
      body: JSON.stringify({ codigo: totp(secretoTotp), recordarEsteEquipo: false }),
    })
    exigir(mfa.status === 204, `el código MFA respondió HTTP ${mfa.status}`)
    await json(await pedir('/api/admin/auth/yo'))
    const salir = await pedir('/api/admin/auth/salir', { method: 'POST', headers: cabecerasEscritura })
    exigir(salir.status === 204, `salir respondió HTTP ${salir.status}`)
  })
} else {
  console.log('::warning::Sin HUMO_USUARIO, HUMO_CONTRASENA y HUMO_TOTP: se omite la entrada al panel.')
}

if (fallas > 0) {
  console.error(`${fallas} paso(s) fallaron.`)
  process.exit(1)
}
console.log('Prueba de humo correcta.')
