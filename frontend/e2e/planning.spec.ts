import { test, expect, type Page } from '@playwright/test';
import { readFileSync } from 'node:fs';
const privateConfig = JSON.parse(readFileSync('../.local/phase02d/runtime.json', 'utf8')) as unknown;
if (!privateConfig || typeof privateConfig !== 'object' || !('inboxKey' in privateConfig) || typeof privateConfig.inboxKey !== 'string') throw new Error('Private test mailbox configuration required.');
const inboxKey = privateConfig.inboxKey;

test('tarjetas y compromisos reales sin duplicar gastos ni anticipar ingresos', async ({ page }, info) => {
  const errors: string[] = []; page.on('pageerror', error => errors.push(error.message));
  const today = new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Panama', year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date());
  await register(page, info.project.name === 'desktop' ? '+999000000401' : '+999000000402');
  await page.getByRole('button', { name: 'Añadir cuenta', exact: true }).click(); await page.getByLabel('Nombre', { exact: true }).fill('Cuenta de ensayo 02D');
  await page.getByLabel('Saldo inicial (USD)').fill('500.00'); await page.getByLabel('Fecha de apertura').fill(today); await save(page);
  await page.getByRole('button', { name: 'Categorías', exact: true }).click(); await page.getByRole('button', { name: 'Añadir categorías iniciales', exact: true }).click(); await expect(page.getByRole('heading', { name: 'Salario', exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Tarjetas', exact: true }).click(); await expect(page.getByText('No hay tarjetas registradas.')).toBeVisible();
  await page.getByRole('button', { name: 'Añadir tarjeta', exact: true }).click(); await page.getByRole('button', { name: 'Guardar', exact: true }).click(); await expect(page.getByRole('alert')).toContainText('Revisa nombre');
  await page.getByLabel('Alias de tarjeta').fill('Crédito de ensayo'); await page.getByLabel('Límite de crédito (USD)').fill('1000.00'); await page.getByLabel('Deuda inicial (USD)').fill('100.00'); await save(page);
  await page.getByRole('button', { name: 'Añadir tarjeta', exact: true }).click(); await page.getByLabel('Alias de tarjeta').fill('Débito de ensayo'); await page.getByLabel('Tipo de tarjeta').selectOption('debit'); await page.getByLabel('Cuenta vinculada').selectOption({ label: 'Cuenta de ensayo 02D' }); await save(page);
  await purchase(page, 'Débito de ensayo', '5.00', 'Compra débito 02D'); await purchase(page, 'Crédito de ensayo', '50.00', 'Compra crédito 02D');
  await page.getByRole('button', { name: 'Pagar Crédito de ensayo', exact: true }).click(); await page.getByLabel('Cuenta para operación').selectOption({ label: 'Cuenta de ensayo 02D' }); await page.getByLabel('Importe de operación (USD)').fill('40.00'); await page.getByLabel('Descripción de operación').fill('Pago crédito 02D');
  await page.getByRole('button', { name: 'Registrar operación', exact: true }).click(); await expect(page.getByRole('status').filter({ hasText: 'Operación registrada' })).toBeVisible();
  await expect(page.getByText(/Deuda actual: \$110.00 USD · Crédito disponible: \$890.00 USD/)).toBeVisible();
  await shot(page, info.project.name, 'cards');
  await page.getByRole('button', { name: 'Movimientos', exact: true }).click(); await expect(page.getByText('Ingresos: $0.00 USD · Gastos: $55.00 USD.', { exact: false })).toBeVisible();
  await page.getByLabel('Filtrar tipo').selectOption('card_payment'); await page.getByRole('button', { name: 'Aplicar filtros' }).click(); await expect(page.getByText('Ingresos: $0.00 USD · Gastos: $0.00 USD.', { exact: false })).toBeVisible();
  await page.getByRole('button', { name: 'Compromisos', exact: true }).click(); await expect(page.getByText('No hay compromisos registrados.')).toBeVisible();
  for (const [name, kind, amount, category] of [['Gasto previsto 02D', 'expense', '20.00', 'Alimentación'], ['Ingreso previsto 02D', 'income', '1000.00', 'Salario']]) {
    await page.getByRole('button', { name: 'Añadir compromiso', exact: true }).click(); await page.getByLabel('Concepto', { exact: true }).fill(name!); await page.getByLabel('Tipo previsto').selectOption(kind!); await page.getByLabel('Categoría prevista').selectOption({ label: category! }); await page.getByLabel('Importe previsto (USD)').fill(amount!); await save(page);
  }
  await expect(page.getByText('Saldo real disponible: $455.00 USD', { exact: true })).toBeVisible(); await expect(page.getByText('Disponible estimado: $435.00 USD', { exact: true })).toBeVisible();
  await realize(page, today);
  await expect(page.getByText('Saldo real disponible: $435.00 USD', { exact: true })).toBeVisible(); await expect(page.getByRole('button', { name: `Realizar Gasto previsto 02D ${today}`, exact: true })).toHaveCount(0);
  await page.getByRole('button', { name: 'Movimientos', exact: true }).click(); await voidMovement(page, 'Compra crédito 02D'); await voidMovement(page, 'Gasto previsto 02D');
  await page.getByRole('button', { name: 'Compromisos', exact: true }).click(); await expect(page.getByRole('button', { name: `Realizar Gasto previsto 02D ${today}`, exact: true })).toBeVisible();
  await expect(page.getByText('Saldo real disponible: $455.00 USD', { exact: true })).toBeVisible(); await expect(page.getByText('Disponible estimado: $435.00 USD', { exact: true })).toBeVisible(); await shot(page, info.project.name, 'commitments');
  await realize(page, today);
  await page.getByRole('button', { name: 'Editar Ingreso previsto 02D', exact: true }).click(); await page.getByLabel('Estado del compromiso').selectOption('paused'); await save(page); await expect(page.getByText('No hay compromisos pendientes en este período.')).toBeVisible();
  await page.getByRole('button', { name: 'Tarjetas', exact: true }).click(); await expect(page.getByText(/Deuda actual: \$60.00 USD/)).toBeVisible();
  await page.getByRole('button', { name: 'Editar Crédito de ensayo', exact: true }).click(); await expect(page.getByLabel('Deuda inicial (USD)')).toBeDisabled(); await page.getByLabel('Límite de crédito (USD)').fill('1200.00'); await page.getByLabel('Tarjeta activa', { exact: true }).uncheck(); await save(page);
  await expect(page.getByRole('button', { name: 'Comprar con Crédito de ensayo', exact: true })).toBeDisabled();
  await page.getByRole('button', { name: 'Catálogos', exact: true }).click(); await expect(page.getByText('$435.00 USD', { exact: true })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true); expect(errors).toEqual([]);
});
async function save(page: Page) { await page.getByRole('button', { name: 'Guardar', exact: true }).click(); await expect(page.getByRole('status').filter({ hasText: 'Cambios guardados.' })).toBeVisible(); await expect(page.getByRole('status').filter({ hasText: /^Cargando/ })).toHaveCount(0); }
async function purchase(page: Page, card: string, amount: string, description: string) {
  await page.getByRole('button', { name: `Comprar con ${card}`, exact: true }).click(); await page.getByLabel('Categoría de compra').selectOption({ label: 'Alimentación' }); await page.getByLabel('Importe de operación (USD)').fill(amount); await page.getByLabel('Descripción de operación').fill(description); await page.getByRole('button', { name: 'Registrar operación', exact: true }).click(); await expect(page.getByRole('status').filter({ hasText: 'Operación registrada' })).toBeVisible(); await expect(page.getByRole('status').filter({ hasText: /^Cargando/ })).toHaveCount(0);
}
async function realize(page: Page, date: string) { await page.getByRole('button', { name: `Realizar Gasto previsto 02D ${date}`, exact: true }).click(); await page.getByLabel('Cuenta de realización').selectOption({ label: 'Cuenta de ensayo 02D' }); await page.getByRole('button', { name: 'Confirmar realización', exact: true }).click(); await expect(page.getByRole('status').filter({ hasText: 'Compromiso realizado' })).toBeVisible(); await expect(page.getByRole('status').filter({ hasText: /^Cargando/ })).toHaveCount(0); }
async function voidMovement(page: Page, description: string) { await page.getByRole('button', { name: `Detalle ${description}`, exact: true }).click(); await page.getByLabel('Motivo de anulación').fill('Corrección del ensayo'); await page.getByRole('button', { name: 'Confirmar anulación', exact: true }).click(); await expect(page.getByRole('status').filter({ hasText: 'Movimiento anulado' })).toBeVisible(); await expect(page.getByRole('status').filter({ hasText: /^Cargando/ })).toHaveCount(0); }
async function shot(page: Page, project: string, kind: string) { expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true); await page.screenshot({ path: `../docs/evidence/phase02d/${kind}-${project}.png`, fullPage: true, mask: [page.locator('.session-header p')] }); }

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
