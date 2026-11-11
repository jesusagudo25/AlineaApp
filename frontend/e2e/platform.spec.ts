import { test, expect } from '@playwright/test';
test('plataforma conectada, responsive y navegable por teclado', async ({ page }, testInfo) => {
  const errors: string[] = [];
  page.on('pageerror', error => errors.push(error.message));
  await page.goto('/');
  await expect(page.getByRole('heading', { name: 'La base está lista.' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Volver a comprobar' })).toBeEnabled();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
  await page.keyboard.press('Tab');
  await expect(page.getByRole('link', { name: 'AlineaApp, inicio' })).toBeFocused();
  await page.keyboard.press('Tab');
  await expect(page.getByRole('button')).toBeFocused();
  await page.keyboard.press('Enter');
  await expect(page.getByRole('heading', { name: 'La base está lista.' })).toBeVisible();
  expect(errors).toEqual([]);
  await page.screenshot({ path: `../docs/evidence/ui-${testInfo.project.name}.png`, fullPage: true });
});
