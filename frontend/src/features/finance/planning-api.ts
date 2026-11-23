import { isRecord } from '../auth/api';
export type Card = { id: string; version: string; name: string; kind: 'credit' | 'debit'; accountId: string | null; openingDate: string; creditLimit: string; initialDebt: string; currentDebt: string | null; availableCredit: string | null; closingDay: number | null; paymentDay: number | null; openingLocked: boolean; isActive: boolean };
export type Plan = { id: string; version: string; name: string; kind: 'income' | 'expense'; categoryId: string; amount: string; frequency: string; nextDate: string | null; status: string };
export type Pending = { commitmentId: string; version: string; dueDate: string; concept: string; kind: 'income' | 'expense'; categoryId: string; amount: string };
export type Forecast = { items: Pending[]; actualAvailable: string; pendingExpenses: string; estimatedAvailable: string };
function amount(value: unknown): value is string { return typeof value === 'string' && /^-?\d+\.\d{2}$/.test(value); }
function base(value: unknown): value is Record<string, unknown> { return isRecord(value) && typeof value.id === 'string' && typeof value.version === 'string' && typeof value.name === 'string'; }
export function cards(value: unknown): Card[] {
  if (!Array.isArray(value) || !value.every(v => base(v) && (v.kind === 'credit' || v.kind === 'debit') && (v.accountId === null || typeof v.accountId === 'string') && typeof v.openingDate === 'string'
    && amount(v.creditLimit) && amount(v.initialDebt) && (v.currentDebt === null || amount(v.currentDebt)) && (v.availableCredit === null || amount(v.availableCredit))
    && (v.kind === 'credit' ? amount(v.currentDebt) && amount(v.availableCredit) && v.accountId === null && typeof v.closingDay === 'number' && Number.isInteger(v.closingDay) && v.closingDay >= 1 && v.closingDay <= 31
      && typeof v.paymentDay === 'number' && Number.isInteger(v.paymentDay) && v.paymentDay >= 1 && v.paymentDay <= 31
      : typeof v.accountId === 'string' && v.currentDebt === null && v.availableCredit === null && v.closingDay === null && v.paymentDay === null)
    && typeof v.openingLocked === 'boolean' && typeof v.isActive === 'boolean')) throw new Error('Respuesta de tarjetas no válida.');
  return value as Card[];
}
export function plans(value: unknown): Plan[] {
  if (!Array.isArray(value) || !value.every(v => base(v) && (v.kind === 'income' || v.kind === 'expense') && typeof v.categoryId === 'string' && amount(v.amount)
    && ['once', 'weekly', 'fortnightly', 'monthly'].includes(String(v.frequency)) && (v.nextDate === null || typeof v.nextDate === 'string') && ['active', 'paused', 'inactive'].includes(String(v.status)))) throw new Error('Respuesta de compromisos no válida.');
  return value as Plan[];
}
export function forecast(value: unknown): Forecast {
  if (!isRecord(value) || !Array.isArray(value.items) || !amount(value.actualAvailable) || !amount(value.pendingExpenses) || !amount(value.estimatedAvailable)
    || !value.items.every(v => isRecord(v) && typeof v.commitmentId === 'string' && typeof v.version === 'string' && typeof v.dueDate === 'string' && typeof v.concept === 'string'
      && (v.kind === 'income' || v.kind === 'expense') && typeof v.categoryId === 'string' && amount(v.amount))) throw new Error('Previsión recibida no válida.');
  return value as Forecast;
}
