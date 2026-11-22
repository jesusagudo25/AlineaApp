import { test, expect, type Page } from '@playwright/test';
import { readFileSync } from 'node:fs';
const inboxKey = process.env.DEV_SMS_INBOX_KEY ?? readFileSync('../.env', 'utf8').match(/^DEV_SMS_INBOX_KEY=(.+)$/m)?.[1]?.trim();
if (!inboxKey) throw new Error('Falta la clave privada local de pruebas; no se imprime.');

test('registro, sesión persistida, cierre y login reales con SMS local; responsive', async ({ page }, info) => {
  const phone = '+99900000000' + (info.project.name === 'desktop' ? '1' : info.project.name === 'mobile' ? '2' : '3');
  const errors: string[] = []; page.on('pageerror', error => errors.push(error.message));
  await page.goto('/');
  await expect(page.getByRole('button', { name: 'Solicitar código' })).toBeVisible();
  await expect(page.getByText('Pruebas locales: no se envían SMS reales.')).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  const context = await page.locator('.access-context').boundingBox();
  const panel = await page.locator('.access-form-panel').boundingBox();
  if (info.project.name === 'desktop') {
    expect(context!.width).toBeGreaterThan(250);
    expect(context!.x + context!.width).toBeLessThanOrEqual(panel!.x);
  } else expect(context!.y + context!.height).toBeLessThanOrEqual(panel!.y);
  await page.screenshot({ path: `../docs/evidence/phase01/auth-${info.project.name}.png`, fullPage: true });
  await page.getByRole('button', { name: 'Registrarme' }).click();
  await page.getByLabel('Número de teléfono').fill(phone);
  await enterCode(page);
  await expect(page.getByRole('heading', { name: 'Completar registro' })).toBeFocused();
  await page.getByRole('button', { name: 'Completar registro', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Tu perfil' })).toBeVisible();
  await expect(page.getByText(phone, { exact: true })).toBeVisible();
  await page.reload(); await expect(page.getByRole('heading', { name: 'Tu perfil' })).toBeVisible();
  await page.getByRole('button', { name: 'Cerrar sesión' }).click();
  await expect(page.getByRole('button', { name: 'Solicitar código' })).toBeVisible();
  expect((await page.request.get('/api/auth/session')).status()).toBe(401);
  await page.getByRole('button', { name: 'Iniciar sesión', exact: true }).click();
  // Espera real: el reloj del navegador no altera el cooldown del servidor.
  await page.waitForTimeout(61000);
  await page.getByLabel('Número de teléfono').fill(phone);
  await enterCode(page);
  await expect(page.getByRole('heading', { name: 'Tu perfil' })).toBeVisible();
  await page.getByRole('button', { name: 'Cerrar sesión' }).click();
  expect(await page.evaluate(() => ({ local: localStorage.length, session: sessionStorage.length }))).toEqual({ local: 0, session: 0 });
  expect(errors).toEqual([]);
});

test('denegación uniforme, OTP inválido, temporizador y recuperación sin falsa sesión', async ({ page }) => {
  await page.goto('/'); await page.getByRole('button', { name: 'Registrarme' }).click();
  await page.getByLabel('Número de teléfono').fill('+999000009999');
  await page.getByRole('button', { name: 'Solicitar código' }).click();
  await expect(page.getByRole('status')).toHaveText('Si el número es elegible, recibirás un código.');
  await expect(page.getByRole('button', { name: /Reenviar en/ })).toBeDisabled();
  await page.getByLabel('Código de seis dígitos').fill('000000');
  await page.getByRole('button', { name: 'Verificar código', exact: true }).click();
  await expect(page.getByRole('alert')).toContainText('no son válidos');
  expect((await page.request.get('/api/auth/session')).status()).toBe(401);
  await page.getByRole('button', { name: 'Volver al teléfono' }).click();
  await expect(page.getByRole('heading', { name: 'Crear acceso' })).toBeFocused();
});

async function enterCode(page: Page) {
  const pending = page.waitForResponse(response => response.url().endsWith('/api/auth/otp/request'));
  await page.getByRole('button', { name: 'Solicitar código' }).click();
  const challenge: unknown = await (await pending).json();
  if (typeof challenge !== 'object' || challenge === null || !('challengeId' in challenge) || typeof challenge.challengeId !== 'string') throw new Error('Respuesta inválida.');
  const inbox = await page.request.get(`/api/development/sms/${challenge.challengeId}`, { headers: { 'X-Local-Sms-Inbox': inboxKey! } });
  if (!inbox.ok()) throw new Error('No se recibió el OTP en el buzón local.');
  const payload: unknown = await inbox.json();
  if (typeof payload !== 'object' || payload === null || !('code' in payload) || typeof payload.code !== 'string') throw new Error('OTP local inválido.');
  // Nunca capturar código, claves ni perfil en imágenes, trazas o logs.
  await page.getByLabel('Código de seis dígitos').fill(payload.code);
  await page.getByRole('button', { name: 'Verificar código', exact: true }).click();
}
