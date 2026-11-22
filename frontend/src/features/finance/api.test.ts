import { describe, expect, it, vi } from 'vitest';
import { accounts, banks, categories, catalogRequest, money } from './api';

describe('contratos financieros', () => {
  it('mantiene precisión monetaria más allá de Number seguro', () => {
    expect(money('9999999999999999.99')).toContain('999');
    expect(money('9999999999999999.99')).toMatch(/\.99$/);
    expect(money('-0.01')).toBe('-$0.01');
  });
  it('rechaza respuestas ajenas al contrato, incluyendo montos numéricos', () => {
    expect(() => banks({})).toThrow(); expect(() => categories([{ id: 'x' }])).toThrow();
    expect(() => accounts([{ id: 'id', name: 'Cuenta', isActive: true, version: 'v', kind: 'cash', bankId: null, currency: 'USD', openingDate: '2026-11-22', openingLocked: false, openingBalance: 1.01, currentBalance: 1.01 }])).toThrow();
  });
  it('obtiene antiforgery y escribe con cookies del mismo origen', async () => {
    const fetch = vi.fn().mockResolvedValueOnce(new Response(JSON.stringify({ token: 'synthetic-csrf' })))
      .mockResolvedValueOnce(new Response(JSON.stringify({ id: 'synthetic' }))); vi.stubGlobal('fetch', fetch);
    await catalogRequest('banks/id', 'PUT', { name: 'Banco ensayo' });
    expect(fetch).toHaveBeenNthCalledWith(2, '/api/finance/banks/id', expect.objectContaining({ method: 'PUT', credentials: 'same-origin', headers: expect.objectContaining({ 'X-CSRF-TOKEN': 'synthetic-csrf' }) }));
  });
});
