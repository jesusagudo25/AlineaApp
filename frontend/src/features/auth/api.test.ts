import { describe, expect, it, vi } from 'vitest';
import { authApi, AuthError } from './api';
describe('HTTP de autenticación', () => {
  it('usa cookies de mismo origen y un token CSRF nuevo antes de escribir', async () => {
    const fetch = vi.fn().mockResolvedValueOnce(new Response(JSON.stringify({ token: 'ephemeral-csrf' })))
      .mockResolvedValueOnce(new Response(null, { status: 204 })); vi.stubGlobal('fetch', fetch);
    await authApi('/logout', {});
    expect(fetch).toHaveBeenNthCalledWith(1, '/api/auth/csrf', expect.objectContaining({ credentials: 'same-origin', method: 'GET' }));
    expect(fetch).toHaveBeenNthCalledWith(2, '/api/auth/logout', expect.objectContaining({ credentials: 'same-origin', headers: expect.objectContaining({ 'X-CSRF-TOKEN': 'ephemeral-csrf' }), method: 'POST' }));
  });
  it('rechaza respuestas inseguras sin enviar el POST', async () => {
    const fetch = vi.fn().mockResolvedValue(new Response('{}')); vi.stubGlobal('fetch', fetch);
    await expect(authApi('/login', {})).rejects.toThrow('Respuesta de seguridad'); expect(fetch).toHaveBeenCalledTimes(1);
  });
  it('propaga fallos HTTP y no interpreta HTML como sesión', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('unavailable', { status: 503 })));
    await expect(authApi('/status')).rejects.toBeInstanceOf(AuthError);
  });
});
