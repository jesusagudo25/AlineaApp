import { useState } from 'react';
import { InitializationPage } from '../initialization/InitializationPage';
import { AccessPreview } from './AccessPreview';
import { Brand, Icon } from './Icon';

const sections = [
  { id: 'home', label: 'Inicio', icon: 'home' },
  { id: 'activity', label: 'Movimientos', icon: 'activity' },
  { id: 'accounts', label: 'Cuentas', icon: 'accounts' },
  { id: 'calendar', label: 'Compromisos', icon: 'calendar' },
] as const;
type View = typeof sections[number]['id'] | 'access';

function EmptyState({ kind }: { kind: 'activity' | 'accounts' | 'calendar' }) {
  const content = {
    activity: { icon: 'receipt', title: 'Tus movimientos, en un lugar', text: 'Aquí podrás revisar las entradas y salidas de tus cuentas.' },
    accounts: { icon: 'accounts', title: 'Un lugar para cada cuenta', text: 'Aquí podrás organizar tus cuentas y sus saldos de apertura.' },
    calendar: { icon: 'calendar', title: 'Lo que viene, a la vista', text: 'Aquí podrás consultar tus próximos compromisos.' },
  } as const;
  const item = content[kind];
  return <div className="empty-state"><span className="empty-symbol"><Icon name={item.icon} /></span><h3>{item.title}</h3><p>{item.text}</p><span className="availability">Función aún no disponible</span></div>;
}

function HomePreview() {
  return <>
    <div className="overview-grid">
      <section className="balance-panel" aria-labelledby="balance-title">
        <div className="balance-label"><h2 id="balance-title">Saldo disponible</h2><span>USD</span></div>
        <div className="balance-amount" aria-label="Saldo no disponible"><span>$</span> —</div>
        <p>El saldo se mostrará al habilitar tus cuentas.</p>
        <dl className="balance-breakdown"><div><dt>Entradas del mes</dt><dd>—</dd></div><div><dt>Salidas del mes</dt><dd>—</dd></div></dl>
      </section>
      <section className="content-panel accounts-panel" aria-labelledby="accounts-title"><div className="panel-heading"><h2 id="accounts-title">Cuentas</h2><Icon name="accounts" /></div><p className="account-placeholder">Sin cuentas en esta vista</p><p className="panel-description">Organiza de dónde viene y a dónde va tu dinero.</p><button className="secondary-button" disabled><Icon name="plus" />Añadir cuenta</button><span className="action-note">Disponible más adelante</span></section>
    </div>
    <div className="detail-grid">
      <section className="content-panel movements-panel" aria-labelledby="activity-title"><div className="panel-heading"><h2 id="activity-title">Movimientos recientes</h2><span className="subtle-label">Sin datos</span></div><div className="list-columns" aria-hidden="true"><span>Concepto / Cuenta</span><span>Importe</span></div><EmptyState kind="activity" /></section>
      <section className="content-panel commitments-panel" aria-labelledby="calendar-title"><div className="panel-heading"><h2 id="calendar-title">Compromisos</h2><Icon name="calendar" /></div><EmptyState kind="calendar" /></section>
    </div>
  </>;
}

export function DesignPreview() {
  const [view, setView] = useState<View>('home');
  const title = view === 'access' ? 'Acceso' : sections.find(section => section.id === view)!.label;

  return <div className="app-shell">
    <a className="skip-link" href="#main-content">Ir al contenido</a>
    <aside className="sidebar" aria-label="Panel lateral">
      <a className="brand-link" href="/" aria-label="AlineaApp, inicio"><Brand /></a>
      <p className="sidebar-label">EXPLORAR DISEÑO</p>
      <nav aria-label="Navegación principal">{sections.map(section => <button key={section.id} className="nav-item" aria-current={view === section.id ? 'page' : undefined} onClick={() => setView(section.id)}><Icon name={section.icon} /><span>{section.label}</span></button>)}</nav>
      <div className="sidebar-bottom"><span className="sidebar-divider" /><p>Prototipo sin datos.<br /><strong>Diseño en revisión.</strong></p><span className="sidebar-version">Dirección visual · 00B</span></div>
    </aside>
    <div className="workspace">
      <header className="app-header"><a className="mobile-brand brand-link" href="/" aria-label="AlineaApp, inicio"><Brand /></a><div className="desktop-breadcrumb">AlineaApp<span>/</span><strong>{title}</strong></div><button className="access-button" aria-pressed={view === 'access'} onClick={() => setView(view === 'access' ? 'home' : 'access')}><Icon name="phone" /><span>{view === 'access' ? 'Ver inicio' : 'Vista de acceso'}</span></button></header>
      <main id="main-content" tabIndex={-1}>
        <div className="prototype-bar"><span className="prototype-label"><span className="note-dot" />Prototipo visual</span><span>Sin sesión ni datos financieros</span></div>
        {view === 'access' ? <AccessPreview /> : <>
          <div className="page-heading"><div><p className="section-kicker">VISTA DE LA APP</p><h1>{view === 'home' ? 'Resumen' : title}</h1><p>{view === 'home' ? 'Tu saldo, actividad y próximos compromisos.' : 'Vista de estructura · Sin información financiera.'}</p></div><span className="currency-label">Moneda prevista<strong>USD <span>· $</span></strong></span></div>
          {view === 'home' ? <HomePreview /> : <section className="content-panel full-panel" aria-label={`Vista de ${title.toLowerCase()}`}><EmptyState kind={view} /><button className="secondary-button" disabled><Icon name="plus" />{view === 'accounts' ? 'Añadir cuenta' : view === 'activity' ? 'Añadir movimiento' : 'Añadir compromiso'}</button><p className="disabled-explanation">Disponible más adelante. Esta vista solo muestra la estructura.</p></section>}
        </>}
        <footer className="workspace-footer"><span>AlineaApp · Diseño en revisión</span><span>Español · Panamá</span></footer>
        {import.meta.env.DEV && <details className="developer-panel"><summary>Estado técnico · Desarrollo</summary><InitializationPage /></details>}
      </main>
    </div>
    <nav className="mobile-nav" aria-label="Navegación móvil">{sections.map(section => <button key={section.id} aria-current={view === section.id ? 'page' : undefined} onClick={() => setView(section.id)}><Icon name={section.icon} /><span>{section.label}</span></button>)}</nav>
  </div>;
}
