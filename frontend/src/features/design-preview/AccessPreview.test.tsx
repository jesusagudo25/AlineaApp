import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { AccessPreview } from './AccessPreview';

describe('prototipo de acceso', () => {
  it('identifica el prototipo y explica por qué no se pueden enviar mensajes', () => {
    render(<AccessPreview />);
    expect(screen.getByText('No envía SMS ni inicia sesión.')).toBeInTheDocument();
    expect(screen.getByText('+507')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Enviar código' })).toBeDisabled();
  });
  it('asocia el error del teléfono a su campo sin llamar a la red', () => {
    const fetch = vi.fn(); vi.stubGlobal('fetch', fetch);
    render(<AccessPreview />);
    const input = screen.getByLabelText('Número de teléfono');
    fireEvent.change(input, { target: { value: '12' } });
    fireEvent.blur(input);
    expect(input).toHaveAttribute('aria-invalid', 'true');
    expect(input).toHaveAccessibleDescription(/Revisa el número/);
    expect(screen.getByRole('button', { name: 'Enviar código' })).toBeDisabled();
    expect(fetch).not.toHaveBeenCalled();
  });
  it('permite inspeccionar OTP sin simular envío o sesión y descarta las entradas al cambiar de vista', () => {
    const fetch = vi.fn(); vi.stubGlobal('fetch', fetch);
    render(<AccessPreview />);
    fireEvent.change(screen.getByLabelText('Número de teléfono'), { target: { value: '12' } });
    fireEvent.click(screen.getByRole('button', { name: 'Código SMS' }));
    const code = screen.getByLabelText('Código de seis dígitos');
    fireEvent.change(code, { target: { value: '12' } });
    fireEvent.blur(code);
    expect(code).toHaveAccessibleDescription(/El código debe tener 6 dígitos/);
    expect(screen.getByRole('button', { name: 'Verificar código' })).toBeDisabled();
    fireEvent.click(screen.getByRole('button', { name: 'Teléfono' }));
    expect(screen.getByLabelText('Número de teléfono')).toHaveValue('');
    expect(fetch).not.toHaveBeenCalled();
  });
});
