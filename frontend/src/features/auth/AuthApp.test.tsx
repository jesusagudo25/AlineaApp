import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AuthApp } from './AuthApp';
import { authApi, AuthError } from './api';
vi.mock('./api', async importOriginal => ({ ...await importOriginal<typeof import('./api')>(), authApi: vi.fn() }));
const api = vi.mocked(authApi);
const user = { id: 'synthetic-id', phone: '+999000000001', name: null, email: null };

beforeEach(() => {
  api.mockReset();
  api.mockImplementation(async path => {
    if (path === '/status') return { deliveryMode: 'local-test' };
    if (path === '/session') throw new AuthError(401, '');
    if (path === '/otp/request') return { challengeId: 'synthetic', retryAfterSeconds: 60 };
    if (path === '/otp/verify') return { verificationToken: 'ephemeral-proof' };
    return null;
  });
});

async function request(register = false) {
  await screen.findByRole('button', { name: 'Solicitar código' });
  if (register) fireEvent.click(screen.getByRole('button', { name: 'Registrarme' }));
  fireEvent.change(screen.getByLabelText('Número de teléfono'), { target: { value: user.phone } });
  fireEvent.click(screen.getByRole('button', { name: 'Solicitar código' }));
  await screen.findByLabelText('Código de seis dígitos');
}

describe('autenticación funcional', () => {
  it('distingue entorno local y muestra el cooldown sin confirmar envío real', async () => {
    render(<AuthApp />); await request();
    expect(screen.getByText('Pruebas locales: no se envían SMS reales.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Reenviar en/ })).toBeDisabled();
    expect(screen.getByRole('status')).toHaveTextContent('Si el número es elegible');
    expect(api).toHaveBeenCalledWith('/otp/request', { phone: user.phone, purpose: 'login' });
  });
  it('un error de verificación conserva la entrada y permite volver al teléfono', async () => {
    render(<AuthApp />); await request();
    api.mockRejectedValueOnce(new AuthError(400, 'Código inválido'));
    fireEvent.change(screen.getByLabelText('Código de seis dígitos'), { target: { value: '000000' } });
    fireEvent.click(screen.getByRole('button', { name: 'Verificar código' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Código inválido');
    expect(screen.getByLabelText('Código de seis dígitos')).toHaveValue('000000');
    fireEvent.click(screen.getByRole('button', { name: 'Volver al teléfono' }));
    expect(screen.getByLabelText('Número de teléfono')).toBeInTheDocument();
  });
  it('completa registro solo después de verificar y carga el perfil del servidor', async () => {
    render(<AuthApp />); await request(true);
    fireEvent.change(screen.getByLabelText('Código de seis dígitos'), { target: { value: '000000' } });
    fireEvent.click(screen.getByRole('button', { name: 'Verificar código' }));
    await screen.findByLabelText('Nombre (opcional)');
    expect(api).not.toHaveBeenCalledWith('/register', expect.anything());
    api.mockImplementation(async path => path === '/session' ? user : { authenticated: true });
    fireEvent.click(screen.getByRole('button', { name: 'Completar registro' }));
    await screen.findByRole('heading', { name: 'Tu perfil' });
    expect(api).toHaveBeenCalledWith('/register', { verificationToken: 'ephemeral-proof', name: null, email: null });
    expect(screen.getByText(user.phone)).toBeInTheDocument();
  });
  it('inicia sesión tras verificar y cierra la sesión en el servidor', async () => {
    render(<AuthApp />); await request();
    api.mockImplementation(async path => path === '/otp/verify' ? { verificationToken: 'ephemeral-proof' } : path === '/session' ? user : null);
    fireEvent.change(screen.getByLabelText('Código de seis dígitos'), { target: { value: '000000' } });
    fireEvent.click(screen.getByRole('button', { name: 'Verificar código' }));
    await screen.findByRole('button', { name: 'Cerrar sesión' });
    expect(api).toHaveBeenCalledWith('/login', { verificationToken: 'ephemeral-proof' });
    fireEvent.click(screen.getByRole('button', { name: 'Cerrar sesión' }));
    await screen.findByLabelText('Número de teléfono');
    expect(api).toHaveBeenCalledWith('/logout', {});
  });
  it('no simula funcionamiento si SMS está deshabilitado', async () => {
    api.mockImplementation(async path => { if (path === '/status') return { deliveryMode: 'unavailable' }; throw new AuthError(401, ''); });
    render(<AuthApp />);
    await waitFor(() => expect(screen.getByRole('button', { name: 'Solicitar código' })).toBeDisabled());
    expect(screen.getByText('El envío SMS no está configurado.')).toBeInTheDocument();
  });
  it('fallos de red se muestran y el formulario permite reintentar', async () => {
    render(<AuthApp />); await request();
    api.mockRejectedValueOnce(new TypeError('Network failed'));
    fireEvent.change(screen.getByLabelText('Código de seis dígitos'), { target: { value: '000000' } });
    fireEvent.click(screen.getByRole('button', { name: 'Verificar código' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('No se pudo conectar');
    expect(screen.getByRole('button', { name: 'Verificar código' })).toBeEnabled();
  });
});
