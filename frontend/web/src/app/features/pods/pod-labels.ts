import type { PodAction, PodStatus } from '../../data-access/generated/control-plane';

export const POD_STATUS: Record<PodStatus, { label: string; tone: string }> = {
  running: { label: 'Em execução', tone: 'success' },
  degraded: { label: 'Degradado', tone: 'warning' },
  exited: { label: 'Parado', tone: 'neutral' },
  paused: { label: 'Pausado', tone: 'info' },
  created: { label: 'Criado', tone: 'neutral' },
  error: { label: 'Com erro', tone: 'error' },
};

export const POD_ACTION: Record<PodAction, { verb: string; icon: string }> = {
  start: { verb: 'Iniciar', icon: 'play' },
  stop: { verb: 'Parar', icon: 'stop' },
  restart: { verb: 'Reiniciar', icon: 'refresh' },
  pause: { verb: 'Pausar', icon: 'pause' },
  unpause: { verb: 'Retomar', icon: 'play' },
};
