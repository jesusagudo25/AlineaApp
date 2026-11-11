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
        <section className={`technical-status ${state}`} aria-labelledby="state-title" aria-busy={state === 'loading'}>
          <div className="state-heading"><span className="status-dot" aria-hidden="true" /><p>Estado de la plataforma</p></div>
          <div role="status" aria-live="polite" aria-atomic="true"><h2 id="state-title">{copy[state].title}</h2><p>{copy[state].detail}</p></div>
          <button type="button" disabled={state === 'loading'} onClick={() => setAttempt(value => value + 1)}>{state === 'loading' ? 'Comprobando…' : 'Volver a comprobar'}<span aria-hidden="true">↗</span></button>
        </section>
  );
}
