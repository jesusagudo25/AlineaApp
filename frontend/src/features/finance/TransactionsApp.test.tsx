import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { TransactionsApp } from './TransactionsApp';
import { catalogRequest } from './api';
import { AuthError } from '../auth/api';
import { history, movement, validAmount } from './transactions-api';
vi.mock('./api', async original => ({ ...await original<typeof import('./api')>(), catalogRequest: vi.fn() }));
const api = vi.mocked(catalogRequest);
const account = { id: 'account', name: 'Caja', kind: 'cash', bankId: null, currency: 'USD', openingBalance: '100.00', currentBalance: '100.00', openingDate: '2026-01-01', openingLocked: false, version: 'version', isActive: true };
const category = { id: 'category', name: 'Comida', kind: 'expense', isInitial: false, version: 'version', isActive: true };
const empty = { items: [], total: 0, page: 1, pageSize: 10, income: '0.00', expense: '0.00' };
beforeEach(() => { api.mockReset(); api.mockImplementation(async path => path === 'accounts' ? [account] : path === 'categories' ? [category] : empty); });
describe('transacciones', () => {
  it('valida contratos e importes sin conversión float', () => {
    for (const value of ['0', '-1', '1.001', '10000000000000000', 'NaN']) expect(validAmount(value)).toBe(false);
    expect(validAmount('9999999999999999.99')).toBe(true);
    expect(() => movement({ amount: 1 })).toThrow(); expect(() => history({ ...empty, expense: 1 })).toThrow();
  });
  it('muestra carga, vacío y filtros enviados al servidor', async () => {
    render(<TransactionsApp onSessionExpired={vi.fn()} />); expect(screen.getByRole('status')).toHaveTextContent('Cargando');
    await screen.findByText('No hay movimientos para estos filtros.');
    fireEvent.change(screen.getByLabelText('Filtrar tipo'), { target: { value: 'transfer' } });
    fireEvent.click(screen.getByRole('button', { name: 'Aplicar filtros' }));
    await waitFor(() => expect(api).toHaveBeenCalledWith(expect.stringContaining('kind=transfer'), 'GET', undefined, expect.any(AbortSignal)));
  });
  it('conserva clave y campos tras timeout, bloquea doble envío y renueva clave al corregir', async () => {
    render(<TransactionsApp onSessionExpired={vi.fn()} />); await screen.findByText('No hay movimientos para estos filtros.');
    fireEvent.click(screen.getByRole('button', { name: 'Registrar gasto' }));
    fireEvent.click(screen.getByRole('button', { name: 'Guardar movimiento' })); expect(screen.getByRole('alert')).toHaveTextContent('Revisa importe');
    fireEvent.change(screen.getByLabelText('Cuenta de origen'), { target: { value: 'account' } });
    fireEvent.change(screen.getByLabelText('Categoría del movimiento'), { target: { value: 'category' } });
    fireEvent.change(screen.getByLabelText('Importe (USD)'), { target: { value: '12.34' } });
    api.mockRejectedValueOnce(new TypeError('timeout'));
    fireEvent.click(screen.getByRole('button', { name: 'Guardar movimiento' })); await screen.findByText(/No se pudo completar la consulta/);
    const first = api.mock.calls.find(c => c[1] === 'POST')![2] as { requestId: string };
    api.mockRejectedValueOnce(new AuthError(409, 'Conflicto controlado'));
    fireEvent.click(screen.getByRole('button', { name: 'Guardar movimiento' })); await screen.findByText('Conflicto controlado');
    const writes = api.mock.calls.filter(c => c[1] === 'POST'); expect((writes[1]![2] as { requestId: string }).requestId).toBe(first.requestId);
    expect(screen.getByLabelText('Importe (USD)')).toHaveValue('12.34');
    fireEvent.change(screen.getByLabelText('Importe (USD)'), { target: { value: '12.35' } });
    let finish: ((v: unknown) => void) | undefined; api.mockImplementationOnce(() => new Promise(resolve => { finish = resolve; }));
    fireEvent.click(screen.getByRole('button', { name: 'Guardar movimiento' })); expect(screen.getByRole('button', { name: 'Guardando…' })).toBeDisabled();
    const last = api.mock.calls.filter(c => c[1] === 'POST').at(-1)![2] as { requestId: string; amount: string };
    expect(last.requestId).not.toBe(first.requestId); expect(last.amount).toBe('12.35'); finish!({}); await screen.findByText('Movimiento guardado.');
  });
  it('no finge vacío ante JSON inválido y solicita renovar una sesión vencida', async () => {
    api.mockResolvedValue({}); const expired = vi.fn(); render(<TransactionsApp onSessionExpired={expired} />);
    await screen.findByRole('alert'); expect(screen.queryByText('No hay movimientos para estos filtros.')).not.toBeInTheDocument();
    api.mockRejectedValue(new AuthError(401, 'Sesión vencida')); fireEvent.click(screen.getByRole('button', { name: 'Actualizar historial' }));
    await waitFor(() => expect(expired).toHaveBeenCalled());
  });
});
