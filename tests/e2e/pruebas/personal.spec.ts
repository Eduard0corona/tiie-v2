import { randomUUID } from 'node:crypto';
import { expect, test, type APIRequestContext, type Browser, type TestInfo } from '@playwright/test';
import {
  CSRF,
  citaVentanilla,
  cuposAdmin,
  disponibilidad,
  eliminarBloqueo,
  guardarPlantilla,
  hoySegun,
  json,
  leerPlantilla,
  ofertasPublicas,
  reservarCita,
  buscarLugar,
  type OfertaPublica,
  type PlantillaSemanal,
} from '../ayudas/api';
import { sinDesbordeHorizontal } from '../ayudas/accesibilidad';
import { diaDelCalendario } from '../ayudas/asistente';
import { carpetaCorreos, enlace, esperarCorreo, MOTIVO_SIN_BUZON } from '../ayudas/correos';
import { diaSemana, etiquetaDia } from '../ayudas/fechas';
import { personaNueva } from '../ayudas/persona';
import { apiPersonal, credenciales, entrarAlPanel, MOTIVO_SIN_PERSONAL, RUTA_SESION } from '../ayudas/personal';

const NOMBRES_DIA = ['domingo', 'lunes', 'martes', 'miércoles', 'jueves', 'viernes', 'sábado'];

test.beforeEach(() => {
  test.skip(!credenciales(), MOTIVO_SIN_PERSONAL);
});

test.describe('Personal: agenda del día', () => {
  // Esta prueba entra por la interfaz; las demás usan la sesión de la preparación.
  test.use({ storageState: { cookies: [], origins: [] } });

  // Flujo 6 de docs/12.
  test('entra con MFA, abre la agenda del día y marca la asistencia', async ({ page, request }, testInfo) => {
    const admin = await apiPersonal(testInfo);
    try {
      const preparada = await citaDeHoy(admin, request);
      test.skip('motivo' in preparada, 'motivo' in preparada ? preparada.motivo : '');
      const { folio, moduloId } = preparada as { folio: string; moduloId: number };

      await entrarAlPanel(page, credenciales()!);
      await page.getByRole('combobox', { name: 'Módulo' }).selectOption(String(moduloId));
      const fila = page.getByRole('row').filter({ hasText: folio });
      await expect(fila).toBeVisible();
      await fila.getByRole('button', { name: `Atendida: ${folio}` }).click();

      await expect(page.getByText(`Cita ${folio}: Atendida.`)).toBeVisible();
      await expect(fila.getByText('Atendida', { exact: true })).toBeVisible();
      await expect(fila.getByRole('button', { name: `Atendida: ${folio}` })).toHaveCount(0);
    } finally {
      await admin.dispose();
    }
  });
});

test.describe('Personal: supervisión y administración', () => {
  test.use({ storageState: RUTA_SESION });

  // Flujo 7: editar una plantilla y ver el cambio en la disponibilidad pública.
  test('una hora nueva en la plantilla aparece en la disponibilidad pública', async ({ page, request }, testInfo) => {
    const admin = await apiPersonal(testInfo);
    const elegida = await ofertaConDiaSinServicio(admin, request);
    test.skip(!elegida, 'Ninguna oferta tiene un día de la semana sin servicio para agregarle horas.');
    const { oferta, dia, fecha, original } = elegida!;
    const asistente = `/agendar?municipio=${oferta.municipioId}&modulo=${oferta.moduloId}&oferta=${oferta.ofertaId}&paso=fecha`;

    try {
      await page.goto(asistente);
      await expect(await diaDelCalendario(page, fecha)).toHaveAccessibleName(`${etiquetaDia(fecha)}, sin servicio`);

      await page.goto(`/admin/ofertas/${oferta.ofertaId}`);
      await expect(page.getByRole('heading', { name: 'Plantilla semanal' })).toBeVisible();
      const nombre = NOMBRES_DIA[dia]!;
      await page.getByRole('button', { name: `Agregar hora al ${nombre}` }).click();
      await page.getByLabel(`Hora 1 del ${nombre}`, { exact: true }).fill('10:00');
      await page.getByLabel(`Turnos de la hora 1 del ${nombre}`, { exact: true }).fill('2');
      await page.getByRole('button', { name: 'Guardar plantilla' }).click();
      await expect(page.getByText('La plantilla se guardó y los cupos se regeneraron.')).toBeVisible();

      await page.goto(asistente);
      await expect(await diaDelCalendario(page, fecha)).toHaveAccessibleName(
        `${etiquetaDia(fecha)}, alta disponibilidad`,
      );
    } finally {
      await guardarPlantilla(admin, oferta.ofertaId, original);
      await admin.dispose();
    }
  });

  // Flujo 8: bloqueo con citas afectadas y cancelación con aviso.
  test('un bloqueo con citas afectadas las cancela y avisa a cada persona', async ({ page, request }, testInfo) => {
    const admin = await apiPersonal(testInfo);
    const persona = personaNueva();
    const lugar = await buscarLugar(request, { desdeElFinal: true, diasMinimos: 3 });
    const cita = await reservarCita(request, lugar.cupo.cupoId, persona);
    const motivo = `Prueba E2E ${randomUUID().slice(0, 8)}`;
    let bloqueoId: number | null = null;

    try {
      await page.goto('/admin/bloqueos');
      const formulario = page.getByRole('form', { name: 'Nuevo bloqueo' });
      await formulario.getByRole('radio', { name: 'Oferta' }).check();
      await formulario.getByLabel('Desde').fill(lugar.fecha);
      await formulario.getByLabel('Hasta').fill(lugar.fecha);
      await formulario.getByRole('combobox', { name: 'Módulo' }).selectOption(String(lugar.oferta.moduloId));
      const tramite = formulario.getByRole('combobox', { name: 'Trámite' });
      await expect(tramite.locator(`option[value="${lugar.oferta.ofertaId}"]`)).toBeAttached();
      await tramite.selectOption(String(lugar.oferta.ofertaId));
      await formulario.getByLabel('Motivo').fill(motivo);

      const creado = page.waitForResponse(
        (r) => r.url().endsWith('/api/admin/bloqueos') && r.request().method() === 'POST',
      );
      await formulario.getByRole('button', { name: 'Crear bloqueo' }).click();
      bloqueoId = ((await (await creado).json()) as { bloqueoId: number }).bloqueoId;

      await expect(page.getByText('El bloqueo se creó.')).toBeVisible();
      const afectadas = page.getByRole('table', { name: 'Citas afectadas por el bloqueo' });
      await expect(afectadas.getByRole('link', { name: cita.folio })).toBeVisible();
      await sinDesbordeHorizontal(page, 'Bloqueos: citas afectadas');

      await page.getByRole('button', { name: 'Cancelar y avisar' }).click();
      const dialogo = page.getByRole('dialog', { name: '¿Cancelar las citas afectadas?' });
      await dialogo.getByLabel('Motivo para las personas').fill('Cierre del módulo por una prueba automatizada.');
      await dialogo.getByRole('button', { name: 'Cancelar y avisar' }).click();
      await expect(page.getByText(/^Se cancel(ó|aron) \d+ citas? y se avisó/)).toBeVisible();

      const detalle = await json<{ estado: string }>(
        await admin.get(`/api/admin/citas/${await citaIdDe(admin, cita.folio)}`),
        'detalle de la cita',
      );
      expect(detalle.estado).toBe('Cancelada');
      if (carpetaCorreos()) {
        await esperarCorreo(persona.correo, (c) => c.asunto === 'Tu cita fue cancelada por cierre del módulo');
      }
    } finally {
      // Reabre los cupos del día para no afectar otras pruebas.
      if (bloqueoId !== null) await eliminarBloqueo(admin, bloqueoId);
      await admin.dispose();
    }
  });

  // Flujo 9: crear usuario, activar la cuenta con el correo y configurar MFA.
  test('el administrador crea un usuario que activa su cuenta y configura MFA', async ({
    page,
    browser,
    request,
  }, testInfo) => {
    test.skip(!carpetaCorreos(), MOTIVO_SIN_BUZON);
    const admin = await apiPersonal(testInfo);
    const id = randomUUID().slice(0, 8);
    const usuario = `e2e.${id}`;
    const correo = `e2e.${id}@example.com`;
    const contrasena = `Prueba-extremo-${randomUUID()}`;
    const [oferta] = await ofertasPublicas(request);

    try {
      await page.goto('/admin/usuarios/nuevo');
      await page.getByLabel('Usuario', { exact: true }).fill(usuario);
      await page.getByLabel('Correo', { exact: true }).fill(correo);
      await page.getByLabel('Nombre o nombres').fill('Persona');
      await page.getByLabel('Primer apellido').fill('De Prueba');
      await page.getByRole('combobox', { name: 'Rol' }).selectOption('Capturista');
      await page.getByRole('checkbox', { name: oferta!.modulo }).check();
      await page.getByRole('button', { name: 'Crear usuario y enviar invitación' }).click();
      await expect(page.getByText(`La cuenta se creó y se envió la invitación a ${correo}.`)).toBeVisible();

      const invitacion = await esperarCorreo(correo, (c) => c.asunto === 'Activa tu cuenta del panel de citas TIIE');
      const nueva = await contextoSinSesion(browser, testInfo);
      try {
        const persona = await nueva.newPage();
        await persona.goto(enlace(invitacion, /\/admin\/activar\?usuarioId=\d+&token=/));
        await expect(persona.getByRole('heading', { name: 'Activa tu cuenta' })).toBeVisible();
        await persona.getByLabel('Contraseña nueva').fill(contrasena);
        await persona.getByLabel('Confirma la contraseña').fill(contrasena);
        await persona.getByRole('button', { name: 'Activar cuenta' }).click();
        await expect(persona.getByText('Tu cuenta quedó activa. Ya puedes entrar.')).toBeVisible();

        // Primera entrada: el panel exige configurar la app autenticadora antes de mostrar la agenda.
        await entrarAlPanel(persona, { usuario, contrasena }, false);
        await expect(persona.getByRole('combobox', { name: 'Módulo' })).toHaveValue(String(oferta!.moduloId));
      } finally {
        await nueva.close();
      }
    } finally {
      // La cuenta de prueba queda desactivada.
      const lista = await json<{ elementos: { usuarioId: number; usuario: string }[] }>(
        await admin.get(`/api/admin/usuarios?busqueda=${encodeURIComponent(usuario)}`),
        'búsqueda de usuarios',
      );
      for (const u of lista.elementos.filter((x) => x.usuario === usuario)) {
        await admin.post(`/api/admin/usuarios/${u.usuarioId}/activacion`, { headers: CSRF, data: { activo: false } });
      }
      await admin.dispose();
    }
  });
});

/**
 * Cita en ventanilla para hoy (RN-18) en la última hora con lugar. La asistencia se marca solo en citas de hoy
 * o anteriores, y no hay cita de hoy si el sitio no generó cupos ayer: GeneracionCupos crea desde mañana.
 */
async function citaDeHoy(
  admin: APIRequestContext,
  publico: APIRequestContext,
): Promise<{ folio: string; moduloId: number } | { motivo: string }> {
  const ofertas = await ofertasPublicas(publico);
  const hoy = hoySegun(await disponibilidad(publico, ofertas[0]!.ofertaId));
  let hayCuposDeHoy = false;
  for (const oferta of ofertas) {
    const cupos = (await cuposAdmin(admin, oferta.ofertaId, hoy))
      .filter((c) => !c.cerrado && c.ocupados < c.capacidad)
      .sort((a, b) => b.hora.localeCompare(a.hora));
    const ultimo = cupos[0];
    if (!ultimo) continue;
    hayCuposDeHoy = true;
    const respuesta = await citaVentanilla(admin, ultimo.cupoId, personaNueva());
    if (respuesta.status() === 201) {
      const { folio } = (await respuesta.json()) as { folio: string };
      return { folio, moduloId: oferta.moduloId };
    }
    // FueraDeVentana: la última hora de hoy ya pasó; las demás también.
    expect(respuesta.status(), await respuesta.text()).toBe(422);
  }
  return {
    motivo: hayCuposDeHoy
      ? 'Ya pasó la última hora de hoy: la asistencia solo se marca en citas de hoy o anteriores.'
      : 'No hay cupos de hoy con lugar: GeneracionCupos crea desde mañana, así que una base recién creada no los tiene.',
  };
}

async function ofertaConDiaSinServicio(
  admin: APIRequestContext,
  publico: APIRequestContext,
): Promise<{ oferta: OfertaPublica; dia: number; fecha: string; original: PlantillaSemanal } | null> {
  for (const oferta of await ofertasPublicas(publico)) {
    const original = await leerPlantilla(admin, oferta.ofertaId);
    const dia = [0, 1, 2, 3, 4, 5, 6].find(
      (d) => !original.dias.some((x) => x.diaSemana === d && x.horas.length > 0),
    );
    if (dia === undefined) continue;
    const d = await disponibilidad(publico, oferta.ofertaId);
    const libre = d.dias.find((x) => diaSemana(x.fecha) === dia && x.estado === 'SinServicio');
    if (libre) return { oferta, dia, fecha: libre.fecha, original };
  }
  return null;
}

async function citaIdDe(admin: APIRequestContext, folio: string): Promise<number> {
  const lista = await json<{ elementos: { citaId: number; folio: string }[] }>(
    await admin.get(`/api/admin/citas?busqueda=${encodeURIComponent(folio)}`),
    'búsqueda de citas',
  );
  const cita = lista.elementos.find((c) => c.folio === folio);
  expect(cita, `No se encontró la cita ${folio}.`).toBeTruthy();
  return cita!.citaId;
}

/** Contexto nuevo con el mismo dispositivo del proyecto y sin la sesión del administrador. */
function contextoSinSesion(browser: Browser, testInfo: TestInfo) {
  const { baseURL, ignoreHTTPSErrors, viewport, userAgent, deviceScaleFactor, isMobile, hasTouch, locale, timezoneId } =
    testInfo.project.use;
  return browser.newContext({
    baseURL,
    ignoreHTTPSErrors,
    viewport,
    userAgent,
    deviceScaleFactor,
    isMobile,
    hasTouch,
    locale,
    timezoneId,
  });
}
