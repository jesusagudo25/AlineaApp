import { test, expect, type Page } from '@playwright/test';
import { readFileSync } from 'node:fs';
const inboxKey = readFileSync('../.env', 'utf8').match(/^DEV_SMS_INBOX_KEY=(.+)$/m)?.[1]?.trim();
if (!inboxKey) throw new Error('Falta clave privada del buzón local; valor omitido.');

test('movimientos reales, saldos, filtros, edición y anulación con trazabilidad', async ({ page }, info) => {
  const errors: string[] = []; page.on('pageerror', error => errors.push(error.message));
  const today = new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Panama', year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date());
  await register(page, info.project.name === 'desktop' ? '+999000000301' : '+999000000302');
  await expect(page.getByRole('heading', { name: 'Cuentas', exact: true })).toBeVisible();
  for (const [name, amount] of [['Caja principal', '100.00'], ['Caja reserva', '20.00']]) {
    await page.getByRole('button', { name: 'Añadir cuenta', exact: true }).click(); await page.getByLabel('Nombre', { exact: true }).fill(name!);
    await page.getByLabel('Saldo inicial (USD)').fill(amount!); await page.getByLabel('Fecha de apertura').fill(today);
    await page.getByRole('button', { name: 'Guardar', exact: true }).click(); await expect(page.getByRole('heading', { name: name!, exact: true })).toBeVisible();
  }
  await page.getByRole('button', { name: 'Categorías', exact: true }).click(); await page.getByRole('button', { name: 'Añadir categorías iniciales', exact: true }).click(); await expect(page.getByRole('heading', { name: 'Salario', exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Movimientos', exact: true }).click(); await expect(page.getByText('No hay movimientos para estos filtros.')).toBeVisible();
  await page.getByRole('button', { name: 'Registrar gasto', exact: true }).click();
  await page.getByRole('button', { name: 'Guardar movimiento', exact: true }).click(); await expect(page.getByRole('alert')).toContainText('Revisa importe');
  await page.getByLabel('Cuenta de origen').selectOption({ label: 'Caja principal' }); await page.getByLabel('Categoría del movimiento').selectOption({ label: 'Alimentación' });
  await page.getByLabel('Importe (USD)', { exact: true }).fill('10.01'); await page.getByLabel('Descripción', { exact: true }).fill('Gasto de ensayo');
  await page.screenshot({ path: `../docs/evidence/phase02c/movement-form-${info.project.name}.png`, fullPage: true, mask: [page.locator('.session-header p')] });
  await saveMovement(page);
  await page.getByRole('button', { name: 'Nuevo movimiento', exact: true }).click();
  await page.getByLabel('Tipo de movimiento').selectOption('income'); await page.getByLabel('Cuenta de origen').selectOption({ label: 'Caja principal' }); await page.getByLabel('Categoría del movimiento').selectOption({ label: 'Salario' });
  await page.getByLabel('Importe (USD)', { exact: true }).fill('50.10'); await page.getByLabel('Descripción', { exact: true }).fill('Ingreso de ensayo'); await saveMovement(page);
  await page.getByRole('button', { name: 'Nuevo movimiento', exact: true }).click(); await page.getByLabel('Tipo de movimiento').selectOption('transfer');
  await expect(page.getByLabel('Categoría del movimiento')).toHaveCount(0);
  await page.getByLabel('Cuenta de origen').selectOption({ label: 'Caja principal' }); await page.getByLabel('Cuenta de destino').selectOption({ label: 'Caja reserva' });
  await page.getByLabel('Importe (USD)', { exact: true }).fill('30.00'); await page.getByLabel('Descripción', { exact: true }).fill('Transferencia de ensayo'); await saveMovement(page);
  await expect(page.getByText('Ingresos: $50.10 USD · Gastos: $10.01 USD.', { exact: false })).toBeVisible();
  await page.getByLabel('Filtrar tipo').selectOption('transfer'); await page.getByRole('button', { name: 'Aplicar filtros' }).click();
  await expect(page.getByText('Página 1 · 1 movimientos', { exact: true })).toBeVisible(); await expect(page.getByText('Ingresos: $0.00 USD · Gastos: $0.00 USD.', { exact: false })).toBeVisible();
  await page.getByRole('button', { name: 'Limpiar filtros' }).click(); await expect(page.getByText('Página 1 · 3 movimientos', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Detalle Gasto de ensayo', exact: true }).click(); await page.getByRole('button', { name: 'Editar movimiento', exact: true }).click();
  await page.getByLabel('Importe (USD)', { exact: true }).fill('12.34'); await page.getByLabel('Motivo de edición').fill('Corrección comprobada'); await saveMovement(page);
  await page.getByRole('button', { name: 'Detalle Gasto de ensayo', exact: true }).click(); await expect(page.getByText(/Edición · Corrección comprobada/)).toBeVisible();
  await page.getByLabel('Motivo de anulación').fill('Ensayo de reversión'); await page.getByRole('button', { name: 'Confirmar anulación', exact: true }).click();
  await expect(page.getByRole('status').filter({ hasText: 'Movimiento anulado' })).toBeVisible();
  await expect(page.getByRole('heading', { name: /Gasto.*Anulado/ })).toBeVisible();
  await page.reload(); await page.getByRole('button', { name: 'Movimientos', exact: true }).click(); await page.getByRole('button', { name: 'Detalle Gasto de ensayo', exact: true }).click();
  await expect(page.getByText(/Anulación · Ensayo de reversión/)).toBeVisible(); await expect(page.getByRole('button', { name: 'Editar movimiento', exact: true })).toHaveCount(0);
  await page.screenshot({ path: `../docs/evidence/phase02c/history-${info.project.name}.png`, fullPage: true, mask: [page.locator('.session-header p')] });
  await page.getByRole('button', { name: 'Catálogos', exact: true }).click();
  const main = page.getByRole('listitem').filter({ has: page.getByRole('heading', { name: 'Caja principal', exact: true }) });
  await expect(main.getByText('$120.10 USD', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Editar Caja principal', exact: true }).click(); await expect(page.getByLabel('Saldo inicial (USD)')).toBeDisabled(); await expect(page.getByLabel('Fecha de apertura')).toBeDisabled();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true); expect(errors).toEqual([]);
});
async function saveMovement(page: Page) {
  await page.getByRole('button', { name: 'Guardar movimiento', exact: true }).click(); await expect(page.getByRole('status').filter({ hasText: 'Movimiento guardado.' })).toBeVisible();
  await expect(page.getByRole('status').filter({ hasText: 'Cargando movimientos' })).toHaveCount(0);
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
