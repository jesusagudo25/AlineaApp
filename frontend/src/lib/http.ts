const baseUrl = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/$/, '');

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
