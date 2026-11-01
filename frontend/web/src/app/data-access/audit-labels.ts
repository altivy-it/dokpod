import type { AuditCategory, AuditEventView, AuditOutcome } from './generated/control-plane';
import type { LabelTone } from './console-labels';

/** Ações da trilha (spec 017) em linguagem do operador; ação desconhecida aparece como foi gravada. */
export const AUDIT_ACTION: Record<string, string> = {
  'environment.register': 'Host cadastrado',
  'environment.approve': 'Host aprovado',
  'environment.suspend': 'Host suspenso',
  'environment.revoke': 'Identidade do agente revogada',
  'environment.stage_changed': 'Estágio do host alterado',
  'container.start': 'Iniciar container',
  'container.stop': 'Parar container',
  'container.restart': 'Reiniciar container',
  'container.delete': 'Remover container',
  'container.pause': 'Pausar container',
  'container.unpause': 'Retomar container',
  'container.create.requested': 'Criar container',
  'container.command.result': 'Resultado do comando',
  'image.pull': 'Baixar imagem',
  'container.inspected': 'Inspeção do container',
  'container.logs.read': 'Leitura de logs',
  'container.logs.stream_started': 'Logs ao vivo iniciados',
  'container.logs.stream_ended': 'Logs ao vivo encerrados',
  'container.exec.requested': 'Terminal solicitado',
  'container.exec.started': 'Terminal aberto',
  'container.exec.ended': 'Terminal encerrado',
  'container.exec.denied': 'Terminal negado',
  'alert.rule_created': 'Regra de alerta criada',
  'alert.rule_updated': 'Regra de alerta alterada',
  'alert.rule_deleted': 'Regra de alerta excluída',
  'alert.acknowledged': 'Alerta reconhecido',
  'alert.resolved': 'Alerta resolvido',
  'alert.silenced': 'Alerta silenciado',
  'alert.silence_created': 'Silenciamento criado',
  'alert.silence_ended': 'Silenciamento encerrado',
  'container.justification_policy_changed': 'Política de justificativa alterada',
  'audit.exported': 'Auditoria exportada',
  'volume.remove': 'Remover volume',
  'network.remove': 'Remover rede',
  'registry.policy_changed': 'Política de registros alterada',
  'pod.start': 'Iniciar pod',
  'pod.stop': 'Parar pod',
  'pod.restart': 'Reiniciar pod',
  'pod.pause': 'Pausar pod',
  'pod.unpause': 'Retomar pod',
};

export const AUDIT_CATEGORIES: { id: AuditCategory; label: string }[] = [
  { id: 'environments', label: 'Ambientes' },
  { id: 'containers', label: 'Containers' },
  { id: 'commands', label: 'Comandos' },
  { id: 'images', label: 'Imagens' },
  { id: 'volumes', label: 'Volumes' },
  { id: 'networks', label: 'Redes' },
  { id: 'pods', label: 'Pods' },
  { id: 'inspection', label: 'Logs e inspeção' },
  { id: 'terminal', label: 'Terminal' },
  { id: 'governance', label: 'Alertas e política' },
  { id: 'audit', label: 'Auditoria' },
  { id: 'audit_export', label: 'Exportação para SIEM' },
];

export const AUDIT_OUTCOME: Record<AuditOutcome, LabelTone> = {
  succeeded: { label: 'Sucesso', tone: 'success' },
  denied: { label: 'Negado', tone: 'warning' },
  failed: { label: 'Falha', tone: 'error' },
  indeterminate: { label: 'Indeterminado', tone: 'neutral' },
};

export const AUDIT_ACTOR_KIND: Record<AuditEventView['actorKind'], string> = { user: 'Usuário', agent: 'Agente', system: 'Sistema' };

/** Rótulos dos detalhes conhecidos; chaves novas aparecem como foram gravadas. */
export const AUDIT_DETAIL: Record<string, string> = {
  justification: 'Justificativa',
  reason: 'Motivo',
  shell: 'Shell',
  root: 'Usuário root',
  bytesIn: 'Bytes enviados',
  bytesOut: 'Bytes recebidos',
  durationSeconds: 'Duração (s)',
  sessionId: 'Sessão',
  stage: 'Estágio',
  previousStage: 'Estágio anterior',
  image: 'Imagem',
  name: 'Nome',
  networkId: 'Identificador da rede',
  registries: 'Registros permitidos',
  previousRegistries: 'Registros permitidos antes',
  podId: 'Identificador do pod',
  command: 'Comando equivalente',
  tail: 'Linhas pedidas',
  lines: 'Linhas lidas',
  filtered: 'Com filtro',
  ruleId: 'Regra',
  alertId: 'Alerta',
  silenceId: 'Silenciamento',
  severity: 'Severidade',
  enabled: 'Ativa',
  required: 'Obrigatória',
  version: 'Versão',
  thresholdPercent: 'Limite (%)',
  since: 'Desde',
  until: 'Até',
  count: 'Quantidade',
  action: 'Ação',
};

export function auditActionLabel(action: string): string {
  return AUDIT_ACTION[action] ?? action;
}

export function auditActorLabel(event: Pick<AuditEventView, 'actorKind' | 'actorName' | 'actorId'>): string {
  if (event.actorKind !== 'user') return AUDIT_ACTOR_KIND[event.actorKind];
  return event.actorName ?? event.actorId;
}
