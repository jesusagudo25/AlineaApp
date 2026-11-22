import { sameOriginRequest } from '../../lib/http';

export class AuthError extends Error {
  constructor(public status: number, message: string) { super(message); }
}

export async function authApi(path: string, body?: unknown, signal?: AbortSignal): Promise<unknown> {
  let token: string | undefined;
  if (body !== undefined) {
    const csrf = await authApi('/csrf', undefined, signal);
    if (!isRecord(csrf) || typeof csrf.token !== 'string') throw new Error('Respuesta de seguridad no válida.');
    token = csrf.token;
  }
  const response = await sameOriginRequest(`/api/auth${path}`, {
    method: body === undefined ? 'GET' : 'POST', signal,
    headers: { Accept: 'application/json', ...(token ? { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': token } : {}) },
    ...(body === undefined ? {} : { body: JSON.stringify(body) }),
  });
  if (!response.ok) {
    const problem: unknown = await response.json().catch(() => null);
    throw new AuthError(response.status, isRecord(problem) && typeof problem.title === 'string' ? problem.title : 'No se pudo completar la solicitud. Inténtalo de nuevo.');
  }
  return response.status === 204 ? null : response.json() as Promise<unknown>;
}
export function isRecord(value: unknown): value is Record<string, unknown> { return typeof value === 'object' && value !== null; }
