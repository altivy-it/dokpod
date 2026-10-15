import type { AlertSeverity } from './generated/control-plane';
import type { LabelTone } from './console-labels';

/** Severidade do alerta (spec 016) → rótulo e tom, da mais grave à mais leve. */
export const ALERT_SEVERITY: Record<AlertSeverity, LabelTone> = {
  critical: { label: 'Crítica', tone: 'error' },
  high: { label: 'Alta', tone: 'error' },
  medium: { label: 'Média', tone: 'warning' },
  low: { label: 'Baixa', tone: 'info' },
};

export const ALERT_SEVERITIES: AlertSeverity[] = ['critical', 'high', 'medium', 'low'];
