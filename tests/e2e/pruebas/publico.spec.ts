import { expect, test, type APIRequestContext, type TestInfo } from '@playwright/test';
import { revisarAccesibilidad } from '../ayudas/accesibilidad';
import {
  ajustarCupo,
  buscarLugar,
  cuposAdmin,
  reservarCita,
  type Lugar,
} from '../ayudas/api';
import {
  abrirPasoDatos,
  elegirDia,
  elegirHora,
  elegirOpcion,
  empiezaCon,
  esperarCaptcha,
  llenarDatos,
} from '../ayudas/asistente';
import { carpetaCorreos, enlace, esperarCorreo } from '../ayudas/correos';
import { personaNueva } from '../ayudas/persona';
import { apiPersonal, credenciales } from '../ayudas/personal';

test.describe('Sitio público', () => {
  test('las pantallas sin datos de cita no tienen hallazgos graves de accesibilidad', async ({ page }) => {
    await page.goto('/');
    await expect(page.getByRole('heading', { name: /Agenda tu cita para la Tarjeta Inteligente/ })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Sedes' })).toBeVisible();
    await revisarAccesibilidad(page, 'Inicio');

    await page.goto('/aviso-de-privacidad');
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
    await revisarAccesibilidad(page, 'Aviso de privacidad');

    await page.goto('/recuperar');
    await expect(page.getByRole('heading', { name: 'Recupera el enlace de tu cita' })).toBeVisible();
    await revisarAccesibilidad(page, 'Recuperar enlace');

    await page.goto('/mi-cita?error=enlace-invalido');
    await expect(page.getByRole('heading', { name: 'Este enlace ya no es válido' })).toBeVisible();
    await revisarAccesibilidad(page, 'Mi cita: enlace inválido');

    await page.goto('/una-pagina-que-no-existe');
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
    await revisarAccesibilidad(page, 'Página no encontrada');
  });

  // Flujo 1 de docs/12: sede, fecha, hora, datos y Mi cita con el aviso de cita nueva.
  test('reserva completa desde el inicio hasta Mi cita', async ({ page }) => {
    const lugar = await buscarLugar(page.request);
    const persona = personaNueva();

    await page.goto('/');
    await page.getByRole('link', { name: 'Agendar cita' }).click();
    await expect(page.getByRole('heading', { name: '¿Dónde quieres tu cita?' })).toBeVisible();
    await elegirOpcion(page.getByRole('radio', { name: lugar.oferta.municipio, exact: true }));
    await elegirOpcion(page.getByRole('radio', { name: empiezaCon(lugar.oferta.modulo) }));
    await elegirOpcion(page.getByRole('radio', { name: empiezaCon(lugar.oferta.tramite) }));
    await revisarAccesibilidad(page, 'Agendar: sede y trámite');
    await page.getByRole('button', { name: 'Continuar' }).click();

    await expect(page.getByRole('heading', { name: 'Elige el día' })).toBeVisible();
    await revisarAccesibilidad(page, 'Agendar: fecha');
    await elegirDia(page, lugar.fecha);

    await expect(page.getByRole('heading', { name: 'Elige la hora' })).toBeVisible();
    await revisarAccesibilidad(page, 'Agendar: hora');
    await elegirHora(page, lugar.cupo.hora);
    await page.getByRole('button', { name: 'Continuar' }).click();

    await expect(page.getByRole('heading', { name: 'Tus datos' })).toBeVisible();
    await llenarDatos(page, persona);
    await esperarCaptcha(page);
    await revisarAccesibilidad(page, 'Agendar: tus datos');
    await page.getByRole('button', { name: 'Agendar cita' }).click();

    await expect(page).toHaveURL(/\/mi-cita\?nueva=1$/);
    await expect(page.getByRole('heading', { name: 'Tu cita' })).toBeVisible();
    await expect(page.getByText('Tu cita quedó agendada')).toBeVisible();
    await expect(page.getByText(lugar.cupo.hora, { exact: true })).toBeVisible();
    await expect(page.getByText(`CURP ${persona.curp.slice(0, 4)}`)).toBeVisible();
    await revisarAccesibilidad(page, 'Mi cita');
  });

  // Flujo 2: el horario se llena mientras la persona escribe sus datos.
  test('si el horario se llena a mitad del flujo, avisa y regresa al paso de la hora', async ({
    page,
    request,
  }, testInfo) => {
    const lugar = await buscarLugar(page.request, { diasMinimos: 2 });
    await abrirPasoDatos(page, lugar);
    await llenarDatos(page, personaNueva());
    await esperarCaptcha(page);

    const restaurar = await llenarCupo(testInfo, request, lugar);
    try {
      await page.getByRole('button', { name: 'Agendar cita' }).click();
      await expect(page.getByRole('heading', { name: 'Elige la hora' })).toBeVisible();
      await expect(page.getByText('Ese horario se acaba de llenar. Elige otro.')).toBeVisible();
      await expect(
        page
          .getByRole('group', { name: /^Horas con lugar/ })
          .locator('label')
          .filter({ hasText: new RegExp(`^${lugar.cupo.hora}`) }),
      ).toHaveCount(0);
    } finally {
      await restaurar();
    }
  });

  // Flujo 3: RN-01 en el asistente y el camino a recuperar el enlace.
  test('una CURP con cita activa recibe la oferta de recuperar su enlace', async ({ page, request }) => {
    const persona = personaNueva();
    const primera = await buscarLugar(request);
    await reservarCita(request, primera.cupo.cupoId, persona);

    const segunda = await buscarLugar(page.request, { desdeElFinal: true });
    await abrirPasoDatos(page, segunda);
    await llenarDatos(page, { ...persona, correo: `otro.${persona.correo}` });
    await esperarCaptcha(page);
    await page.getByRole('button', { name: 'Agendar cita' }).click();

    await expect(page.getByText('Esta CURP ya tiene una cita agendada.')).toBeVisible();
    await page.getByRole('button', { name: 'Recibir mi enlace por correo' }).click();

    await expect(page.getByRole('heading', { name: 'Recupera el enlace de tu cita' })).toBeVisible();
    // La CURP llega por el estado de navegación, nunca por la URL (docs/11#datos-personales).
    await expect(page.getByLabel('CURP')).toHaveValue(persona.curp);
    expect(page.url()).not.toContain(persona.curp);
    await page.getByLabel('Correo electrónico').fill(persona.correo);
    await esperarCaptcha(page);
    await page.getByRole('button', { name: 'Enviarme el enlace' }).click();
    await expect(page.getByRole('heading', { name: 'Revisa tu correo' })).toBeVisible();

    if (carpetaCorreos()) {
      const correo = await esperarCorreo(persona.correo, (c) => c.asunto === 'Enlace para consultar tu cita');
      expect(enlace(correo, /\/mi-cita\/[\w-]{43}$/)).toMatch(/^\/mi-cita\//);
    }
  });
});

/**
 * Deja el cupo sin lugares. Con sesión del personal baja la capacidad a los ocupados más uno y reserva ese
 * lugar (una sola reserva, por el límite de reservas por IP); sin ella, reserva todos los lugares libres.
 * Devuelve cómo restaurar la capacidad.
 */
async function llenarCupo(
  testInfo: TestInfo,
  request: APIRequestContext,
  lugar: Lugar,
): Promise<() => Promise<void>> {
  if (!credenciales()) {
    for (let i = 0; i < lugar.cupo.libres; i++) await reservarCita(request, lugar.cupo.cupoId, personaNueva());
    return async () => {};
  }

  const admin = await apiPersonal(testInfo);
  const delCupo = async () =>
    (await cuposAdmin(admin, lugar.oferta.ofertaId, lugar.fecha)).find((c) => c.cupoId === lugar.cupo.cupoId)!;
  const original = await delCupo();
  await ajustarCupo(admin, original, { capacidad: original.ocupados + 1 });
  await reservarCita(request, lugar.cupo.cupoId, personaNueva());
  return async () => {
    await ajustarCupo(admin, await delCupo(), { capacidad: original.capacidad });
    await admin.dispose();
  };
}
