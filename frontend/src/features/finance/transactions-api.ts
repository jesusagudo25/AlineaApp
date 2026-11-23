import { isRecord } from '../auth/api';
export type Movement = { id: string; version: string; kind: 'income' | 'expense' | 'transfer' | 'card_payment'; accountId: string | null; cardId?: string | null; commitmentOccurrenceId?: string | null; destinationAccountId: string | null; categoryId: string | null; amount: string; date: string; description: string; isVoided: boolean };
export type History = { items: Movement[]; total: number; page: number; pageSize: number; income: string; expense: string };
export function movement(value: unknown): Movement {
  if (!isRecord(value) || typeof value.id !== 'string' || typeof value.version !== 'string' || !['income', 'expense', 'transfer', 'card_payment'].includes(String(value.kind))
    || !(value.accountId === null || typeof value.accountId === 'string') || !(value.cardId == null || typeof value.cardId === 'string') || !(value.commitmentOccurrenceId == null || typeof value.commitmentOccurrenceId === 'string') || !(value.destinationAccountId === null || typeof value.destinationAccountId === 'string')
    || !(value.categoryId === null || typeof value.categoryId === 'string') || typeof value.amount !== 'string' || !/^\d{1,16}\.\d{2}$/.test(value.amount)
    || typeof value.date !== 'string' || !/^\d{4}-\d{2}-\d{2}$/.test(value.date) || typeof value.description !== 'string' || typeof value.isVoided !== 'boolean') throw new Error('Movimiento recibido no válido.');
  return value as Movement;
}
export function history(value: unknown): History {
  if (!isRecord(value) || !Array.isArray(value.items) || typeof value.total !== 'number' || !Number.isSafeInteger(value.total) || value.total < 0
    || typeof value.page !== 'number' || typeof value.pageSize !== 'number' || typeof value.income !== 'string' || !/^\d+\.\d{2}$/.test(value.income)
    || typeof value.expense !== 'string' || !/^\d+\.\d{2}$/.test(value.expense)) throw new Error('Historial recibido no válido.');
  return { items: value.items.map(movement), total: value.total, page: value.page, pageSize: value.pageSize, income: value.income, expense: value.expense };
}
export function detail(value: unknown) {
  if (!isRecord(value) || !Array.isArray(value.revisions)) throw new Error('Detalle recibido no válido.');
  return { transaction: movement(value.transaction), revisions: value.revisions.map((r: unknown) => {
    if (!isRecord(r) || typeof r.operation !== 'string' || typeof r.reason !== 'string' || typeof r.at !== 'string' || typeof r.snapshot !== 'string') throw new Error('Revisión recibida no válida.');
    return { operation: r.operation, reason: r.reason, at: r.at, transaction: movement(JSON.parse(r.snapshot) as unknown) };
  }) };
}
export function validAmount(value: string): boolean { return /^\d{1,16}(\.\d{1,2})?$/.test(value) && BigInt(value.replace('.', '')) > 0n; }
export function businessToday(): string { return new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Panama', year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date()); }
