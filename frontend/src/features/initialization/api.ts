import { getJson, HttpError } from '../../lib/http';

export type PlatformState = 'ready' | 'unavailable' | 'empty';

function hasStatus(value: unknown, status: string): boolean {
  return typeof value === 'object' && value !== null && 'status' in value && value.status === status;
}

export async function getPlatformState(signal: AbortSignal): Promise<PlatformState> {
  const health = await getJson('/health', signal);
  if (health === null) return 'empty';
  if (!hasStatus(health, 'ok')) throw new Error('Respuesta de salud inválida');
  try {
    const ready = await getJson('/health/ready', signal);
    if (ready === null) return 'empty';
    if (!hasStatus(ready, 'ready')) throw new Error('Respuesta de disponibilidad inválida');
    return 'ready';
  } catch (error) {
    if (error instanceof HttpError && error.status === 503) return 'unavailable';
    throw error;
  }
}
