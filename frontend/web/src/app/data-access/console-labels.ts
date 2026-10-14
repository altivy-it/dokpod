import type {
  ContainerDisplayStatus,
  HostSituation,
  OperationalEvent,
} from './generated/control-plane';

export interface LabelTone {
  label: string;
  tone: 'success' | 'warning' | 'error' | 'info' | 'neutral' | 'accent';
}

/** Situação do host (FR-004 da spec 008) → rótulo e tom do mockup. */
export const HOST_SITUATION: Record<HostSituation, LabelTone> = {
  up_to_date: { label: 'Em dia', tone: 'success' },
  update_required: { label: 'Atualizar', tone: 'warning' },
  stale: { label: 'Desatualizado', tone: 'warning' },
  disconnected: { label: 'Desconectado', tone: 'error' },
  disabled: { label: 'Desabilitado', tone: 'neutral' },
  unknown: { label: 'Desconhecido', tone: 'neutral' },
};

/** Situação exibida do container (FR-006 da spec 008). */
export const CONTAINER_STATUS: Record<ContainerDisplayStatus, LabelTone> = {
  running: { label: 'Em execução', tone: 'success' },
  starting: { label: 'Iniciando', tone: 'info' },
  failed: { label: 'Com falha', tone: 'error' },
  restarting: { label: 'Reiniciando', tone: 'warning' },
  paused: { label: 'Pausado', tone: 'info' },
  stopped: { label: 'Parado', tone: 'neutral' },
};

export const CONTAINER_STATUS_TONES: Record<string, string> = Object.fromEntries(
  Object.values(CONTAINER_STATUS).map(s => [s.label, s.tone]),
);

export const ENGINE_LABEL: Record<string, string> = { docker: 'Docker', podman: 'Podman' };

const ACTION_LABEL: Record<string, string> = {
  StartContainer: 'iniciar',
  StopContainer: 'parar',
  RestartContainer: 'reiniciar',
  DeleteContainer: 'remover',
  PauseContainer: 'pausar',
  UnpauseContainer: 'retomar',
  PullImage: 'baixar imagem',
  CreateContainer: 'criar container',
};

export interface EventView {
  text: string;
  where: string;
  label: string;
  tone: LabelTone['tone'];
}

/** Texto pt-BR de um evento operacional no padrão do card "Eventos recentes". */
export function describeEvent(event: OperationalEvent): EventView {
  const p = event.parameters ?? {};
  const subject = event.subjectName || p['name'] || shortId(event.subjectId);
  const host = event.environmentName ?? '';
  const where = [subject, host].filter(Boolean).join(' · ');
  const count = event.aggregatedCount > 1 ? ` (${event.aggregatedCount}×)` : '';
  switch (event.type) {
    case 'container.died':
      return { text: `Container encerrado com erro${p['exitCode'] ? ` (código ${p['exitCode']})` : ''}${count}`, where, label: 'Com falha', tone: 'error' };
    case 'container.oom_killed':
      return { text: `Limite de memória atingido (OOMKilled)${count}`, where, label: 'Com falha', tone: 'error' };
    case 'container.unhealthy':
      return { text: 'Health check falhou; container não saudável', where, label: 'Atenção', tone: 'warning' };
    case 'container.recovered':
      return { text: 'Health check voltou a responder', where, label: 'Recuperado', tone: 'success' };
    case 'container.auto_restarted':
      return { text: `Reinício automático pela política do container${count}`, where, label: 'Reiniciando', tone: 'warning' };
    case 'container.created':
      return { text: 'Container criado', where: [subject, p['image'], host].filter(Boolean).join(' · '), label: 'Criado', tone: 'info' };
    case 'container.removed':
      return { text: 'Container removido', where, label: 'Removido', tone: 'neutral' };
    case 'image.pulled':
      return { text: 'Imagem baixada', where: [subject || p['image'], host].filter(Boolean).join(' · '), label: 'Concluído', tone: 'success' };
    case 'image.removed':
      return { text: 'Imagem removida', where, label: 'Neutro', tone: 'neutral' };
    case 'volume.removed':
      return { text: 'Volume removido', where, label: 'Neutro', tone: 'neutral' };
    case 'engine.version_outdated':
      return {
        text: 'Host com engine fora da versão homologada',
        where: [host, p['engine'] ? `${ENGINE_LABEL[p['engine']] ?? p['engine']} ${p['version'] ?? ''}`.trim() : ''].filter(Boolean).join(' · '),
        label: 'Atualizar',
        tone: 'warning',
      };
    case 'agent.connected':
      return { text: 'Agente conectado', where: host, label: 'Conectado', tone: 'info' };
    case 'agent.disconnected':
      return { text: 'Agente desconectado', where: host, label: 'Desconectado', tone: 'warning' };
    case 'command.succeeded':
      return { text: `Comando ${ACTION_LABEL[p['action'] ?? ''] ?? ''} concluído`.replace('  ', ' '), where, label: 'Concluído', tone: 'success' };
    case 'command.failed':
      return { text: `Comando ${ACTION_LABEL[p['action'] ?? ''] ?? ''} falhou${p['failureCode'] ? ` (${p['failureCode']})` : ''}`.replace('  ', ' '), where, label: 'Falhou', tone: 'error' };
    case 'command.indeterminate':
      return { text: `Resultado do comando ${ACTION_LABEL[p['action'] ?? ''] ?? ''} indeterminado`.replace('  ', ' '), where, label: 'Indeterminado', tone: 'warning' };
    case 'container.exec_orphan_suspected':
      return { text: 'Shell de terminal possivelmente ainda em execução após reinício do agente', where, label: 'Atenção', tone: 'warning' };
    case 'events.dropped':
      return { text: `${p['droppedCount'] ?? 'Vários'} eventos descartados por excesso de volume`, where: host, label: 'Descartados', tone: 'warning' };
    default:
      return { text: event.type, where, label: 'Evento', tone: 'neutral' };
  }
}

function shortId(id: string | null | undefined): string {
  return id ? id.slice(0, 12) : '';
}
