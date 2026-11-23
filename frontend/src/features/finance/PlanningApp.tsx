import { useEffect, useRef, useState, type FormEvent } from 'react';
import { AuthError } from '../auth/api';
import { accounts, categories, catalogRequest, money, type Account, type Category } from './api';
import { cards, plans, forecast, type Card, type Plan, type Forecast, type Pending } from './planning-api';
import { businessToday, validAmount } from './transactions-api';
type Form = { id?: string; version?: string; key: string; name: string; kind: string; accountId: string; openingDate: string; creditLimit: string; initialDebt: string; closingDay: string; paymentDay: string; isActive: boolean; confirm: boolean; locked: boolean; categoryId: string; amount: string; frequency: string; nextDate: string; status: string };
type Operation = { card: Card; kind: 'expense' | 'card_payment'; key: string; accountId: string; categoryId: string; amount: string; date: string; description: string };
type Confirmation = { row: Pending; key: string; accountId: string; date: string };
const frequencies: Record<string, string> = { once: 'Único', weekly: 'Semanal', fortnightly: 'Cada 15 días', monthly: 'Mensual' };
const statuses: Record<string, string> = { active: 'Activo', paused: 'Pausado', inactive: 'Inactivo' };
function blank(mode: 'cards' | 'commitments'): Form { return { key: crypto.randomUUID(), name: '', kind: mode === 'cards' ? 'credit' : 'expense', accountId: '', openingDate: businessToday(), creditLimit: '', initialDebt: '0.00', closingDay: '20', paymentDay: '5', isActive: true, confirm: false, locked: false, categoryId: '', amount: '', frequency: 'once', nextDate: businessToday(), status: 'active' }; }
export function PlanningApp({ mode, onSessionExpired }: { mode: 'cards' | 'commitments'; onSessionExpired: () => void }) {
  const [cardRows, setCards] = useState<Card[]>([]), [planRows, setPlans] = useState<Plan[]>([]), [accountRows, setAccounts] = useState<Account[]>([]), [categoryRows, setCategories] = useState<Category[]>([]);
  const [projection, setProjection] = useState<Forecast | null>(null), [form, setForm] = useState<Form | null>(null), [operation, setOperation] = useState<Operation | null>(null), [confirmation, setConfirmation] = useState<Confirmation | null>(null);
  const [loading, setLoading] = useState(true), [saving, setSaving] = useState(false), [error, setError] = useState(''), [notice, setNotice] = useState(''), [revision, setRevision] = useState(0);
  const [from, setFrom] = useState(businessToday()), [to, setTo] = useState(businessToday()), [period, setPeriod] = useState({ from: businessToday(), to: businessToday() });
  const request = useRef<AbortController | null>(null), input = useRef<HTMLInputElement>(null);
  useEffect(() => () => request.current?.abort(), []);
  useEffect(() => { if (form) input.current?.focus(); }, [form?.id, form !== null]);
  function fail(cause: unknown) { if (cause instanceof AuthError && cause.status === 401) { setError('La sesión venció. Inicia sesión de nuevo.'); onSessionExpired(); return; } setError(cause instanceof AuthError ? cause.message : 'No se pudo completar la operación. Reintenta; los campos y la clave se conservan.'); }
  useEffect(() => {
    const controller = new AbortController(); setLoading(true); setError('');
    void Promise.all([catalogRequest(mode, 'GET', undefined, controller.signal), catalogRequest('accounts', 'GET', undefined, controller.signal), catalogRequest('categories', 'GET', undefined, controller.signal), mode === 'commitments' ? catalogRequest(`commitments/pending?${new URLSearchParams(period)}`, 'GET', undefined, controller.signal) : Promise.resolve(null)])
      .then(([rows, a, c, p]) => { if (!controller.signal.aborted) { if (mode === 'cards') setCards(cards(rows)); else { setPlans(plans(rows)); setProjection(forecast(p)); } setAccounts(accounts(a)); setCategories(categories(c)); } })
      .catch((cause: unknown) => { if (!controller.signal.aborted) fail(cause); }).finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [mode, revision, period]);
  async function write(path: string, method: string, body: unknown, success: string) {
    if (saving) return; const controller = new AbortController(); request.current = controller; setSaving(true); setError(''); setNotice('');
    try { await catalogRequest(path, method, body, controller.signal); if (!controller.signal.aborted) { setForm(null); setOperation(null); setConfirmation(null); setNotice(success); setRevision(v => v + 1); } }
    catch (cause) { if (!controller.signal.aborted) fail(cause); }
    finally { if (!controller.signal.aborted) setSaving(false); }
  }
  function edit(row: Card | Plan) {
    setError(''); setNotice(''); setOperation(null); setConfirmation(null);
    setForm({ ...blank(mode), id: row.id, version: row.version, name: row.name, kind: row.kind,
      ...('creditLimit' in row ? { accountId: row.accountId ?? '', openingDate: row.openingDate, creditLimit: row.creditLimit, initialDebt: row.initialDebt, closingDay: String(row.closingDay ?? ''), paymentDay: String(row.paymentDay ?? ''), locked: row.openingLocked, isActive: row.isActive }
        : { categoryId: row.categoryId, amount: row.amount, frequency: row.frequency, nextDate: row.nextDate ?? '', status: row.status }) });
  }
  function change(patch: Partial<Form>) { setForm(current => current ? { ...current, ...patch, key: crypto.randomUUID() } : null); }
  function submit(event: FormEvent) {
    event.preventDefault(); if (!form || saving) return;
    if (!form.name.trim() || (mode === 'cards' ? !form.openingDate || (form.kind === 'credit' ? !validAmount(form.creditLimit) || !/^\d{1,16}(\.\d{1,2})?$/.test(form.initialDebt) || !/^\d{1,2}$/.test(form.closingDay) || !/^\d{1,2}$/.test(form.paymentDay) : !form.accountId) : !validAmount(form.amount) || !form.categoryId || (!form.nextDate && !form.id))) { setError('Revisa nombre, importes, fechas y relaciones obligatorias.'); return; }
    const body = mode === 'cards' ? { requestId: form.key, name: form.name, kind: form.kind, accountId: form.kind === 'debit' ? form.accountId : null, openingDate: form.openingDate,
      creditLimit: form.kind === 'credit' ? form.creditLimit : '0.00', initialDebt: form.kind === 'credit' ? form.initialDebt : '0.00', closingDay: form.kind === 'credit' ? Number(form.closingDay) : null, paymentDay: form.kind === 'credit' ? Number(form.paymentDay) : null, isActive: form.isActive, version: form.version, confirmOpeningChange: form.confirm }
      : { requestId: form.key, name: form.name, kind: form.kind, categoryId: form.categoryId, amount: form.amount, frequency: form.frequency, nextDate: form.nextDate || null, status: form.status, version: form.version };
    void write(`${mode}${form.id ? `/${form.id}` : ''}`, form.id ? 'PUT' : 'POST', body, 'Cambios guardados.');
  }
  function submitOperation(event: FormEvent) {
    event.preventDefault(); if (!operation || saving) return;
    if (!validAmount(operation.amount) || !operation.date || (operation.kind === 'expense' ? !operation.categoryId : !operation.accountId)) { setError('Revisa importe positivo, fecha, cuenta y categoría.'); return; }
    void write('transactions', 'POST', { requestId: operation.key, kind: operation.kind, cardId: operation.card.id, accountId: operation.card.kind === 'credit' && operation.kind === 'expense' ? null : operation.accountId,
      categoryId: operation.kind === 'expense' ? operation.categoryId : null, amount: operation.amount, date: operation.date, description: operation.description }, 'Operación registrada una sola vez en Movimientos.');
  }
  function submitConfirmation(event: FormEvent) {
    event.preventDefault(); if (!confirmation || saving) return;
    if (!confirmation.accountId || !confirmation.date) { setError('Selecciona cuenta y fecha real.'); return; }
    void write(`commitments/${confirmation.row.commitmentId}/confirm`, 'POST', { requestId: confirmation.key, version: confirmation.row.version, dueDate: confirmation.row.dueDate, date: confirmation.date, accountId: confirmation.accountId }, 'Compromiso realizado mediante una transacción real vinculada.');
  }
  function closeForms() { setForm(null); setOperation(null); setConfirmation(null); setError(''); }
  const textField = (id: string, label: string, value: string, update: (value: string) => void, type = 'text', disabled = false) => <><label htmlFor={id}>{label}</label><div className="phone-field"><input ref={id === 'planning-name' ? input : undefined} id={id} type={type} inputMode={id.includes('amount') || id.includes('debt') || id.includes('limit') ? 'decimal' : undefined} maxLength={id === 'planning-name' ? 100 : 500} disabled={saving || disabled} value={value} onChange={e => update(e.target.value)} /></div></>;
  const accountSelect = (id: string, label: string, value: string, update: (value: string) => void, disabled = false) => <><label htmlFor={id}>{label}</label><select id={id} required disabled={saving || disabled} value={value} onChange={e => update(e.target.value)}><option value="">Selecciona una cuenta</option>{accountRows.filter(a => a.isActive || a.id === value).map(a => <option key={a.id} value={a.id}>{a.name}{a.isActive ? '' : ' (inactiva)'}</option>)}</select></>;
  const actions = (label: string) => <div className="catalog-actions"><button className="primary-button" disabled={saving} type="submit">{saving ? 'Guardando…' : label}</button><button className="secondary-button" disabled={saving} type="button" onClick={closeForms}>Cancelar</button></div>;
  return <section aria-label={mode === 'cards' ? 'Gestión de tarjetas' : 'Gestión de compromisos'}>
    <header className="catalog-heading"><h1>{mode === 'cards' ? 'Tarjetas' : 'Compromisos'}</h1><button className="secondary-button" disabled={loading || saving} onClick={() => { closeForms(); setNotice(''); setForm(blank(mode)); }}>{mode === 'cards' ? 'Añadir tarjeta' : 'Añadir compromiso'}</button></header>
    {notice && <p role="status">{notice}</p>}{error && <div role="alert"><p>{error}</p><button className="secondary-button" disabled={saving} onClick={() => setRevision(v => v + 1)}>Actualizar consulta</button></div>}
    {form && <form className="catalog-form" aria-label={mode === 'cards' ? 'Formulario de tarjeta' : 'Formulario de compromiso'} onSubmit={submit} noValidate aria-busy={saving}>
      <h2>{form.id ? 'Editar' : 'Añadir'} {mode === 'cards' ? 'tarjeta' : 'compromiso'}</h2>
      {textField('planning-name', mode === 'cards' ? 'Alias de tarjeta' : 'Concepto', form.name, name => change({ name }))}
      <label htmlFor="planning-kind">{mode === 'cards' ? 'Tipo de tarjeta' : 'Tipo previsto'}</label><select id="planning-kind" disabled={saving || mode === 'cards' && !!form.id} value={form.kind} onChange={e => change({ kind: e.target.value, categoryId: '' })}>{mode === 'cards' ? <><option value="credit">Crédito</option><option value="debit">Débito</option></> : <><option value="expense">Gasto previsto</option><option value="income">Ingreso previsto</option></>}</select>
      {mode === 'cards' ? <>
        {form.kind === 'debit' && accountSelect('card-account', 'Cuenta vinculada', form.accountId, accountId => change({ accountId }), form.locked)}
        {textField('card-opening', 'Fecha inicial de tarjeta', form.openingDate, openingDate => change({ openingDate }), 'date', form.locked)}
        {form.kind === 'credit' && <>{textField('card-limit', 'Límite de crédito (USD)', form.creditLimit, creditLimit => change({ creditLimit }))}{textField('card-debt', 'Deuda inicial (USD)', form.initialDebt, initialDebt => change({ initialDebt }), 'text', form.locked)}{textField('card-closing', 'Día de corte (1–31)', form.closingDay, closingDay => change({ closingDay }), 'number')}{textField('card-payment', 'Día de pago (1–31)', form.paymentDay, paymentDay => change({ paymentDay }), 'number')}</>}
        <p>No introduzcas números de tarjeta ni CVV. En meses cortos, el día configurado corresponde al último día del mes.</p>
        {form.id && <><label className="catalog-check"><input type="checkbox" disabled={saving} checked={form.isActive} onChange={e => change({ isActive: e.target.checked })} />Tarjeta activa</label>{form.locked ? <p>Apertura protegida por operaciones registradas.</p> : <label className="catalog-check"><input type="checkbox" disabled={saving} checked={form.confirm} onChange={e => change({ confirm: e.target.checked })} />Confirmo cambios de apertura</label>}</>}
      </> : <>
        <label htmlFor="planning-category">Categoría prevista</label><select id="planning-category" required disabled={saving} value={form.categoryId} onChange={e => change({ categoryId: e.target.value })}><option value="">Selecciona categoría</option>{categoryRows.filter(c => c.kind === form.kind && (c.isActive || c.id === form.categoryId)).map(c => <option key={c.id} value={c.id}>{c.name}</option>)}</select>
        {textField('planning-amount', 'Importe previsto (USD)', form.amount, amount => change({ amount }))}
        <label htmlFor="planning-frequency">Frecuencia</label><select id="planning-frequency" disabled={saving} value={form.frequency} onChange={e => change({ frequency: e.target.value })}>{Object.entries(frequencies).map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select>
        {textField('planning-next', 'Próxima fecha', form.nextDate, nextDate => change({ nextDate }), 'date')}
        <label htmlFor="planning-status">Estado del compromiso</label><select id="planning-status" disabled={saving} value={form.status} onChange={e => change({ status: e.target.value })}>{Object.entries(statuses).map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select>
        <p>Editar cambia las próximas previsiones. Las ocurrencias ya confirmadas conservan sus datos originales.</p>
      </>}{actions('Guardar')}
    </form>}
    {operation && <form className="catalog-form" onSubmit={submitOperation} aria-label="Operación de tarjeta" noValidate><h2>{operation.kind === 'expense' ? 'Compra' : 'Pago'} · {operation.card.name}</h2>
      {(operation.card.kind === 'debit' || operation.kind === 'card_payment') && accountSelect('operation-account', 'Cuenta para operación', operation.accountId, accountId => setOperation({ ...operation, accountId, key: crypto.randomUUID() }), operation.card.kind === 'debit')}
      {operation.kind === 'expense' && <><label htmlFor="operation-category">Categoría de compra</label><select id="operation-category" disabled={saving} required value={operation.categoryId} onChange={e => setOperation({ ...operation, categoryId: e.target.value, key: crypto.randomUUID() })}><option value="">Selecciona categoría</option>{categoryRows.filter(c => c.isActive && c.kind === 'expense').map(c => <option key={c.id} value={c.id}>{c.name}</option>)}</select></>}
      {textField('operation-amount', 'Importe de operación (USD)', operation.amount, amount => setOperation({ ...operation, amount, key: crypto.randomUUID() }))}{textField('operation-date', 'Fecha real de operación', operation.date, date => setOperation({ ...operation, date, key: crypto.randomUUID() }), 'date')}{textField('operation-description', 'Descripción de operación', operation.description, description => setOperation({ ...operation, description, key: crypto.randomUUID() }))}
      <p>{operation.kind === 'card_payment' ? 'El pago reduce cuenta y deuda; no añade otro gasto.' : operation.card.kind === 'credit' ? 'La compra aumenta la deuda y el gasto; no reduce efectivo.' : 'La compra se registra como un único gasto en la cuenta vinculada.'} Consulta y anula desde Movimientos.</p>{actions('Registrar operación')}
    </form>}
    {confirmation && <form className="catalog-form" onSubmit={submitConfirmation} aria-label="Confirmar compromiso" noValidate><h2>Realizar · {confirmation.row.concept}</h2><p>Previsto: {confirmation.row.dueDate} · {money(confirmation.row.amount)} USD</p>{accountSelect('confirmation-account', 'Cuenta de realización', confirmation.accountId, accountId => setConfirmation({ ...confirmation, accountId, key: crypto.randomUUID() }))}{textField('confirmation-date', 'Fecha real de realización', confirmation.date, date => setConfirmation({ ...confirmation, date, key: crypto.randomUUID() }), 'date')}<p>Se creará una transacción real vinculada. La previsión dejará de estar pendiente.</p>{actions('Confirmar realización')}</form>}
    {mode === 'commitments' && <form className="catalog-form" onSubmit={e => { e.preventDefault(); if (!from || !to) { setError('Selecciona fechas del período.'); return; } setPeriod({ from, to }); }} aria-label="Período de compromisos">{textField('period-from', 'Período desde', from, setFrom, 'date')}{textField('period-to', 'Período hasta', to, setTo, 'date')}<button className="secondary-button" disabled={saving || loading}>Consultar período</button></form>}
    {loading ? <p role="status">Cargando {mode === 'cards' ? 'tarjetas' : 'compromisos'}…</p> : error ? null : mode === 'cards' ? <>
      {!cardRows.length && <p>No hay tarjetas registradas.</p>}<ul className="catalog-list">{cardRows.map(card => <li key={card.id}><div><h2>{card.name}</h2><p>{card.kind === 'credit' ? 'Crédito' : 'Débito'} · {card.isActive ? 'Activa' : 'Inactiva'}</p>{card.kind === 'credit' ? <><p>Límite: {money(card.creditLimit)} USD · Deuda actual: {money(card.currentDebt!)} USD · Crédito disponible: {money(card.availableCredit!)} USD</p><p>Deuda inicial: {money(card.initialDebt)} USD · Corte: día {card.closingDay} · Pago: día {card.paymentDay}</p></> : <p>Cuenta: {accountRows.find(a => a.id === card.accountId)?.name ?? 'No disponible'}</p>}
      <div className="catalog-actions"><button className="secondary-button" disabled={saving} onClick={() => edit(card)} aria-label={`Editar ${card.name}`}>Editar</button>{(['expense', ...(card.kind === 'credit' ? ['card_payment'] : [])] as Operation['kind'][]).map(kind => <button key={kind} className="secondary-button" disabled={saving || !card.isActive} onClick={() => { closeForms(); setNotice(''); setOperation({ card, kind, accountId: card.accountId ?? '', categoryId: '', amount: '', date: businessToday(), description: '', key: crypto.randomUUID() }); }} aria-label={`${kind === 'expense' ? 'Comprar con' : 'Pagar'} ${card.name}`}>{kind === 'expense' ? 'Registrar compra' : 'Registrar pago'}</button>)}</div></div></li>)}</ul>
    </> : <>
      {projection && <><p>Saldo real disponible: {money(projection.actualAvailable)} USD</p><p>Gastos previstos pendientes: {money(projection.pendingExpenses)} USD</p><p>Disponible estimado: {money(projection.estimatedAvailable)} USD</p><p>No incluye ingresos futuros ni crédito disponible. Solo cuentas activas; las previsiones no reservan ni mueven dinero.</p><h2>Pendientes del período</h2>{!projection.items.length && <p>No hay compromisos pendientes en este período.</p>}<ul className="catalog-list">{projection.items.map(row => <li key={`${row.commitmentId}/${row.dueDate}`}><div><h3>{row.concept}</h3><p>{row.dueDate} · {row.kind === 'expense' ? 'Gasto previsto' : 'Ingreso previsto'} · {money(row.amount)} USD</p></div><button className="secondary-button" disabled={saving} aria-label={`Realizar ${row.concept} ${row.dueDate}`} onClick={() => { closeForms(); setNotice(''); setConfirmation({ row, key: crypto.randomUUID(), date: businessToday(), accountId: '' }); }}>Realizar</button></li>)}</ul></>}
      <h2>Programaciones</h2>{!planRows.length && <p>No hay compromisos registrados.</p>}<ul className="catalog-list">{planRows.map(plan => <li key={plan.id}><div><h3>{plan.name}</h3><p>{statuses[plan.status]} · {frequencies[plan.frequency]} · {money(plan.amount)} USD · Próxima: {plan.nextDate ?? 'Sin próxima fecha'}</p></div><button className="secondary-button" disabled={saving} onClick={() => edit(plan)} aria-label={`Editar ${plan.name}`}>Editar</button></li>)}</ul>
    </>}
  </section>;
}
