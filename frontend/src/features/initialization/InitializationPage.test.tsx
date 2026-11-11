import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { InitializationPage } from './InitializationPage';

const json = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status });

describe('inicialización', () => {
  it('muestra carga y deshabilita la consulta mientras espera', () => {
    vi.stubGlobal('fetch', vi.fn(() => new Promise(() => {})));
    render(<InitializationPage />);
    expect(screen.getByRole('button')).toBeDisabled();
    expect(screen.getByText('Comprobando la plataforma…')).toBeInTheDocument();
  });
  it('consulta salud y readiness reales antes de mostrar lista', async () => {
    const fetch = vi.fn().mockResolvedValueOnce(json({ status: 'ok' })).mockResolvedValueOnce(json({ status: 'ready' }));
    vi.stubGlobal('fetch', fetch);
    render(<InitializationPage />);
    expect(await screen.findByText('La base está lista.')).toBeInTheDocument();
    expect(fetch.mock.calls.map(call => call[0])).toEqual(['/health', '/health/ready']);
  });
  it('distingue dependencia caída de API caída', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValueOnce(json({ status: 'ok' })).mockResolvedValueOnce(json({ status: 'unavailable' }, 503)));
    render(<InitializationPage />);
    expect(await screen.findByText('Inicialización pendiente.')).toBeInTheDocument();
  });
  it('permite reintentar después de un fallo de red', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValueOnce(new TypeError('network')).mockResolvedValueOnce(json({ status: 'ok' })).mockResolvedValueOnce(json({ status: 'ready' })));
    render(<InitializationPage />);
    expect(await screen.findByText('No pudimos conectar.')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button'));
    expect(await screen.findByText('La base está lista.')).toBeInTheDocument();
  });
  it('explica una respuesta vacía', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(json(null)));
    render(<InitializationPage />);
    expect(await screen.findByText('Sin información de estado.')).toBeInTheDocument();
  });
  it('rechaza contratos de respuesta desconocidos', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(json({ status: 'inventado' })));
    render(<InitializationPage />);
    expect(await screen.findByText('No pudimos conectar.')).toBeInTheDocument();
  });
});
