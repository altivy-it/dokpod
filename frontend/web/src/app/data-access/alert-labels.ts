import type { AlertRule, AlertScope, AlertState, IntentionalAction } from './generated/control-plane';
import type { LabelTone } from './console-labels';

// Severidades ficam num módulo próprio: o sino (bundle inicial) usa só elas.
export { ALERT_SEVERITIES, ALERT_SEVERITY } from './alert-severity';

export const ALERT_STATE: Record<AlertState, LabelTone> = {
  open: { label: 'Aberto', tone: 'error' },
  acknowledged: { label: 'Reconhecido', tone: 'warning' },
  silenced: { label: 'Silenciado', tone: 'neutral' },
  resolved: { label: 'Resolvido', tone: 'success' },
};

export const ALERT_RESOLUTION: Record<string, string> = {
  condition_cleared: 'condição normalizada',
  manual: 'resolvido manualmente',
  container_removed: 'container removido',
  rule_removed: 'regra removida',
};

export const TIMELINE_LABEL: Record<string, string> = {
  opened: 'Aberto',
  occurred: 'Nova ocorrência',
  acknowledged: 'Reconhecido',
  silenced: 'Silenciado',
  unsilenced: 'Fim do silenciamento',
  resolved: 'Resolvido',
};

/** Tipos de evento que uma regra pode observar (spec 008), com rótulo legível. */
export const RULE_EVENT_TYPES: { id: string; label: string }[] = [
  { id: 'container.died', label: 'Container encerrado com erro' },
  { id: 'container.oom_killed', label: 'Container sem memória (OOM)' },
  { id: 'container.unhealthy', label: 'Container sem saúde' },
  { id: 'container.auto_restarted', label: 'Container reiniciado automaticamente' },
  { id: 'agent.disconnected', label: 'Agente desconectado' },
  { id: 'engine.version_outdated', label: 'Engine desatualizada' },
  { id: 'command.failed', label: 'Comando falhou' },
  { id: 'command.indeterminate', label: 'Comando indeterminado' },
];

export const RULE_METRICS: { id: NonNullable<AlertRule['metric']>; label: string }[] = [
  { id: 'host_cpu', label: 'CPU do host' },
  { id: 'host_memory', label: 'Memória do host' },
  { id: 'container_cpu', label: 'CPU do container' },
  { id: 'container_memory', label: 'Memória do container' },
];

const STAGE_LABEL: Record<string, string> = { production: 'Produção', staging: 'Homologação', development: 'Desenvolvimento' };

/** Condição da regra em linguagem do operador. */
export function describeRule(rule: AlertRule): string {
  if (rule.conditionKind === 'metric') {
    const metric = RULE_METRICS.find(m => m.id === rule.metric)?.label ?? rule.metric ?? 'métrica';
    const minutes = Math.round(rule.durationSeconds / 60);
    return `${metric} acima de ${rule.thresholdPercent ?? '?'}%${minutes ? ` por ${minutes} min` : ''}`;
  }
  const event = RULE_EVENT_TYPES.find(e => e.id === rule.eventType)?.label ?? rule.eventType ?? 'evento';
  if (rule.minOccurrences > 1) return `${event}: ${rule.minOccurrences} vezes em ${Math.round(rule.durationSeconds / 60)} min`;
  if (rule.eventType === 'agent.disconnected' && rule.durationSeconds > 0) return `${event} por mais de ${Math.round(rule.durationSeconds / 60)} min`;
  return event;
}

/** Abrangência legível; o nome do host vem do diretório quando conhecido. */
export function describeScope(scope: AlertScope | null | undefined, hostName: (id: string) => string | undefined): string {
  const parts: string[] = [];
  if (scope?.stage) parts.push(STAGE_LABEL[scope.stage] ?? scope.stage);
  if (scope?.environmentId) parts.push(hostName(scope.environmentId) ?? 'host restrito');
  if (scope?.containerPattern) parts.push(`containers "${scope.containerPattern}"`);
  return parts.length ? parts.join(' · ') : 'Todos os hosts';
}

/** Rótulo "Parado intencionalmente" / "Pausado intencionalmente" (spec 016 FR-018). */
export function intentionalLabel(intentional: IntentionalAction): string {
  return intentional.action === 'pause' ? 'Pausado intencionalmente' : 'Parado intencionalmente';
}
