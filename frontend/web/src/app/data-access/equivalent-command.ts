import type { ContainerCreationRequest } from './generated/control-plane';
import { parseImageReference } from './image-reference';

/** Aspas simples POSIX somente quando necessário (mesma regra do `EquivalentCommandRenderer` do servidor). */
export function shellQuote(value: string): string {
  if (value.length > 0 && /^[A-Za-z0-9\-_./:=,@%+]+$/.test(value)) return value;
  return `'${value.replace(/'/g, "'\\''")}'`;
}

/**
 * Comando equivalente (spec 013 D-005): valores literais viram `***` e segredos aparecem só por referência.
 * Paridade com o servidor garantida por `src/testing/equivalent-command.fixtures.json`.
 */
export function renderEquivalentCommand(request: ContainerCreationRequest, engine: 'docker' | 'podman'): string {
  const parts: string[] = [engine === 'podman' ? 'podman' : 'docker', 'run', '-d', '--name', shellQuote(request.name)];
  for (const port of request.ports ?? []) {
    const host = port.hostIp ? `${port.hostIp.includes(':') ? `[${port.hostIp}]` : port.hostIp}:` : '';
    const mapping = port.hostPort === 0 ? `${port.containerPort}` : `${host}${port.hostPort}:${port.containerPort}`;
    parts.push('-p', mapping + (port.protocol === 'udp' ? '/udp' : ''));
  }
  for (const variable of request.environment ?? []) {
    if (variable.secret?.kind === 'podman_secret') parts.push('--secret', shellQuote(`${variable.secret.name},type=env,target=${variable.name}`));
    else if (variable.secret) parts.push('-e', shellQuote(`${variable.name}=<segredo:${variable.secret.name}>`));
    else parts.push('-e', `${variable.name}=***`);
  }
  for (const volume of request.volumes ?? []) parts.push('-v', shellQuote(`${volume.source}:${volume.target}${volume.readOnly ? ':ro' : ''}`));
  if (request.network) parts.push('--network', shellQuote(request.network));
  if (request.cpuLimitCores != null) parts.push('--cpus', `${Number(request.cpuLimitCores.toFixed(2))}`);
  if (request.memoryLimitBytes != null) {
    const mib = 1024 * 1024;
    parts.push('--memory', request.memoryLimitBytes % mib === 0 ? `${request.memoryLimitBytes / mib}m` : `${request.memoryLimitBytes}b`);
  }
  const restart = request.restartPolicy?.name ?? 'no';
  if (restart !== 'no') {
    const max = request.restartPolicy?.maximumRetryCount ?? 0;
    parts.push('--restart', restart === 'on-failure' && max > 0 ? `on-failure:${max}` : restart);
  }
  const health = request.healthcheck;
  if (health) {
    parts.push('--health-cmd', shellQuote(health.test[0] === 'CMD-SHELL' ? health.test[1] : health.test.slice(1).join(' ')));
    parts.push(`--health-interval=${health.intervalSeconds}s`, `--health-timeout=${health.timeoutSeconds}s`, `--health-retries=${health.retries}`);
    if (health.startPeriodSeconds > 0) parts.push(`--health-start-period=${health.startPeriodSeconds}s`);
    if (health.onFailure === 'restart') parts.push('--health-on-failure=restart');
  }
  if (request.autoRemove) parts.push('--rm');
  parts.push(parseImageReference(request.image)?.canonical ?? request.image);
  return parts.join(' ');
}
