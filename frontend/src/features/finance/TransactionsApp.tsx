import { useEffect, useRef, useState, type FormEvent } from 'react';
import { AuthError } from '../auth/api';
import { accounts, categories, catalogRequest, money, type Account, type Category } from './api';
import { businessToday, detail, history, validAmount, type History, type Movement } from './transactions-api';
import { cards, type Card } from './planning-api';

const kinds = { expense: 'Gasto', income: 'Ingreso', transfer: 'Transferencia', card_payment: 'Pago de tarjeta' };
type Form = { id?: string; version?: string; requestId: string; kind: Movement['kind']; accountId: string; destinationAccountId: string; categoryId: string; amount: string; date: string; description: string; reason: string };
type Filters = { from: string; to: string; accountId: string; categoryId: string; kind: string };
const emptyFilters: Filters = { from: '', to: '', accountId: '', categoryId: '', kind: '' };
export function TransactionsApp({ onSessionExpired }: { onSessionExpired: () => void }) {
  const [accountRows, setAccounts] = useState<Account[]>([]), [categoryRows, setCategories] = useState<Category[]>([]);
  const [cardRows, setCards] = useState<Card[]>([]);
  const [data, setData] = useState<History | null>(null), [filters, setFilters] = useState(emptyFilters), [draft, setDraft] = useState(emptyFilters);
  const [page, setPage] = useState(1), [revision, setRevision] = useState(0), [loading, setLoading] = useState(true), [saving, setSaving] = useState(false);
  const [error, setError] = useState(''), [notice, setNotice] = useState(''), [form, setForm] = useState<Form | null>(null);
  const [selected, setSelected] = useState<ReturnType<typeof detail> | null>(null), [voidReason, setVoidReason] = useState('');
  const [voidKey, setVoidKey] = useState(() => crypto.randomUUID());
  const request = useRef<AbortController | null>(null), first = useRef<HTMLSelectElement>(null);
  useEffect(() => () => request.current?.abort(), []);
  useEffect(() => { if (form) first.current?.focus(); }, [form?.id, form !== null]);
  function fail(cause: unknown) {
    if (cause instanceof AuthError && cause.status === 401) { onSessionExpired(); return; }
    setError(cause instanceof AuthError ? cause.message : 'No se pudo completar la consulta. Reintenta; se conservará la clave para evitar duplicados.');
  }
  useEffect(() => {
    const controller = new AbortController(); setLoading(true); setError('');
    const query = new URLSearchParams({ page: String(page), pageSize: '10' });
    Object.entries(filters).forEach(([key, value]) => { if (value) query.set(key, value); });
    void Promise.all([catalogRequest(`transactions?${query}`, 'GET', undefined, controller.signal), catalogRequest('accounts', 'GET', undefined, controller.signal), catalogRequest('categories', 'GET', undefined, controller.signal), catalogRequest('cards', 'GET', undefined, controller.signal)])
      .then(([h, a, c, cardData]) => { if (!controller.signal.aborted) { setData(history(h)); setAccounts(accounts(a)); setCategories(categories(c)); setCards(cards(cardData)); } })
      .catch((cause: unknown) => { if (!controller.signal.aborted) fail(cause); }).finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [filters, page, revision]);
  function start(kind: Movement['kind'] = 'expense', row?: Movement) {
    setSelected(null); setError(''); setNotice('');
    setForm({ requestId: crypto.randomUUID(), kind, accountId: '', destinationAccountId: '', categoryId: '', amount: '', date: businessToday(), description: '', reason: '',
      ...(row ? { ...row, accountId: row.accountId ?? '', destinationAccountId: row.destinationAccountId ?? '', categoryId: row.categoryId ?? '' } : {}) });
  }
  function change(patch: Partial<Form>) { setForm(current => current ? { ...current, ...patch, requestId: crypto.randomUUID() } : null); }
  async function submit(event: FormEvent) {
    event.preventDefault(); if (!form || saving) return;
    if (!validAmount(form.amount) || !form.date || !form.accountId || (form.kind === 'transfer' ? !form.destinationAccountId || form.destinationAccountId === form.accountId : !form.categoryId)
      || (form.id && !form.reason.trim())) { setError('Revisa importe positivo con hasta dos decimales, fecha, cuentas, categoría y motivo si editas.'); return; }
    const controller = new AbortController(); request.current = controller; setSaving(true); setError('');
    try {
      await catalogRequest(`transactions${form.id ? `/${form.id}` : ''}`, form.id ? 'PUT' : 'POST', { ...form, categoryId: form.kind === 'transfer' ? null : form.categoryId, destinationAccountId: form.kind === 'transfer' ? form.destinationAccountId : null }, controller.signal);
      if (!controller.signal.aborted) { setForm(null); setNotice('Movimiento guardado.'); setRevision(v => v + 1); }
    } catch (cause) { if (!controller.signal.aborted) fail(cause); }
    finally { if (!controller.signal.aborted) setSaving(false); }
  }
  async function open(row: Movement) {
    const controller = new AbortController(); request.current?.abort(); request.current = controller; setError(''); setNotice(''); setSelected(null); setSaving(true);
    try { const value = detail(await catalogRequest(`transactions/${row.id}`, 'GET', undefined, controller.signal)); if (!controller.signal.aborted) { setSelected(value); setVoidReason(''); setVoidKey(crypto.randomUUID()); setForm(null); } }
    catch (cause) { if (!controller.signal.aborted) fail(cause); }
    finally { if (!controller.signal.aborted) setSaving(false); }
  }
  async function voidMovement(event: FormEvent) {
    event.preventDefault(); if (!selected || saving || !voidReason.trim()) { setError('Indica un motivo para confirmar la anulación.'); return; }
    const controller = new AbortController(); request.current = controller; setSaving(true); setError(''); setNotice('');
    try {
      await catalogRequest(`transactions/${selected.transaction.id}/void`, 'POST', { requestId: voidKey, version: selected.transaction.version, reason: voidReason }, controller.signal);
      if (!controller.signal.aborted) { setSelected(null); setNotice('Movimiento anulado. Su trazabilidad se conserva.'); setRevision(v => v + 1); }
    } catch (cause) { if (!controller.signal.aborted) fail(cause); }
    finally { if (!controller.signal.aborted) setSaving(false); }
  }
  const name = (id: string | null) => id === null ? 'Tarjeta de crédito' : accountRows.find(a => a.id === id)?.name ?? 'Cuenta no disponible';
  return <section className="movements" aria-label="Transacciones financieras">
    <header className="catalog-heading"><h1>Movimientos</h1><div className="catalog-actions"><button className="primary-button" disabled={saving || loading} onClick={() => start()}>Registrar gasto</button><button className="secondary-button" disabled={saving || loading} onClick={() => start('income')}>Nuevo movimiento</button></div></header>
    {notice && <p role="status">{notice}</p>}
    {error && <div role="alert"><p>{error}</p><button className="secondary-button" disabled={saving} onClick={() => setRevision(v => v + 1)}>Actualizar historial</button></div>}
    {form && <form className="catalog-form" aria-label={form.id ? 'Editar movimiento' : 'Registrar movimiento'} onSubmit={event => void submit(event)} noValidate aria-busy={saving}>
      <h2>{form.id ? 'Editar movimiento' : 'Registrar movimiento'}</h2>
      <label htmlFor="movement-kind">Tipo de movimiento</label><select ref={first} id="movement-kind" disabled={saving} value={form.kind} onChange={e => change({ kind: e.target.value as Movement['kind'], categoryId: '', destinationAccountId: '' })}>{Object.entries(kinds).filter(([key]) => key !== 'card_payment').map(([key, label]) => <option key={key} value={key}>{label}</option>)}</select>
      <label htmlFor="movement-account">Cuenta de origen</label><select id="movement-account" required disabled={saving} value={form.accountId} onChange={e => change({ accountId: e.target.value })}><option value="">Selecciona una cuenta</option>{accountRows.filter(a => a.isActive).map(a => <option key={a.id} value={a.id}>{a.name}</option>)}</select>
      {form.kind === 'transfer' ? <><label htmlFor="movement-destination">Cuenta de destino</label><select id="movement-destination" required disabled={saving} value={form.destinationAccountId} onChange={e => change({ destinationAccountId: e.target.value })}><option value="">Selecciona otra cuenta</option>{accountRows.filter(a => a.isActive && a.id !== form.accountId).map(a => <option key={a.id} value={a.id}>{a.name}</option>)}</select></> : <><label htmlFor="movement-category">Categoría del movimiento</label><select id="movement-category" required disabled={saving} value={form.categoryId} onChange={e => change({ categoryId: e.target.value })}><option value="">Selecciona una categoría</option>{categoryRows.filter(c => c.isActive && c.kind === form.kind).map(c => <option key={c.id} value={c.id}>{c.name}</option>)}</select></>}
      <label htmlFor="movement-amount">Importe (USD)</label><div className="phone-field"><input id="movement-amount" inputMode="decimal" required disabled={saving} value={form.amount} onChange={e => change({ amount: e.target.value })} /></div>
      <label htmlFor="movement-date">Fecha del movimiento</label><div className="phone-field"><input id="movement-date" type="date" required disabled={saving} max={businessToday()} min={accountRows.find(a => a.id === form.accountId)?.openingDate} value={form.date} onChange={e => change({ date: e.target.value })} /></div>
      <label htmlFor="movement-description">Descripción</label><div className="phone-field"><input id="movement-description" maxLength={500} disabled={saving} value={form.description} onChange={e => change({ description: e.target.value })} /></div>
      {form.id && <><label htmlFor="movement-reason">Motivo de edición</label><div className="phone-field"><input id="movement-reason" required maxLength={100} disabled={saving} value={form.reason} onChange={e => change({ reason: e.target.value })} /></div></>}
      <p className="field-hint">Importe positivo y hasta dos decimales. Fecha desde la apertura de ambas cuentas, hasta hoy en Panamá. Las transferencias no son ingresos ni gastos.</p>
      <div className="catalog-actions"><button className="primary-button" disabled={saving} type="submit">{saving ? 'Guardando…' : 'Guardar movimiento'}</button><button className="secondary-button" disabled={saving} type="button" onClick={() => setForm(null)}>Cancelar</button></div>
    </form>}
    {!loading && accountRows.length === 0 && <p>Crea una cuenta en Catálogos antes de registrar movimientos. Para ingresos y gastos necesitas también una categoría activa del mismo tipo.</p>}
    <form className="movement-filters" aria-label="Filtrar movimientos" onSubmit={e => { e.preventDefault(); setFilters({ ...draft }); setPage(1); }}>
      <label>Desde<input type="date" value={draft.from} onChange={e => setDraft({ ...draft, from: e.target.value })} /></label><label>Hasta<input type="date" value={draft.to} onChange={e => setDraft({ ...draft, to: e.target.value })} /></label>
      <label>Filtrar cuenta<select value={draft.accountId} onChange={e => setDraft({ ...draft, accountId: e.target.value })}><option value="">Todas</option>{accountRows.map(a => <option key={a.id} value={a.id}>{a.name}</option>)}</select></label>
      <label>Filtrar tipo<select value={draft.kind} onChange={e => setDraft({ ...draft, kind: e.target.value })}><option value="">Todos</option>{Object.entries(kinds).map(([key, label]) => <option key={key} value={key}>{label}</option>)}</select></label>
      <label>Filtrar categoría<select value={draft.categoryId} onChange={e => setDraft({ ...draft, categoryId: e.target.value })}><option value="">Todas</option>{categoryRows.map(c => <option key={c.id} value={c.id}>{c.name} · {kinds[c.kind]}</option>)}</select></label>
      <button className="secondary-button" disabled={loading || saving}>Aplicar filtros</button><button type="button" className="secondary-button" disabled={loading || saving} onClick={() => { setDraft(emptyFilters); setFilters(emptyFilters); setPage(1); }}>Limpiar filtros</button>
    </form>
    {loading ? <p role="status">Cargando movimientos…</p> : !error && data && <>
      <p>Ingresos: {money(data.income)} USD · Gastos: {money(data.expense)} USD. Totales del filtro, sin transferencias ni anulados.</p>
      {data.items.length === 0 ? <p>No hay movimientos para estos filtros.</p> : <ul className="catalog-list">{data.items.map(row => <li key={row.id}><div><h2>{kinds[row.kind]} · {money(row.amount)} USD {row.isVoided && '· Anulado'}</h2><p>{row.date} · {name(row.accountId)}{row.destinationAccountId && ` → ${name(row.destinationAccountId)}`}</p><p>{row.description || 'Sin descripción'}{row.categoryId && ` · ${categoryRows.find(c => c.id === row.categoryId)?.name ?? 'Categoría'}`}</p></div><button className="secondary-button" disabled={saving} onClick={() => void open(row)} aria-label={`Detalle ${row.description || kinds[row.kind]}`}>Ver detalle</button></li>)}</ul>}
      <nav className="catalog-actions" aria-label="Paginación de movimientos"><button className="secondary-button" disabled={page <= 1 || saving} onClick={() => setPage(p => p - 1)}>Anterior</button><span>Página {page} · {data.total} movimientos</span><button className="secondary-button" disabled={page * data.pageSize >= data.total || saving} onClick={() => setPage(p => p + 1)}>Siguiente</button></nav>
    </>}
    {selected && <section className="catalog-form" aria-label="Detalle del movimiento"><h2>Detalle del movimiento</h2><p>{kinds[selected.transaction.kind]} · {money(selected.transaction.amount)} USD · {selected.transaction.date}</p><p>{selected.transaction.description}</p><p>{selected.transaction.isVoided ? 'Anulado' : 'Vigente'}</p>{selected.transaction.cardId && <p>Tarjeta: {cardRows.find(c => c.id === selected.transaction.cardId)?.name ?? 'No disponible'}</p>}{selected.transaction.commitmentOccurrenceId && <p>Compromiso vinculado: {selected.transaction.commitmentOccurrenceId}</p>}
      <h3>Trazabilidad</h3><ol>{selected.revisions.map((r, i) => <li key={i}><p>{r.operation === 'create' ? 'Registro' : r.operation === 'edit' ? 'Edición' : 'Anulación'} · {r.reason} · {new Date(r.at).toLocaleString('es-PA', { timeZone: 'America/Panama' })}</p><p>{kinds[r.transaction.kind]} · {money(r.transaction.amount)} USD · {r.transaction.date} · {name(r.transaction.accountId)}{r.transaction.destinationAccountId && ` → ${name(r.transaction.destinationAccountId)}`} · {r.transaction.description}</p></li>)}</ol>
      {(selected.transaction.cardId || selected.transaction.commitmentOccurrenceId) && <p>Operación vinculada. Para corregirla, anula y registra de nuevo desde Tarjetas o confirma el compromiso pendiente.</p>}
      {!selected.transaction.isVoided && <>{!selected.transaction.cardId && !selected.transaction.commitmentOccurrenceId && <button className="secondary-button" disabled={saving} onClick={() => start(selected.transaction.kind, selected.transaction)}>Editar movimiento</button>}<form onSubmit={e => void voidMovement(e)}><label htmlFor="void-reason">Motivo de anulación</label><div className="phone-field"><input id="void-reason" maxLength={100} required disabled={saving} value={voidReason} onChange={e => { setVoidReason(e.target.value); setVoidKey(crypto.randomUUID()); }} /></div><button className="secondary-button" disabled={saving}>Confirmar anulación</button><p>La anulación conserva el registro y retira su efecto de los saldos.</p></form></>}
      <button className="secondary-button" disabled={saving} onClick={() => setSelected(null)}>Cerrar detalle</button>
    </section>}
  </section>;
}
