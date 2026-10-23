import { Turnstile, type TurnstileInstance } from '@marsidev/react-turnstile';
import { useImperativeHandle, useRef, type Ref } from 'react';

export interface CaptchaHandle {
  /** Pide un captcha nuevo; el token anterior ya no sirve (se acepta una sola vez). */
  reiniciar: () => void;
}

export interface CaptchaProps {
  claveSitio: string;
  alCambiarToken: (token: string | null) => void;
  ref?: Ref<CaptchaHandle>;
}

/**
 * Cloudflare Turnstile (docs/11#captcha). El script viene de challenges.cloudflare.com, que la CSP permite;
 * el servidor verifica el token. Las pruebas reemplazan este módulo por uno que entrega un token fijo.
 */
export function Captcha({ claveSitio, alCambiarToken, ref }: CaptchaProps) {
  const turnstile = useRef<TurnstileInstance>(null);
  useImperativeHandle(ref, () => ({
    reiniciar: () => {
      turnstile.current?.reset();
      alCambiarToken(null);
    },
  }));

  return (
    <Turnstile
      ref={turnstile}
      siteKey={claveSitio}
      options={{ language: 'es', theme: 'light', size: 'flexible' }}
      onSuccess={(token) => alCambiarToken(token)}
      onExpire={() => alCambiarToken(null)}
      onError={() => alCambiarToken(null)}
    />
  );
}
