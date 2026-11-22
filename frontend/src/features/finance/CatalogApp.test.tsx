import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { CatalogApp } from './CatalogApp';
import { catalogRequest } from './api';
import { AuthError } from '../auth/api';
vi.mock('./api', async original => ({ ...await original<typeof import('./api')>(), catalogRequest: vi.fn() }));
const api = vi.mocked(catalogRequest);
beforeEach(() => { api.mockReset(); api.mockResolvedValue([]); });

describe('catálogos financieros', () => {
  it('muestra carga, vacío y navegación sin balances inventados', async () => {
    render(<CatalogApp onSessionExpired={vi.fn()} />);
    expect(screen.getByRole('status')).toHaveTextContent('Cargando');
    expect(await screen.findByText('No hay cuentas activas. Añade la primera para comenzar.')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Bancos' }));
    expect(screen.getByRole('button', { name: 'Bancos' })).toHaveAttribute('aria-current', 'page');
    expect(screen.queryByText(/\$0/)).not.toBeInTheDocument();
  });
  it('conserva un fallo de carga al navegar y permite reintento', async () => {
    api.mockRejectedValue(new TypeError('Network failure'));
    render(<CatalogApp onSessionExpired={vi.fn()} />);
    await screen.findByRole('alert');
    fireEvent.click(screen.getByRole('button', { name: 'Bancos' }));
    expect(screen.getByRole('alert')).toBeInTheDocument();
    api.mockResolvedValue([]); fireEvent.click(screen.getByRole('button', { name: 'Reintentar' }));
    await screen.findByText('No hay bancos activos. Añade el primero para comenzar.');
  });
  it('asocia validación al formulario sin guardar nombres vacíos', async () => {
    render(<CatalogApp onSessionExpired={vi.fn()} />); await screen.findByText(/No hay cuentas/);
    fireEvent.click(screen.getByRole('button', { name: 'Bancos' }));
    fireEvent.click(screen.getByRole('button', { name: 'Añadir banco' }));
    fireEvent.click(screen.getByRole('button', { name: 'Guardar' }));
    expect(screen.getByRole('alert')).toHaveTextContent('Introduce un nombre');
    expect(screen.getByLabelText('Nombre', { exact: true })).toHaveAccessibleDescription(/Introduce un nombre/);
    expect(api).not.toHaveBeenCalledWith('banks', 'POST', expect.anything(), expect.anything());
  });
  it('muestra duplicado del servidor sin falsa confirmación y permite corregir', async () => {
    render(<CatalogApp onSessionExpired={vi.fn()} />); await screen.findByText(/No hay cuentas/);
    fireEvent.click(screen.getByRole('button', { name: 'Bancos' }));
    fireEvent.click(screen.getByRole('button', { name: 'Añadir banco' }));
    fireEvent.change(screen.getByLabelText('Nombre', { exact: true }), { target: { value: 'Banco ensayo' } });
    api.mockRejectedValueOnce(new AuthError(409, 'Ya existe un banco.'));
    fireEvent.click(screen.getByRole('button', { name: 'Guardar' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Ya existe');
    expect(screen.getByLabelText('Nombre', { exact: true })).toHaveValue('Banco ensayo');
    expect(screen.queryByText('Cambios guardados.')).not.toBeInTheDocument();
  });
  it('guarda cuenta con montos como cadenas y protege doble envío', async () => {
    render(<CatalogApp onSessionExpired={vi.fn()} />); await screen.findByText(/No hay cuentas/);
    fireEvent.click(screen.getByRole('button', { name: 'Añadir cuenta' }));
    fireEvent.change(screen.getByLabelText('Nombre', { exact: true }), { target: { value: 'Efectivo ensayo' } });
    fireEvent.change(screen.getByLabelText('Saldo inicial (USD)'), { target: { value: '9999999999999999.99' } });
    fireEvent.change(screen.getByLabelText('Fecha de apertura'), { target: { value: '2026-11-22' } });
    let finish: ((value: unknown) => void) | undefined;
    api.mockImplementationOnce(() => new Promise(resolve => { finish = resolve; }));
    fireEvent.click(screen.getByRole('button', { name: 'Guardar' }));
    expect(screen.getByRole('button', { name: 'Guardando…' })).toBeDisabled();
    expect(api).toHaveBeenCalledWith('accounts', 'POST', expect.objectContaining({ openingBalance: '9999999999999999.99', bankId: null, currency: 'USD' }), expect.any(AbortSignal));
    finish!({}); await screen.findByText('Cambios guardados.');
  });
  it('ante 401 pide renovar sesión y no muestra datos privados', async () => {
    api.mockRejectedValue(new AuthError(401, 'Sesión vencida')); const expired = vi.fn();
    render(<CatalogApp onSessionExpired={expired} />); await waitFor(() => expect(expired).toHaveBeenCalled());
    expect(screen.queryByRole('list')).not.toBeInTheDocument();
  });
});
