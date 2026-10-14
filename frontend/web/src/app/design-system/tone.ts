import type { IconName } from './icon/icon-paths';

export type Tone = 'success' | 'warning' | 'error' | 'info' | 'accent' | 'neutral';

export interface ToneSpec {
  color: string;
  background: string;
  icon: IconName;
}

/** Tom semântico → tokens. Todo status leva palavra ou ícone além da cor. */
export const TONES: Record<Tone, ToneSpec> = {
  success: { color: 'var(--signal-success)', background: 'var(--success-tint)', icon: 'check-circle' },
  warning: { color: 'var(--signal-warning)', background: 'var(--warning-tint)', icon: 'alert' },
  error: { color: 'var(--signal-error)', background: 'var(--error-tint-strong)', icon: 'x-circle' },
  info: { color: 'var(--signal-info)', background: 'var(--info-tint)', icon: 'info' },
  accent: { color: 'var(--teal-light)', background: 'var(--accent-tint)', icon: 'zap' },
  neutral: { color: 'var(--muted)', background: 'var(--hover-wash)', icon: 'circle' },
};

const ALIASES: Record<string, Tone> = {
  primary: 'accent',
  secondary: 'neutral',
  default: 'neutral',
  disabled: 'neutral',
};

export function toTone(value: string | null | undefined): Tone {
  if (!value) return 'neutral';
  if (value in TONES) return value as Tone;
  return ALIASES[value] ?? 'neutral';
}

export function tone(value: string | null | undefined): ToneSpec {
  return TONES[toTone(value)];
}
