import { useState } from 'react';
import { Icon } from './Icon';

export function AccessPreview() {
  const [step, setStep] = useState<'phone' | 'code'>('phone');
  const [phone, setPhone] = useState('');
  const [code, setCode] = useState('');
  const [touched, setTouched] = useState(false);
  const value = step === 'phone' ? phone : code;
  const invalid = touched && !(step === 'phone' ? /^\d{8}$/ : /^\d{6}$/).test(value);

  function changeStep(next: 'phone' | 'code') {
    setStep(next);
    setTouched(false);
    setPhone('');
    setCode('');
  }

  return (
    <section className="access-layout" aria-labelledby="access-title">
      <div className="access-context">
        <span className="section-kicker">ACCESO PERSONAL</span>
        <h1 id="access-title">Acceso personal</h1>
        <p>Tu teléfono será tu identificador de acceso.</p>
        <div className="access-principle"><Icon name="lock" /><div><strong>Un espacio privado</strong><p>La autenticación será necesaria para consultar tus datos.</p></div></div>
        <div className="preview-note"><span className="note-dot" /><div><strong>Vista de diseño</strong><p>No envía SMS ni inicia sesión.</p></div></div>
      </div>
      <div className="access-form-panel">
        <div className="preview-switch" aria-label="Vistas del prototipo de acceso">
          <button type="button" aria-pressed={step === 'phone'} onClick={() => changeStep('phone')}>Teléfono</button>
          <button type="button" aria-pressed={step === 'code'} onClick={() => changeStep('code')}>Código SMS</button>
        </div>
        <form onSubmit={event => event.preventDefault()} noValidate>
          <span className="form-symbol"><Icon name={step === 'phone' ? 'phone' : 'lock'} /></span>
          <h2>{step === 'phone' ? 'Entrar a AlineaApp' : 'Verificar tu teléfono'}</h2>
          <p className="form-intro">{step === 'phone' ? 'Tu número será tu identificador de acceso.' : 'Así se verá la entrada del código cuando el acceso por SMS esté habilitado.'}</p>
          <label htmlFor={step}>{step === 'phone' ? 'Número de teléfono' : 'Código de seis dígitos'}</label>
          {step === 'phone' ? (
            <div className={`phone-field ${invalid ? 'invalid' : ''}`}>
              <span className="dial-code" aria-hidden="true">+507</span>
              <input id="phone" type="tel" inputMode="tel" autoComplete="off" placeholder="Tu número" value={phone} maxLength={16} onChange={event => setPhone(event.target.value)} onBlur={() => setTouched(true)} aria-invalid={invalid} aria-describedby={invalid ? 'phone-hint field-error' : 'phone-hint'} />
            </div>
          ) : (
            <input className="code-field" id="code" type="text" inputMode="numeric" autoComplete="off" placeholder="— — — — — —" value={code} maxLength={6} onChange={event => setCode(event.target.value.replace(/\D/g, ''))} onBlur={() => setTouched(true)} aria-invalid={invalid} aria-describedby={invalid ? 'code-hint field-error' : 'code-hint'} />
          )}
          <p className="field-hint" id={step === 'phone' ? 'phone-hint' : 'code-hint'}>{step === 'phone' ? 'Panamá · +507 como prefijo sugerido.' : 'Vista de ejemplo. No se ha enviado ningún código.'}</p>
          {invalid && <p className="field-error" id="field-error" role="alert">{step === 'phone' ? 'Revisa el número: utiliza 8 dígitos.' : 'El código debe tener 6 dígitos.'}</p>}
          <button className="primary-button" type="submit" disabled>{step === 'phone' ? 'Enviar código' : 'Verificar código'}<Icon name="arrow" /></button>
          <p className="disabled-explanation">El acceso por SMS aún no está habilitado.</p>
        </form>
        <div className="form-footer"><Icon name="lock" /><span>Sin sesión · Sin datos guardados</span></div>
      </div>
    </section>
  );
}
