import { describeEvent } from './console-labels';
import type { OperationalEvent } from './generated/control-plane';

function event(type: string, parameters: Record<string, string> = {}, subjectName: string | null = 'worker-relatorios'): OperationalEvent {
  return {
    eventId: '00000000-0000-4000-8000-000000000001',
    environmentId: '11111111-1111-4111-8111-000000000001',
    environmentName: 'srv-batch-01',
    type,
    severity: 'error',
    occurredAt: '2026-10-10T17:56:00Z',
    receivedAt: '2026-10-10T17:56:01Z',
    clockSkewDetected: false,
    subjectKind: 'container',
    subjectId: 'abc',
    subjectName,
    parameters,
    aggregatedCount: 1,
  };
}

describe('describeEvent', () => {
  it('uses the mockup wording for OOM and version events', () => {
    expect(describeEvent(event('container.oom_killed'))).toEqual({
      text: 'Limite de memória atingido (OOMKilled)',
      where: 'worker-relatorios · srv-batch-01',
      label: 'Com falha',
      tone: 'error',
    });
    const outdated = describeEvent(event('engine.version_outdated', { engine: 'docker', version: '26.1.4' }, null));
    expect(outdated.text).toBe('Host com engine fora da versão homologada');
    expect(outdated.where).toBe('srv-batch-01 · Docker 26.1.4');
    expect(outdated.label).toBe('Atualizar');
  });

  it('describes dropped events and command results', () => {
    expect(describeEvent(event('events.dropped', { droppedCount: '42' })).text).toBe('42 eventos descartados por excesso de volume');
    expect(describeEvent(event('command.failed', { action: 'RestartContainer', failureCode: 'target_not_found' })).text).toBe(
      'Comando reiniciar falhou (target_not_found)',
    );
  });
});
