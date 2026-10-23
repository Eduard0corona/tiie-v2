import { useImperativeHandle } from 'react';
import type { CaptchaProps } from '@/components/Captcha';

/** Reinicios pedidos por la pantalla; las pruebas lo revisan después de un CaptchaInvalido. */
export const captchaSimulado = { reinicios: 0 };

/**
 * Reemplazo de Turnstile en las pruebas: `vi.mock('@/components/Captcha', () => import('@/test/CaptchaSimulado'))`.
 * Un botón entrega un token fijo.
 */
export function Captcha({ alCambiarToken, ref }: CaptchaProps) {
  useImperativeHandle(ref, () => ({
    reiniciar: () => {
      captchaSimulado.reinicios++;
      alCambiarToken(null);
    },
  }));
  return (
    <button type="button" onClick={() => alCambiarToken('token-prueba')}>
      Simular captcha
    </button>
  );
}
