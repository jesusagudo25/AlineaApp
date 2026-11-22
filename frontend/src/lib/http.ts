const baseUrl = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/$/, '');

export function sameOriginRequest(path: string, options: RequestInit = {}): Promise<Response> {
  return fetch(path, {
    ...options, credentials: 'same-origin', cache: 'no-store',
    signal: options.signal ? AbortSignal.any([options.signal, AbortSignal.timeout(10000)]) : AbortSignal.timeout(10000),
  });
}

export class HttpError extends Error {
  constructor(public readonly status: number) { super(`HTTP ${status}`); }
}

export async function getJson(path: string, signal: AbortSignal): Promise<unknown> {
  const response = await fetch(`${baseUrl}${path}`, {
    signal: AbortSignal.any([signal, AbortSignal.timeout(8000)]),
    headers: { Accept: 'application/json' },
    credentials: 'omit',
    cache: 'no-store',
  });
  if (!response.ok) throw new HttpError(response.status);
  return response.json() as Promise<unknown>;
}
