import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { PlanningApp } from './PlanningApp';
import { catalogRequest } from './api';
import { cards, forecast, plans } from './planning-api';
import { AuthError } from '../auth/api';
vi.mock('./api', async original => ({ ...await original<typeof import('./api')>(), catalogRequest: vi.fn() }));
const api = vi.mocked(catalogRequest);
const account = { id: 'account', name: 'Caja', kind: 'cash', bankId: null, currency: 'USD', openingBalance: '100.00', currentBalance: '100.00', openingDate: '2026-01-01', openingLocked: false, version: 'version', isActive: true };
const category = { id: 'category', name: 'Comida', kind: 'expense', isInitial: false, version: 'version', isActive: true };
const empty = { items: [], actualAvailable: '100.00', pendingExpenses: '0.00', estimatedAvailable: '100.00' };
beforeEach(() => { api.mockReset(); api.mockImplementation(async path => path === 'accounts' ? [account] : path === 'categories' ? [category] : path.startsWith('commitments/pending') ? empty : []); });
describe('tarjetas y compromisos', () => {
  it('rechaza contratos corruptos en vez de inventar deuda o disponible', () => {
    expect(() => cards([{ name: 'Tarjeta', currentDebt: 10 }])).toThrow(); expect(() => plans([{}])).toThrow(); expect(() => forecast({ ...empty, estimatedAvailable: 100 })).toThrow();
  });
  it('valida tarjeta sin enviar credenciales de tarjeta y conserva clave tras fallo', async () => {
    render(<PlanningApp mode="cards" onSessionExpired={vi.fn()} />); await screen.findByText('No hay tarjetas registradas.');
    fireEvent.click(screen.getByRole('button', { name: 'Añadir tarjeta' })); fireEvent.click(screen.getByRole('button', { name: 'Guardar' }));
    expect(screen.getByRole('alert')).toHaveTextContent('Revisa nombre');
    fireEvent.change(screen.getByLabelText('Alias de tarjeta'), { target: { value: 'Crédito ensayo' } }); fireEvent.change(screen.getByLabelText('Límite de crédito (USD)'), { target: { value: '1000.00' } });
    api.mockRejectedValueOnce(new TypeError('timeout')); fireEvent.click(screen.getByRole('button', { name: 'Guardar' })); await screen.findByText(/No se pudo completar la operación/);
    const first = api.mock.calls.find(c => c[1] === 'POST')![2] as { requestId: string };
    api.mockRejectedValueOnce(new AuthError(409, 'Conflicto de ensayo')); fireEvent.click(screen.getByRole('button', { name: 'Guardar' })); await screen.findByText('Conflicto de ensayo');
    const last = api.mock.calls.filter(c => c[1] === 'POST').at(-1)![2] as { requestId: string; creditLimit: string };
    expect(last.requestId).toBe(first.requestId); expect(last.creditLimit).toBe('1000.00'); expect(screen.getByLabelText('Alias de tarjeta')).toHaveValue('Crédito ensayo');
  });
  it('mantiene la edición del período accesible después de un error del servidor', async () => {
    render(<PlanningApp mode="commitments" onSessionExpired={vi.fn()} />); await screen.findByText('No hay compromisos pendientes en este período.');
    api.mockRejectedValueOnce(new AuthError(400, 'Período demasiado amplio'));
    fireEvent.click(screen.getByRole('button', { name: 'Consultar período' })); await screen.findByText('Período demasiado amplio');
    expect(screen.getByLabelText('Período desde')).toBeEnabled();
    fireEvent.change(screen.getByLabelText('Período hasta'), { target: { value: '2026-11-30' } }); fireEvent.click(screen.getByRole('button', { name: 'Consultar período' }));
    await screen.findByText('No hay compromisos pendientes en este período.');
  });
  it('confirma una ocurrencia con versión, fecha y clave y bloquea doble envío', async () => {
    api.mockImplementation(async path => path === 'accounts' ? [account] : path === 'categories' ? [category] : path.startsWith('commitments/pending') ? { ...empty, items: [{ commitmentId: 'plan', version: 'version', dueDate: '2026-11-10', concept: 'Previsto', kind: 'expense', categoryId: 'category', amount: '20.00' }] } : []);
    render(<PlanningApp mode="commitments" onSessionExpired={vi.fn()} />); await screen.findByRole('button', { name: 'Realizar Previsto 2026-11-10' });
    fireEvent.click(screen.getByRole('button', { name: 'Realizar Previsto 2026-11-10' })); fireEvent.change(screen.getByLabelText('Cuenta de realización'), { target: { value: 'account' } });
    let finish: ((v: unknown) => void) | undefined; api.mockImplementationOnce(() => new Promise(resolve => { finish = resolve; }));
    fireEvent.click(screen.getByRole('button', { name: 'Confirmar realización' })); expect(screen.getByRole('button', { name: 'Guardando…' })).toBeDisabled();
    expect(api).toHaveBeenCalledWith('commitments/plan/confirm', 'POST', expect.objectContaining({ dueDate: '2026-11-10', version: 'version', accountId: 'account', requestId: expect.any(String) }), expect.any(AbortSignal));
    finish!({}); await screen.findByText('Compromiso realizado mediante una transacción real vinculada.');
  });
  it('notifica expiración de sesión sin mostrar saldos falsos', async () => {
    api.mockRejectedValue(new AuthError(401, 'Sesión vencida')); const expired = vi.fn(); render(<PlanningApp mode="cards" onSessionExpired={expired} />);
    await waitFor(() => expect(expired).toHaveBeenCalled()); expect(screen.queryByText('No hay tarjetas registradas.')).not.toBeInTheDocument();
  });
});
