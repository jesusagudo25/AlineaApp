import { useEffect, useRef, useState, type FormEvent } from 'react';
import { Icon } from '../design-preview/Icon';
import { authApi, AuthError, isRecord } from './api';

type Profile = { id: string; phone: string; name: string | null; email: string | null };
function profile(value: unknown): Profile {
  if (!isRecord(value) || typeof value.id !== 'string' || typeof value.phone !== 'string'
    || !(value.name === null || typeof value.name === 'string') || !(value.email === null || typeof value.email === 'string')) throw new Error('Respuesta de sesión no válida.');
  return value as Profile;
}

export function AuthApp() {
  const [stage, setStage] = useState<'loading' | 'phone' | 'code' | 'profile' | 'session'>('loading');
  const [purpose, setPurpose] = useState<'login' | 'register'>('login');
  const [phone, setPhone] = useState('');
  const [code, setCode] = useState('');
  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  const [challenge, setChallenge] = useState('');
  const proof = useRef('');
  const [user, setUser] = useState<Profile | null>(null);
  const [delivery, setDelivery] = useState('');
  const [until, setUntil] = useState(0);
  const [seconds, setSeconds] = useState(0);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const heading = useRef<HTMLHeadingElement>(null);

  useEffect(() => {
    let active = true;
    const controller = new AbortController();
    async function load() {
      try {
        const status = await authApi('/status', undefined, controller.signal);
        if (!isRecord(status) || typeof status.deliveryMode !== 'string') throw new Error('Estado del servicio no válido.');
        if (active) setDelivery(status.deliveryMode);
        try {
          const current = profile(await authApi('/session', undefined, controller.signal));
          if (active) { setUser(current); setStage('session'); }
        } catch (cause) {
          if (!(cause instanceof AuthError && cause.status === 401)) throw cause;
          if (active) setStage('phone');
        }
      } catch (cause) { if (active) { setError(message(cause)); setStage('phone'); } }
    }
    void load(); return () => { active = false; controller.abort(); };
  }, []);
  useEffect(() => { heading.current?.focus(); }, [stage]);
  useEffect(() => {
    const tick = () => setSeconds(Math.max(0, Math.ceil((until - Date.now()) / 1000)));
    tick(); const timer = window.setInterval(tick, 1000); return () => window.clearInterval(timer);
  }, [until]);

  async function act(action: () => Promise<void>) {
    if (busy) return;
    setBusy(true); setError(''); setNotice('');
    try { await action(); } catch (cause) { setError(message(cause)); } finally { setBusy(false); }
  }
  async function request() {
    const international = /^\d{8}$/.test(phone.trim()) ? `+507${phone.trim()}` : phone.trim();
    const result = await authApi('/otp/request', { phone: international, purpose });
    if (!isRecord(result) || typeof result.challengeId !== 'string' || typeof result.retryAfterSeconds !== 'number') throw new Error('Respuesta de código no válida.');
    setChallenge(result.challengeId); setSeconds(result.retryAfterSeconds); setUntil(Date.now() + result.retryAfterSeconds * 1000); setCode(''); proof.current = ''; setStage('code');
    setNotice('Si el número es elegible, recibirás un código.');
  }
  async function finish(token: string) {
    await authApi(purpose === 'register' ? '/register' : '/login', {
      verificationToken: token, ...(purpose === 'register' ? { name: name.trim() || null, email: email.trim() || null } : {}),
    });
    proof.current = ''; setCode(''); setPhone(''); setName(''); setEmail('');
    setUser(profile(await authApi('/session'))); setStage('session');
  }
  async function submit(event: FormEvent) {
    event.preventDefault();
    await act(async () => {
      if (stage === 'phone') await request();
      else if (stage === 'code') {
        const result = await authApi('/otp/verify', { challengeId: challenge, code });
        if (!isRecord(result) || typeof result.verificationToken !== 'string') throw new Error('Respuesta de verificación no válida.');
        setCode('');
        if (purpose === 'register') { proof.current = result.verificationToken; setStage('profile'); }
        else await finish(result.verificationToken);
      } else if (stage === 'profile') await finish(proof.current);
    });
  }
  function reset() { setStage('phone'); setCode(''); proof.current = ''; setError(''); setNotice(''); }

  return <main>
    <section className="access-layout" aria-labelledby="auth-title">
      <div className="access-context">
        <span className="section-kicker">ALINEAAPP</span>
        <h1 id="auth-title">Acceso personal</h1>
        <p>Tu teléfono es tu identificador de acceso.</p>
        <div className="access-principle"><Icon name="lock" /><div><strong>Un espacio privado</strong><p>Verifica tu teléfono para acceder a tu perfil.</p></div></div>
        {delivery === 'local-test' && <div className="preview-note"><p>Pruebas locales: no se envían SMS reales.</p></div>}
      </div>
      <div className="access-form-panel" aria-busy={busy || stage === 'loading'}>
        {stage === 'phone' && <div className="preview-switch" aria-label="Tipo de acceso">
          <button disabled={busy} aria-pressed={purpose === 'login'} onClick={() => setPurpose('login')}>Iniciar sesión</button>
          <button disabled={busy} aria-pressed={purpose === 'register'} onClick={() => setPurpose('register')}>Registrarme</button>
        </div>}
        <h2 ref={heading} tabIndex={-1}>{stage === 'loading' ? 'Comprobando sesión…' : stage === 'session' ? 'Tu perfil' : stage === 'code' ? 'Verificar tu teléfono' : stage === 'profile' ? 'Completar registro' : purpose === 'register' ? 'Crear acceso' : 'Entrar a AlineaApp'}</h2>
        {stage !== 'loading' && stage !== 'session' && <p className="form-intro">{stage === 'phone' ? 'Verifica tu teléfono con un código de un solo uso.' : stage === 'code' ? 'Introduce el código para continuar.' : 'Tu teléfono está verificado. Completa los datos opcionales.'}</p>}
        {error && <p className="field-error" role="alert">{error}</p>}
        {notice && <p role="status">{notice}</p>}
        {stage === 'session' && user ? <>
          <p>{user.name || 'Sesión iniciada'}</p><p>{user.phone}</p>{user.email && <p>{user.email}</p>}
          <button className="primary-button" disabled={busy} onClick={() => void act(async () => { await authApi('/logout', {}); setUser(null); reset(); })}>Cerrar sesión</button>
        </> : stage !== 'loading' && <form onSubmit={event => void submit(event)}>
          {stage === 'phone' && <>
            <label htmlFor="phone">Número de teléfono</label>
            <div className="phone-field"><input id="phone" type="tel" inputMode="tel" autoComplete="tel" placeholder="+507 · Tu número" maxLength={40} required value={phone} onChange={event => setPhone(event.target.value)} aria-describedby="phone-hint" disabled={busy} /></div>
            <p className="field-hint" id="phone-hint">Formato internacional o 8 dígitos para Panamá. El registro está sujeto a autorización.</p>
            {delivery === 'unavailable' && <p role="status">El envío SMS no está configurado.</p>}
          </>}
          {stage === 'code' && <>
            <label htmlFor="code">Código de seis dígitos</label>
            <input className="code-field" id="code" type="text" inputMode="numeric" autoComplete="one-time-code" pattern="[0-9]{6}" maxLength={6} required value={code} onChange={event => setCode(event.target.value.replace(/\D/g, ''))} disabled={busy} aria-describedby="code-hint" />
            <p className="field-hint" id="code-hint">El código vence en cinco minutos y solo se puede usar una vez.</p>
          </>}
          {stage === 'profile' && <>
            <p>Los siguientes datos son opcionales.</p>
            <label htmlFor="name">Nombre (opcional)</label><div className="phone-field"><input id="name" autoComplete="name" maxLength={120} value={name} onChange={event => setName(event.target.value)} disabled={busy} /></div>
            <label htmlFor="email">Correo (opcional)</label><div className="phone-field"><input id="email" type="email" autoComplete="email" maxLength={254} value={email} onChange={event => setEmail(event.target.value)} disabled={busy} /></div>
          </>}
          <button className="primary-button" type="submit" disabled={busy || (stage === 'phone' && delivery === 'unavailable')}>{busy ? 'Procesando…' : stage === 'phone' ? 'Solicitar código' : stage === 'code' ? 'Verificar código' : 'Completar registro'}<Icon name="arrow" /></button>
          {stage === 'code' && <button type="button" disabled={busy || seconds > 0} onClick={() => void act(request)}>{seconds > 0 ? `Reenviar en ${seconds} s` : 'Reenviar código'}</button>}
          {stage !== 'phone' && <button type="button" disabled={busy} onClick={reset}>Volver al teléfono</button>}
        </form>}
      </div>
    </section>
  </main>;
}
function message(cause: unknown): string { return cause instanceof AuthError ? cause.message : 'No se pudo conectar con el servicio. Inténtalo de nuevo.'; }
