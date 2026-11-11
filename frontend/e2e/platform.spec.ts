import { test, expect } from '@playwright/test';

test('shell conectado a plataforma conservada, responsive y sin cifras ficticias', async ({ page, request }, testInfo) => {
  const errors: string[] = [];
  page.on('pageerror', error => errors.push(error.message));
  expect((await request.get('/health')).status()).toBe(200);
  expect(await (await request.get('/health/ready')).json()).toEqual({ status: 'ready' });
  await page.goto('/');
  await expect(page.getByRole('heading', { name: 'Resumen', exact: true })).toBeVisible();
  await expect(page.getByLabel('Saldo no disponible')).toHaveText('$ —');
  await expect(page.getByRole('button', { name: 'Añadir cuenta', exact: true })).toBeDisabled();
  await expect(page.getByText('Estado técnico · Desarrollo')).toHaveCount(0);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await page.screenshot({ path: `../docs/evidence/phase00b/shell-${testInfo.project.name}.png`, fullPage: true });
  const nav = page.getByRole('navigation', { name: testInfo.project.name === 'desktop' ? 'Navegación principal' : 'Navegación móvil' });
  for (const name of ['Movimientos', 'Cuentas', 'Compromisos', 'Inicio']) {
    await nav.getByRole('button', { name, exact: true }).click();
    await expect(page.getByRole('heading', { name: name === 'Inicio' ? 'Resumen' : name, level: 1 })).toBeVisible();
    await expect(nav.getByRole('button', { name, exact: true })).toHaveAttribute('aria-current', 'page');
  }
  expect(errors).toEqual([]);
});

test('teléfono y OTP son solo vistas visuales, sin llamadas de autenticación ni persistencia', async ({ page }, testInfo) => {
  const writes: string[] = [];
  page.on('request', request => { if (request.method() !== 'GET') writes.push(request.url()); });
  await page.goto('/');
  await page.getByRole('button', { name: 'Vista de acceso' }).click();
  await expect(page.getByText('No envía SMS ni inicia sesión.')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Enviar código' })).toBeDisabled();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  await page.screenshot({ path: `../docs/evidence/phase00b/access-${testInfo.project.name}.png`, fullPage: true });
  const phone = page.getByLabel('Número de teléfono');
  await phone.fill('12'); await phone.press('Tab');
  await expect(phone).toHaveAttribute('aria-invalid', 'true');
  await expect(page.getByRole('alert')).toHaveText('Revisa el número: utiliza 8 dígitos.');
  await page.screenshot({ path: `../docs/evidence/phase00b/phone-error-${testInfo.project.name}.png`, fullPage: true });
  await page.getByRole('button', { name: 'Código SMS', exact: true }).click();
  await expect(page.getByText('Vista de ejemplo. No se ha enviado ningún código.')).toBeVisible();
  await page.screenshot({ path: `../docs/evidence/phase00b/otp-${testInfo.project.name}.png`, fullPage: true });
  const code = page.getByLabel('Código de seis dígitos');
  await code.fill('12'); await code.press('Tab');
  await expect(code).toHaveAttribute('aria-invalid', 'true');
  await expect(page.getByRole('button', { name: 'Verificar código' })).toBeDisabled();
  await page.getByRole('button', { name: 'Teléfono', exact: true }).click();
  await expect(phone).toHaveValue('');
  expect(await page.evaluate(() => ({ local: localStorage.length, session: sessionStorage.length }))).toEqual({ local: 0, session: 0 });
  expect(writes).toEqual([]);
});

test('teclado, foco y contraste de los controles renderizados', async ({ page }, testInfo) => {
  await page.goto('/');
  await page.keyboard.press('Tab');
  await expect(page.getByRole('link', { name: 'Ir al contenido' })).toBeFocused();
  await page.keyboard.press('Enter');
  await expect(page.locator('main')).toBeFocused();
  await page.getByRole('button', { name: 'Vista de acceso' }).focus();
  await page.keyboard.press('Enter');
  await expect(page.getByRole('button', { name: 'Ver inicio' })).toBeFocused();
  await page.getByRole('button', { name: 'Teléfono', exact: true }).focus();
  await page.keyboard.press('Tab');
  await expect(page.getByRole('button', { name: 'Código SMS', exact: true })).toBeFocused();
  await page.keyboard.press('Tab');
  const input = page.getByLabel('Número de teléfono');
  await expect(input).toBeFocused();
  if (testInfo.project.name !== 'desktop') {
    const field = await input.boundingBox();
    const navigation = await page.getByRole('navigation', { name: 'Navegación móvil' }).boundingBox();
    expect(field!.y + field!.height).toBeLessThanOrEqual(navigation!.y);
  }
  await page.screenshot({ path: `../docs/evidence/phase00b/focus-${testInfo.project.name}.png`, fullPage: true });
  const ratios = await page.evaluate(() => {
    function rgb(color: string): number[] {
      const clean = color.trim();
      return clean.startsWith('#') ? [1, 3, 5].map(i => parseInt(clean.slice(i, i + 2), 16)) : clean.match(/[\d.]+/g)!.slice(0, 3).map(Number);
    }
    function luminance(color: string) { return rgb(color).reduce((sum, value, i) => { const c = value / 255; return sum + (i === 0 ? .2126 : i === 1 ? .7152 : .0722) * (c <= .04045 ? c / 12.92 : ((c + .055) / 1.055) ** 2.4); }, 0); }
    function ratio(foreground: string, background: string) { const a = luminance(foreground), b = luminance(background); return (Math.max(a, b) + .05) / (Math.min(a, b) + .05); }
    const root = getComputedStyle(document.documentElement);
    return {
      mainText: ratio(root.getPropertyValue('--ink'), 'rgb(243,245,242)'),
      secondaryText: ratio(getComputedStyle(document.querySelector('.field-hint')!).color, 'rgb(255,255,255)'),
      errorText: ratio(root.getPropertyValue('--error'), 'rgb(255,255,255)'),
      fieldBoundary: ratio(getComputedStyle(document.querySelector('.phone-field')!).borderColor, 'rgb(255,255,255)'),
      focus: ratio(getComputedStyle(document.querySelector('.phone-field')!).outlineColor, 'rgb(255,255,255)'),
      focusWidth: parseFloat(getComputedStyle(document.querySelector('.phone-field')!).outlineWidth),
    };
  });
  expect(ratios.mainText).toBeGreaterThanOrEqual(4.5);
  expect(ratios.secondaryText).toBeGreaterThanOrEqual(4.5);
  expect(ratios.errorText).toBeGreaterThanOrEqual(4.5);
  expect(ratios.fieldBoundary).toBeGreaterThanOrEqual(3);
  expect(ratios.focus).toBeGreaterThanOrEqual(3);
  expect(ratios.focusWidth).toBeGreaterThanOrEqual(3);
  await testInfo.attach('contrast', { body: JSON.stringify(ratios, null, 2), contentType: 'application/json' });
});
