import { sameOriginRequest } from '../../lib/http';
import { authApi, AuthError, isRecord } from '../auth/api';

export type Bank = { id: string; name: string; isActive: boolean; version: string };
export type Category = Bank & { kind: 'income' | 'expense'; isInitial: boolean };
export type Account = Bank & { kind: 'bank' | 'cash'; bankId: string | null; currency: 'USD'; openingBalance: string; currentBalance: string; openingDate: string; openingLocked: boolean };
export type Catalog = 'banks' | 'accounts' | 'categories';
export async function catalogRequest(path: string, method = 'GET', body?: unknown, signal?: AbortSignal): Promise<unknown> {
  const headers: Record<string, string> = { Accept: 'application/json' };
  if (method !== 'GET') {
    const csrf = await authApi('/csrf', undefined, signal);
    if (!isRecord(csrf) || typeof csrf.token !== 'string') throw new Error('Respuesta de seguridad no válida.');
    headers['X-CSRF-TOKEN'] = csrf.token; headers['Content-Type'] = 'application/json';
  }
  const response = await sameOriginRequest(`/api/finance/${path}`, { method, headers, signal, ...(body === undefined ? {} : { body: JSON.stringify(body) }) });
  if (!response.ok) {
    const error: unknown = await response.json().catch(() => null);
    throw new AuthError(response.status, isRecord(error) && typeof error.title === 'string' ? error.title : 'No se pudo completar la operación.');
  }
  return response.json() as Promise<unknown>;
}
function base(value: unknown): value is Bank & Record<string, unknown> {
  return isRecord(value) && typeof value.id === 'string' && typeof value.name === 'string' && typeof value.isActive === 'boolean' && typeof value.version === 'string';
}
export function banks(value: unknown): Bank[] { if (!Array.isArray(value) || !value.every(base)) throw new Error('Respuesta de bancos no válida.'); return value; }
export function categories(value: unknown): Category[] {
  if (!Array.isArray(value) || !value.every(v => base(v) && isRecord(v) && (v.kind === 'income' || v.kind === 'expense') && typeof v.isInitial === 'boolean')) throw new Error('Respuesta de categorías no válida.');
  return value as Category[];
}
export function accounts(value: unknown): Account[] {
  if (!Array.isArray(value) || !value.every(v => base(v) && isRecord(v) && (v.kind === 'bank' || v.kind === 'cash') && (v.bankId === null || typeof v.bankId === 'string')
    && v.currency === 'USD' && typeof v.openingDate === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(v.openingDate) && typeof v.openingLocked === 'boolean'
    && typeof v.openingBalance === 'string' && /^-?\d{1,16}\.\d{2}$/.test(v.openingBalance) && typeof v.currentBalance === 'string' && /^-?\d{1,29}\.\d{2}$/.test(v.currentBalance))) throw new Error('Respuesta de cuentas no válida.');
  return value as Account[];
}
export function money(value: string): string {
  const [integer, fraction] = value.split('.');
  return `${value.startsWith('-') ? '-$' : '$'}${new Intl.NumberFormat('es-PA').format(BigInt(integer!.replace('-', '')))}.${fraction}`;
}
