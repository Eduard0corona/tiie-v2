// Puerta de cobertura (docs/12-pruebas.md#puertas-del-ci). Une los reportes Cobertura de todos los
// proyectos de prueba —una línea cuenta como cubierta si algún proyecto la ejecutó— y compara cada
// ensamblado con su mínimo.
//
//   node tests/verificar-cobertura.mjs artifacts/cobertura Tiie.Dominio=90 Tiie.Aplicacion=80
import { readdirSync, readFileSync, appendFileSync } from 'node:fs'
import { join } from 'node:path'

const [carpeta, ...minimos] = process.argv.slice(2)
if (!carpeta || minimos.length === 0) {
  console.error('Uso: node tests/verificar-cobertura.mjs <carpeta> <Ensamblado>=<porcentaje>...')
  process.exit(2)
}

function* archivos(dir) {
  for (const entrada of readdirSync(dir, { withFileTypes: true })) {
    const ruta = join(dir, entrada.name)
    if (entrada.isDirectory()) yield* archivos(ruta)
    else if (entrada.name.endsWith('.cobertura.xml')) yield ruta
  }
}

/** ensamblado → archivo fuente → número de línea → cubierta */
const lineas = new Map()
let reportes = 0
for (const ruta of archivos(carpeta)) {
  reportes++
  const xml = readFileSync(ruta, 'utf8')
  for (const paquete of xml.matchAll(/<package\b[^>]*\bname="([^"]+)"[^>]*>([\s\S]*?)<\/package>/g)) {
    const ensamblado = paquete[1].replace(/\.dll$/i, '')
    if (!lineas.has(ensamblado)) lineas.set(ensamblado, new Map())
    const porArchivo = lineas.get(ensamblado)
    for (const clase of paquete[2].matchAll(/<class\b[^>]*\bfilename="([^"]+)"[^>]*>([\s\S]*?)<\/class>/g)) {
      if (!porArchivo.has(clase[1])) porArchivo.set(clase[1], new Map())
      const porLinea = porArchivo.get(clase[1])
      for (const linea of clase[2].matchAll(/<line\b[^>]*\bnumber="(\d+)"[^>]*\bhits="(\d+)"/g))
        porLinea.set(linea[1], porLinea.get(linea[1]) === true || Number(linea[2]) > 0)
    }
  }
}

if (reportes === 0) {
  console.error(`No hay reportes *.cobertura.xml en ${carpeta}.`)
  process.exit(1)
}

const resumen = ['| Ensamblado | Cobertura de líneas | Mínimo |', '| --- | --- | --- |']
let falla = false
for (const minimo of minimos) {
  const [ensamblado, porcentaje] = minimo.split('=')
  const porArchivo = lineas.get(ensamblado)
  if (!porArchivo) {
    console.error(`${ensamblado}: no aparece en los reportes.`)
    falla = true
    continue
  }
  let total = 0
  let cubiertas = 0
  for (const porLinea of porArchivo.values())
    for (const cubierta of porLinea.values()) {
      total++
      if (cubierta) cubiertas++
    }
  const cobertura = total === 0 ? 100 : (100 * cubiertas) / total
  const pasa = cobertura >= Number(porcentaje)
  if (!pasa) falla = true
  console.log(`${pasa ? 'ok   ' : 'FALLA'} ${ensamblado}: ${cobertura.toFixed(1)} % (${cubiertas}/${total} líneas), mínimo ${porcentaje} %`)
  resumen.push(`| ${ensamblado} | ${cobertura.toFixed(1)} % | ${porcentaje} % |`)
}

if (process.env.GITHUB_STEP_SUMMARY)
  appendFileSync(process.env.GITHUB_STEP_SUMMARY, `### Cobertura\n\n${resumen.join('\n')}\n`)
process.exit(falla ? 1 : 0)
