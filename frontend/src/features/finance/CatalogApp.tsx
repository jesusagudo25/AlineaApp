import { useEffect, useRef, useState, type FormEvent } from 'react';
import { AuthError } from '../auth/api';
import { accounts, banks, categories, catalogRequest, money, type Account, type Bank, type Catalog, type Category } from './api';
import './catalog.css';
import { TransactionsApp } from './TransactionsApp';
import { PlanningApp } from './PlanningApp';

type Form = { id: string | null; version?: string; name: string; kind: string; bankId: string; openingBalance: string; openingDate: string; isActive: boolean; confirm: boolean; locked: boolean };
const titles: Record<Catalog, string> = { banks: 'Bancos', accounts: 'Cuentas', categories: 'Categorías' };
const singular: Record<Catalog, string> = { banks: 'banco', accounts: 'cuenta', categories: 'categoría' };
function emptyForm(tab: Catalog): Form { return { id: null, name: '', kind: tab === 'accounts' ? 'cash' : 'expense', bankId: '', openingBalance: '', openingDate: '', isActive: true, confirm: false, locked: false }; }

function CatalogContent({ onSessionExpired }: { onSessionExpired: () => void }) {
  const [tab, setTab] = useState<Catalog>('accounts');
  const [bankRows, setBanks] = useState<Bank[]>([]);
  const [accountRows, setAccounts] = useState<Account[]>([]);
  const [categoryRows, setCategories] = useState<Category[]>([]);
  const [form, setForm] = useState<Form | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [formError, setFormError] = useState('');
  const [notice, setNotice] = useState('');
  const [revision, setRevision] = useState(0);
  const [inactive, setInactive] = useState(false);
  const title = useRef<HTMLHeadingElement>(null);
  const input = useRef<HTMLInputElement>(null);
  const activeRequest = useRef<AbortController | null>(null);
  useEffect(() => () => activeRequest.current?.abort(), []);
  useEffect(() => { title.current?.focus(); }, [tab]);
  useEffect(() => { if (form) input.current?.focus(); }, [form?.id, form !== null]);

  useEffect(() => {
    const controller = new AbortController(); setLoading(true); setError('');
    async function load() {
      try {
        const [bankData, accountData, categoryData] = await Promise.all([
          catalogRequest('banks', 'GET', undefined, controller.signal), catalogRequest('accounts', 'GET', undefined, controller.signal), catalogRequest('categories', 'GET', undefined, controller.signal)
        ]);
        if (!controller.signal.aborted) { setBanks(banks(bankData)); setAccounts(accounts(accountData)); setCategories(categories(categoryData)); }
      } catch (cause) { if (!controller.signal.aborted) fail(cause, false); }
      finally { if (!controller.signal.aborted) setLoading(false); }
    }
    void load(); return () => controller.abort();
  }, [revision]);

  function fail(cause: unknown, inForm: boolean) {
    if (cause instanceof AuthError && cause.status === 401) { onSessionExpired(); return; }
    const message = cause instanceof AuthError ? cause.message : 'No se pudo cargar o guardar la información. Inténtalo de nuevo.';
    if (inForm) setFormError(message); else setError(message);
  }
  function navigate(next: Catalog) { setTab(next); setForm(null); setFormError(''); setNotice(''); setInactive(false); }
  function edit(row: Bank | Account | Category) {
    setForm({ id: row.id, version: row.version, name: row.name, isActive: row.isActive, kind: 'kind' in row ? row.kind : '',
      bankId: 'bankId' in row ? row.bankId ?? '' : '', openingBalance: 'openingBalance' in row ? row.openingBalance : '',
      openingDate: 'openingDate' in row ? row.openingDate : '', locked: 'openingLocked' in row ? row.openingLocked : false, confirm: false }); setFormError(''); setNotice('');
  }
  async function submit(event: FormEvent) {
    event.preventDefault(); if (!form || saving) return;
    const name = form.name.trim();
    if (!name || name.length > 100) { setFormError('Introduce un nombre de entre 1 y 100 caracteres.'); return; }
    if (tab === 'accounts' && (!/^-?\d{1,16}(\.\d{1,2})?$/.test(form.openingBalance) || !form.openingDate || (form.kind === 'bank' && !form.bankId)
      || (form.kind === 'cash' && form.openingBalance.startsWith('-')))) { setFormError('Revisa banco, fecha y saldo inicial (máximo dos decimales; efectivo no negativo).'); return; }
    const body = { name, isActive: form.isActive, version: form.version,
      ...(tab === 'categories' ? { kind: form.kind } : {}),
      ...(tab === 'accounts' ? { kind: form.kind, bankId: form.kind === 'bank' ? form.bankId : null, openingBalance: form.openingBalance, openingDate: form.openingDate, currency: 'USD', confirmOpeningChange: form.confirm } : {}) };
    const controller = new AbortController(); activeRequest.current = controller;
    setSaving(true); setFormError(''); setNotice('');
    try {
      await catalogRequest(`${tab}${form.id ? `/${form.id}` : ''}`, form.id ? 'PUT' : 'POST', body, controller.signal);
      if (!controller.signal.aborted) { setForm(null); setNotice('Cambios guardados.'); setRevision(value => value + 1); title.current?.focus(); }
    } catch (cause) { if (!controller.signal.aborted) fail(cause, true); }
    finally { if (!controller.signal.aborted) setSaving(false); }
  }
  async function initialize() {
    if (saving) return; setSaving(true); setError(''); setNotice('');
    const controller = new AbortController(); activeRequest.current = controller;
    try { await catalogRequest('categories/initialize', 'POST', {}, controller.signal); if (!controller.signal.aborted) { setNotice('Categorías iniciales revisadas y añadidas cuando faltaban.'); setRevision(value => value + 1); } }
    catch (cause) { if (!controller.signal.aborted) fail(cause, false); }
    finally { if (!controller.signal.aborted) setSaving(false); }
  }
  const allRows: Array<Bank | Account | Category> = tab === 'banks' ? bankRows : tab === 'accounts' ? accountRows : categoryRows;
  const rows = allRows.filter(row => inactive || row.isActive);
  const change = (patch: Partial<Form>) => setForm(current => current ? { ...current, ...patch } : null);
  return <section className="catalog" aria-label="Gestión financiera">
    <nav className="catalog-nav" aria-label="Catálogos financieros">{(['banks', 'accounts', 'categories'] as Catalog[]).map(item =>
      <button key={item} disabled={saving} aria-current={tab === item ? 'page' : undefined} onClick={() => navigate(item)}>{titles[item]}</button>)}</nav>
    <header className="catalog-heading"><h1 ref={title} tabIndex={-1}>{titles[tab]}</h1><button className="secondary-button" disabled={saving || loading} onClick={() => { setForm(emptyForm(tab)); setFormError(''); setNotice(''); }}>Añadir {singular[tab]}</button></header>
    {notice && <p role="status">{notice}</p>}
    {error && <div role="alert"><p>{error}</p><button disabled={saving} onClick={() => setRevision(value => value + 1)}>Reintentar</button></div>}
    {form && <form className="catalog-form" onSubmit={event => void submit(event)} noValidate aria-busy={saving} aria-labelledby="catalog-form-title">
      <h2 id="catalog-form-title">{form.id ? 'Editar' : 'Añadir'} {singular[tab]}</h2>
      <label htmlFor="catalog-name">Nombre</label><div className="phone-field"><input ref={input} id="catalog-name" value={form.name} maxLength={100} required disabled={saving} onChange={event => change({ name: event.target.value })} aria-invalid={!!formError} aria-describedby={formError ? 'catalog-form-error' : undefined} /></div>
      {tab !== 'banks' && <><label htmlFor="catalog-kind">Tipo</label><select id="catalog-kind" value={form.kind} disabled={saving || form.locked} onChange={event => change({ kind: event.target.value, bankId: '' })}>
        {tab === 'accounts' ? <><option value="cash">Efectivo</option><option value="bank">Bancaria</option></> : <><option value="expense">Gasto</option><option value="income">Ingreso</option></>}
      </select></>}
      {tab === 'accounts' && <>
        {form.kind === 'bank' && <><label htmlFor="catalog-bank">Banco</label><select id="catalog-bank" required value={form.bankId} disabled={saving || form.locked} onChange={event => change({ bankId: event.target.value })}>
          <option value="">Selecciona un banco</option>{bankRows.filter(bank => bank.isActive || bank.id === form.bankId).map(bank => <option key={bank.id} value={bank.id}>{bank.name}{!bank.isActive ? ' (inactivo)' : ''}</option>)}
        </select>{!bankRows.some(bank => bank.isActive) && <p>Registra un banco activo en Bancos para asociar una cuenta bancaria.</p>}</>}
        <label htmlFor="catalog-balance">Saldo inicial (USD)</label><div className="phone-field"><input id="catalog-balance" inputMode="decimal" type="text" required value={form.openingBalance} disabled={saving || form.locked} onChange={event => change({ openingBalance: event.target.value })} aria-describedby="balance-hint" /></div>
        <p className="field-hint" id="balance-hint">Usa punto decimal y hasta dos decimales. Efectivo no admite saldo negativo.</p>
        <label htmlFor="catalog-date">Fecha de apertura</label><div className="phone-field"><input id="catalog-date" type="date" required value={form.openingDate} disabled={saving || form.locked} onChange={event => change({ openingDate: event.target.value })} /></div>
        {form.locked ? <p>La apertura está protegida por movimientos y no puede modificarse.</p> : form.id && <label className="catalog-check"><input type="checkbox" checked={form.confirm} disabled={saving} onChange={event => change({ confirm: event.target.checked })} />Confirmo cualquier cambio de saldo o fecha de apertura</label>}
      </>}
      {form.id && <label className="catalog-check"><input type="checkbox" checked={form.isActive} disabled={saving} onChange={event => change({ isActive: event.target.checked })} />{tab === 'banks' ? 'Banco activo' : tab === 'accounts' ? 'Cuenta activa' : 'Categoría activa'}</label>}
      {formError && <p className="field-error" id="catalog-form-error" role="alert">{formError}</p>}
      <div className="catalog-actions"><button className="primary-button" disabled={saving} type="submit">{saving ? 'Guardando…' : 'Guardar'}</button><button className="secondary-button" disabled={saving} type="button" onClick={() => { setForm(null); title.current?.focus(); }}>Cancelar</button></div>
    </form>}
    <div className="catalog-tools"><label className="catalog-check"><input type="checkbox" checked={inactive} onChange={event => setInactive(event.target.checked)} />Mostrar inactivos</label>
      {tab === 'categories' && <button className="secondary-button" disabled={saving || loading} onClick={() => void initialize()}>Añadir categorías iniciales</button>}</div>
    {loading ? <p role="status">Cargando {titles[tab].toLowerCase()}…</p> : error ? null : rows.length === 0 ? <p>No hay {titles[tab].toLowerCase()} {tab === 'banks' ? inactive ? 'registrados' : 'activos' : inactive ? 'registradas' : 'activas'}. Añade {tab === 'banks' ? 'el primero' : 'la primera'} para comenzar.</p> :
      <ul className="catalog-list">{rows.map(row => <li key={row.id}>
        <div><h2>{row.name}</h2><p>{row.isActive ? 'Activo' : 'Inactivo'}{'kind' in row ? row.kind === 'bank' ? ' · Bancaria' : row.kind === 'cash' ? ' · Efectivo' : row.kind === 'income' ? ' · Ingreso' : ' · Gasto' : ''}{'isInitial' in row && row.isInitial ? ' · Inicial' : ''}</p>
          {'bankId' in row && row.bankId && <p>Banco: {bankRows.find(bank => bank.id === row.bankId)?.name ?? 'No disponible'}</p>}
          {'openingBalance' in row && <dl><div><dt>Saldo inicial</dt><dd>{money(row.openingBalance)} USD</dd></div><div><dt>Saldo actual</dt><dd>{money(row.currentBalance)} USD</dd></div><div><dt>Apertura</dt><dd>{row.openingDate}</dd></div></dl>}
        </div><button className="secondary-button" disabled={saving} onClick={() => edit(row)} aria-label={`Editar ${row.name}`}>Editar</button>
      </li>)}</ul>}
    {tab === 'accounts' && <p className="field-hint">Saldo actual: apertura más ingresos, menos gastos, más transferencias recibidas y menos enviadas. Los movimientos anulados no afectan el saldo.</p>}
  </section>;
}

export function CatalogApp({ onSessionExpired }: { onSessionExpired: () => void }) {
  const [section, setSection] = useState<'catalogs' | 'movements' | 'cards' | 'commitments'>('catalogs');
  const labels = { movements: 'Movimientos', catalogs: 'Catálogos', cards: 'Tarjetas', commitments: 'Compromisos' } as const;
  return <><nav className="finance-sections" aria-label="Finanzas">{(Object.keys(labels) as Array<keyof typeof labels>).map(key => <button key={key} className="secondary-button" aria-current={section === key ? 'page' : undefined} onClick={() => setSection(key)}>{labels[key]}</button>)}</nav>{section === 'catalogs' ? <CatalogContent onSessionExpired={onSessionExpired} /> : section === 'movements' ? <TransactionsApp onSessionExpired={onSessionExpired} /> : <PlanningApp key={section} mode={section} onSessionExpired={onSessionExpired} />}</>;
}
