import { useEffect, useState } from 'react';
import { getPlatformState } from './api';
import type { PlatformState } from './api';

type State = PlatformState | 'loading' | 'error';
const copy: Record<State, { title: string; detail: string }> = {
  loading: { title: 'Comprobando la plataforma…', detail: 'Consultando la API y la disponibilidad de PostgreSQL.' },
  ready: { title: 'La base está lista.', detail: 'La API responde, las migraciones están aplicadas y la configuración inicial está disponible.' },
  unavailable: { title: 'Inicialización pendiente.', detail: 'La API responde. PostgreSQL o la configuración inicial todavía no están disponibles.' },
  empty: { title: 'Sin información de estado.', detail: 'El servicio no devolvió información suficiente. Vuelve a comprobar en un momento.' },
  error: { title: 'No pudimos conectar.', detail: 'Comprueba que el servicio local esté iniciado y vuelve a intentarlo.' },
};

export function InitializationPage() {
  const [state, setState] = useState<State>('loading');
  const [attempt, setAttempt] = useState(0);
  useEffect(() => {
    const controller = new AbortController();
    setState('loading');
    void getPlatformState(controller.signal).then(value => {
      if (!controller.signal.aborted) setState(value);
    }).catch(() => {
      if (!controller.signal.aborted) setState('error');
    });
    return () => controller.abort();
  }, [attempt]);

  return (
    <div className="shell">
      <header><a className="wordmark" href="/" aria-label="AlineaApp, inicio"><span className="brand-mark" aria-hidden="true">a</span>AlineaApp</a><span className="environment">Entorno local</span></header>
      <main>
        <p className="eyebrow">FASE 00 / FUNDACIONES</p>
        <h1>Un punto de partida<br />para tener claridad.</h1>
        <p className="intro">Estamos preparando la base de AlineaApp, tu futuro espacio para organizar tus finanzas personales.</p>
        <section className={`service-state ${state}`} aria-labelledby="state-title" aria-busy={state === 'loading'}>
          <div className="state-heading"><span className="status-dot" aria-hidden="true" /><p>Estado de la plataforma</p></div>
          <div role="status" aria-live="polite" aria-atomic="true"><h2 id="state-title">{copy[state].title}</h2><p>{copy[state].detail}</p></div>
          <button type="button" disabled={state === 'loading'} onClick={() => setAttempt(value => value + 1)}>{state === 'loading' ? 'Comprobando…' : 'Volver a comprobar'}<span aria-hidden="true">↗</span></button>
        </section>
        <section className="scope" aria-labelledby="scope-title"><h2 id="scope-title">Lo que estamos construyendo</h2><dl><div><dt>Ahora</dt><dd>Plataforma local y conexión con la base de datos.</dd></div><div><dt>Después</dt><dd>Registro restringido y acceso seguro por SMS.</dd></div></dl><p className="scope-note">El acceso de usuarios y las funciones financieras aún no están habilitados.</p></section>
      </main>
      <footer><span>AlineaApp · Base técnica</span><span>Español · America/Panama</span></footer>
    </div>
  );
}
