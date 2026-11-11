import { fireEvent, render, screen, within } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { DesignPreview } from './DesignPreview';

describe('app shell visual', () => {
  it('muestra saldos ausentes y acciones de negocio deshabilitadas', () => {
    vi.stubGlobal('fetch', vi.fn(() => new Promise(() => {})));
    render(<DesignPreview />);
    expect(screen.getByText('Sin sesión ni datos financieros')).toBeInTheDocument();
    expect(screen.getByLabelText('Saldo no disponible')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Añadir cuenta' })).toBeDisabled();
  });
  it('navega entre estructuras sin representar operaciones financieras', () => {
    vi.stubGlobal('fetch', vi.fn(() => new Promise(() => {})));
    render(<DesignPreview />);
    const nav = screen.getByRole('navigation', { name: 'Navegación principal' });
    fireEvent.click(within(nav).getByRole('button', { name: 'Cuentas' }));
    expect(screen.getByRole('heading', { name: 'Cuentas', level: 1 })).toBeInTheDocument();
    expect(within(nav).getByRole('button', { name: 'Cuentas' })).toHaveAttribute('aria-current', 'page');
    expect(screen.getByRole('button', { name: 'Añadir cuenta' })).toBeDisabled();
  });
});
