import { readFile } from 'node:fs/promises';
import { expect, test } from '@playwright/test';
import { revisarAccesibilidad } from '../ayudas/accesibilidad';
import { buscarLugar, reservarCita } from '../ayudas/api';
import { elegirDia, elegirHora } from '../ayudas/asistente';
import { carpetaCorreos, enlace, esperarCorreo, MOTIVO_SIN_BUZON } from '../ayudas/correos';
import { fechaLarga } from '../ayudas/fechas';
import { personaNueva } from '../ayudas/persona';

test.describe('Mi cita', () => {
  // Flujo 4 de docs/12: enlace del correo → Mi cita → comprobante → cancelar.
  test('el enlace del correo abre la cita, descarga el comprobante y la cancela', async ({ page, request }) => {
    test.skip(!carpetaCorreos(), MOTIVO_SIN_BUZON);
    const persona = personaNueva();
    // RN-13: con dos días de anticipación todavía se puede cancelar en línea.
    const lugar = await buscarLugar(request, { diasMinimos: 2 });
    // Reserva fuera del navegador: la única forma de entrar es el enlace del correo.
    const cita = await reservarCita(request, lugar.cupo.cupoId, persona);

    const confirmacion = await esperarCorreo(persona.correo, (c) => c.asunto.startsWith('Tu cita para la TIIE'));
    await page.goto(enlace(confirmacion, /\/mi-cita\/[\w-]{43}$/));
    // El servidor cambia el token por la cookie tiie_cita y quita el token de la URL (docs/07).
    await expect(page).toHaveURL(/\/mi-cita$/);
    await expect(page.getByRole('heading', { name: 'Tu cita' })).toBeVisible();
    await expect(page.getByText(cita.folio, { exact: true })).toBeVisible();

    const descarga = page.waitForEvent('download');
    await page.getByRole('link', { name: 'Descargar comprobante (PDF)' }).click();
    const pdf = await descarga;
    const contenido = await readFile(await pdf.path());
    expect(contenido.subarray(0, 5).toString('latin1')).toBe('%PDF-');

    await page.getByRole('button', { name: 'Cancelar cita' }).click();
    const dialogo = page.getByRole('dialog', { name: '¿Cancelar tu cita?' });
    await expect(dialogo).toBeVisible();
    await revisarAccesibilidad(page, 'Mi cita: cancelar');
    await dialogo.getByLabel('Motivo (opcional)').fill('Prueba extremo a extremo');
    await dialogo.getByRole('button', { name: 'Sí, cancelar mi cita' }).click();

    await expect(page.getByText('Tu cita fue cancelada. Te enviamos un correo de confirmación.')).toBeVisible();
    await expect(page.getByText('Cancelada', { exact: true })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Cancelar cita' })).toHaveCount(0);
    await esperarCorreo(persona.correo, (c) => c.asunto === `Tu cita ${cita.folio} fue cancelada`);
  });

  // Flujo 5: reprogramar desde Mi cita (RN-17).
  test('reprograma la cita a otro día y hora de la misma oferta', async ({ page }) => {
    const persona = personaNueva();
    const lugar = await buscarLugar(page.request, { diasMinimos: 2 });
    // Con page.request la cookie de Mi cita queda en el navegador, como al terminar el asistente.
    await reservarCita(page.request, lugar.cupo.cupoId, persona);
    const nuevo = await buscarLugar(page.request, {
      ofertaId: lugar.oferta.ofertaId,
      diasMinimos: 2,
      desdeElFinal: true,
      excluirFechas: [lugar.fecha],
    });

    await page.goto('/mi-cita');
    await expect(page.getByRole('heading', { name: 'Tu cita' })).toBeVisible();
    await expect(page.getByText(fechaLarga(lugar.fecha), { exact: true })).toBeVisible();
    await page.getByRole('button', { name: 'Cambiar fecha u hora' }).click();
    await expect(page.getByRole('heading', { name: 'Elige la nueva fecha y hora' })).toBeVisible();
    await revisarAccesibilidad(page, 'Mi cita: reprogramar');

    await elegirDia(page, nuevo.fecha);
    await elegirHora(page, nuevo.cupo.hora);
    await page.getByRole('button', { name: 'Confirmar el cambio' }).click();

    await expect(page.getByText('Tu cita cambió. Te enviamos un correo con el comprobante nuevo.')).toBeVisible();
    await expect(page.getByText(fechaLarga(nuevo.fecha), { exact: true })).toBeVisible();
    await expect(page.getByText(nuevo.cupo.hora, { exact: true })).toBeVisible();
    await expect(page.getByText('Agendada', { exact: true })).toBeVisible();
  });
});
