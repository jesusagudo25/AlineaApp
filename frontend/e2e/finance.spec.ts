import { test, expect, type Page } from '@playwright/test';
import { readFileSync } from 'node:fs';
const inboxKey = readFileSync('../.env', 'utf8').match(/^DEV_SMS_INBOX_KEY=(.+)$/m)?.[1]?.trim();
if (!inboxKey) throw new Error('Falta clave privada del buzón local; valor omitido.');

test('bancos, cuentas y categorías persistidos, relaciones, errores y desactivación', async ({ page }, info) => {
  const errors: string[] = []; page.on('pageerror', error => errors.push(error.message));
  await register(page, info.project.name === 'desktop' ? '+999000000201' : '+999000000202');
  await expect(page.getByRole('heading', { name: 'Cuentas', exact: true })).toBeVisible();
  await expect(page.getByText('No hay cuentas activas. Añade la primera para comenzar.')).toBeVisible();
  await page.getByRole('button', { name: 'Bancos', exact: true }).click();
  await page.getByRole('button', { name: 'Añadir banco', exact: true }).click();
  await expect(page.getByLabel('Nombre', { exact: true })).toBeFocused();
  await page.getByRole('button', { name: 'Guardar', exact: true }).click();
  await expect(page.getByRole('alert')).toContainText('Introduce un nombre');
  await page.getByLabel('Nombre', { exact: true }).fill('Banco de ensayo'); await save(page);
  await expect(page.getByRole('heading', { name: 'Banco de ensayo' })).toBeVisible();
  await page.getByRole('button', { name: 'Añadir banco', exact: true }).click();
  await page.getByLabel('Nombre', { exact: true }).fill('banco de ensayo'); await page.getByRole('button', { name: 'Guardar', exact: true }).click();
  await expect(page.getByRole('alert')).toContainText('Ya existe'); await page.getByRole('button', { name: 'Cancelar', exact: true }).click();

  await page.getByRole('button', { name: 'Cuentas', exact: true }).click();
  await page.getByRole('button', { name: 'Añadir cuenta', exact: true }).click();
  await page.getByLabel('Nombre', { exact: true }).fill('Cuenta de ensayo');
  await page.getByLabel('Tipo', { exact: true }).selectOption('bank');
  await page.getByLabel('Banco', { exact: true }).selectOption({ label: 'Banco de ensayo' });
  await page.getByLabel('Saldo inicial (USD)').fill('120.101');
  await page.getByLabel('Fecha de apertura').fill('2026-11-22');
  await page.getByRole('button', { name: 'Guardar', exact: true }).click(); await expect(page.getByRole('alert')).toContainText('dos decimales');
  await page.getByLabel('Saldo inicial (USD)').fill('120.10'); await save(page);
  await expect(page.getByText('$120.10 USD', { exact: true })).toHaveCount(2);
  await page.getByRole('button', { name: 'Editar Cuenta de ensayo' }).click();
  await page.getByLabel('Saldo inicial (USD)').fill('140.25'); await page.getByRole('button', { name: 'Guardar', exact: true }).click();
  await expect(page.getByRole('alert')).toContainText('Confirma explícitamente');
  await page.getByLabel('Confirmo cualquier cambio').check(); await save(page);
  await page.getByRole('button', { name: 'Añadir cuenta', exact: true }).click();
  await page.getByLabel('Nombre', { exact: true }).fill('Efectivo de ensayo');
  await page.getByLabel('Saldo inicial (USD)').fill('25.00'); await page.getByLabel('Fecha de apertura').fill('2026-11-22');
  await page.screenshot({ path: `../docs/evidence/phase02b/account-form-${info.project.name}.png`, fullPage: true, mask: [page.locator('.session-header p')] });
  await save(page);
  await page.reload(); await expect(page.getByRole('heading', { name: 'Cuenta de ensayo' })).toBeVisible();
  await expect(page.getByText('$140.25 USD', { exact: true })).toHaveCount(2);
  await page.screenshot({ path: `../docs/evidence/phase02b/accounts-${info.project.name}.png`, fullPage: true, mask: [page.locator('.session-header p')] });

  await page.getByRole('button', { name: 'Bancos', exact: true }).click(); await page.getByRole('button', { name: 'Editar Banco de ensayo' }).click();
  await page.getByLabel('Banco activo', { exact: true }).uncheck(); await page.getByRole('button', { name: 'Guardar', exact: true }).click();
  await expect(page.getByRole('alert')).toContainText('cuentas activas'); await page.getByRole('button', { name: 'Cancelar', exact: true }).click();
  await page.getByRole('button', { name: 'Cuentas', exact: true }).click(); await page.getByRole('button', { name: 'Editar Cuenta de ensayo' }).click();
  await page.getByLabel('Cuenta activa', { exact: true }).uncheck(); await save(page);
  await expect(page.getByRole('heading', { name: 'Cuenta de ensayo' })).toHaveCount(0);
  await page.getByLabel('Mostrar inactivos').check(); await expect(page.getByRole('heading', { name: 'Cuenta de ensayo' })).toBeVisible();
  await page.getByRole('button', { name: 'Bancos', exact: true }).click(); await page.getByRole('button', { name: 'Editar Banco de ensayo' }).click();
  await page.getByLabel('Banco activo', { exact: true }).uncheck(); await save(page);

  await page.getByRole('button', { name: 'Categorías', exact: true }).click();
  await page.getByRole('button', { name: 'Añadir categorías iniciales', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Salario', exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Añadir categoría', exact: true }).click();
  await page.getByLabel('Nombre', { exact: true }).fill('Categoría de ensayo'); await save(page);
  await page.getByRole('button', { name: 'Editar Categoría de ensayo' }).click();
  await page.getByLabel('Nombre', { exact: true }).fill('Categoría editada'); await page.getByLabel('Tipo', { exact: true }).selectOption('income'); await save(page);
  await page.getByRole('button', { name: 'Editar Categoría editada' }).click(); await page.getByLabel('Categoría activa', { exact: true }).uncheck(); await save(page);
  await page.getByLabel('Mostrar inactivos').check(); await expect(page.getByRole('heading', { name: 'Categoría editada' })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await page.screenshot({ path: `../docs/evidence/phase02b/categories-${info.project.name}.png`, fullPage: true, mask: [page.locator('.session-header p')] });
  expect(errors).toEqual([]);
  await page.getByRole('button', { name: 'Cerrar sesión' }).click(); await expect(page.getByRole('button', { name: 'Solicitar código' })).toBeVisible();
  expect((await page.request.get('/api/finance/accounts')).status()).toBe(401);
});

async function save(page: Page) {
  await page.getByRole('button', { name: 'Guardar', exact: true }).click();
  await expect(page.getByRole('status').filter({ hasText: 'Cambios guardados.' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Guardar', exact: true })).toHaveCount(0);
  await expect(page.getByRole('status').filter({ hasText: /^Cargando/ })).toHaveCount(0);
}
async function register(page: Page, phone: string) {
  await page.goto('/'); await page.getByRole('button', { name: 'Registrarme', exact: true }).click();
  await page.getByLabel('Número de teléfono').fill(phone);
  const pending = page.waitForResponse(response => response.url().endsWith('/api/auth/otp/request'));
  await page.getByRole('button', { name: 'Solicitar código' }).click();
  const response: unknown = await (await pending).json();
  if (!response || typeof response !== 'object' || !('challengeId' in response) || typeof response.challengeId !== 'string') throw new Error('Desafío no válido.');
  const inbox = await page.request.get(`/api/development/sms/${response.challengeId}`, { headers: { 'X-Local-Sms-Inbox': inboxKey! } });
  if (!inbox.ok()) throw new Error('No existe código local para la prueba.');
  const value: unknown = await inbox.json();
  if (!value || typeof value !== 'object' || !('code' in value) || typeof value.code !== 'string') throw new Error('Código local no válido.');
  await page.getByLabel('Código de seis dígitos').fill(value.code); await page.getByRole('button', { name: 'Verificar código', exact: true }).click();
  await page.getByRole('button', { name: 'Completar registro', exact: true }).click();
}
