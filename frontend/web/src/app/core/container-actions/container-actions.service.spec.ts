import { TestBed } from '@angular/core/testing';
import { ControlPlaneApiService } from '../../data-access/control-plane-api.service';
import type { ContainerListItem } from '../../data-access/generated/control-plane';
import { BATCH_LIMIT, ContainerActionsService, evaluateEligibility } from './container-actions.service';

function item(name: string, state: string, allowed: ContainerListItem['allowedActions'], connected = true): ContainerListItem {
  return {
    containerId: `${name}-id`,
    name,
    imageReference: 'img',
    state,
    revision: `${name}:1`,
    observedAt: '2026-10-10T18:00:00Z',
    environmentId: 'env-1',
    environmentName: 'srv-prod-01',
    hostConnected: connected,
    allowedActions: allowed,
  };
}

describe('ContainerActionsService', () => {
  const api = {
    submitContainerAction: vi.fn(),
    getCommand: vi.fn(),
  };

  beforeEach(() => {
    sessionStorage.clear();
    api.submitContainerAction.mockReset();
    api.getCommand.mockReset();
    TestBed.configureTestingModule({ providers: [{ provide: ControlPlaneApiService, useValue: api }] });
  });

  it('separates eligible and skipped containers with reasons', () => {
    const result = evaluateEligibility(
      [item('api', 'Running', ['stop', 'pause']), item('job', 'Exited', ['start']), item('edge', 'Running', ['pause'], false)],
      'pause',
    );
    expect(result.eligible.map(i => i.name)).toEqual(['api']);
    expect(result.skipped.map(s => [s.item.name, s.reason])).toEqual([
      ['job', 'não está em execução'],
      ['edge', 'host desconectado'],
    ]);
  });

  it('submits one keyed command per container and tracks them to completion', async () => {
    api.submitContainerAction.mockImplementation(async (_env: string, containerId: string) => `cmd-${containerId}`);
    api.getCommand.mockImplementation(async () => ({ state: 'succeeded', failureCode: null }));
    const service = TestBed.inject(ContainerActionsService);

    await service.run('restart', [item('a', 'Running', ['restart']), item('b', 'Running', ['restart'])]);

    const keys = api.submitContainerAction.mock.calls.map(call => call[4]);
    expect(new Set(keys).size).toBe(2);
    const stored = JSON.parse(sessionStorage.getItem('dokpod.batch.v1')!);
    expect(stored.items.map((i: { commandId: string }) => i.commandId)).toEqual(['cmd-a-id', 'cmd-b-id']);
    await vi.waitFor(() => expect(service.summary().succeeded).toBe(2));
    expect(service.running()).toBe(false);
  });

  it('sends the same justification with every command of the batch (spec 016)', async () => {
    api.submitContainerAction.mockImplementation(async (_env: string, containerId: string) => `cmd-${containerId}`);
    api.getCommand.mockImplementation(async () => ({ state: 'succeeded', failureCode: null }));
    const service = TestBed.inject(ContainerActionsService);

    await service.run('stop', [item('a', 'Running', ['stop']), item('b', 'Running', ['stop'])], 'Janela de manutenção do banco');

    expect(api.submitContainerAction.mock.calls.map(call => call[5])).toEqual(['Janela de manutenção do banco', 'Janela de manutenção do banco']);
    expect(JSON.parse(sessionStorage.getItem('dokpod.batch.v1')!).justification).toBe('Janela de manutenção do banco');
  });

  it('restores an interrupted batch without resubmitting automatically', () => {
    sessionStorage.setItem(
      'dokpod.batch.v1',
      JSON.stringify({
        id: 'b1',
        action: 'stop',
        startedAt: '2026-10-10T18:00:00Z',
        items: [{ environmentId: 'env-1', environmentName: 'srv', containerId: 'x', name: 'x', revision: 'r', idempotencyKey: 'k1', commandId: null, state: 'submitting', failureCode: null }],
      }),
    );
    const service = TestBed.inject(ContainerActionsService);
    expect(service.batch()!.items[0].state).toBe('queued');
    expect(service.running()).toBe(true);
    expect(api.submitContainerAction).not.toHaveBeenCalled();
  });

  it('rejects batches above the limit', async () => {
    const service = TestBed.inject(ContainerActionsService);
    const many = Array.from({ length: BATCH_LIMIT + 1 }, (_, i) => item(`c${i}`, 'Running', ['stop']));
    await expect(service.run('stop', many)).rejects.toThrow(`no máximo ${BATCH_LIMIT}`);
  });
});
