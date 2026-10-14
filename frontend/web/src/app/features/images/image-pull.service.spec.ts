import { TestBed } from '@angular/core/testing';
import { ControlPlaneApiService } from '../../data-access/control-plane-api.service';
import type { EnvironmentState } from '../../data-access/generated/control-plane';
import { ImagePullService } from './image-pull.service';
import { pullIneligibility } from './pull-image.drawer';

function env(overrides: Partial<EnvironmentState> = {}): EnvironmentState {
  return {
    environmentId: 'env-1',
    name: 'srv-1',
    host: 'srv-1',
    enabled: true,
    scopes: ['environment:read', 'image:pull'],
    agent: { connected: true, capabilities: ['Inventory', 'ImagePull'] },
    ...overrides,
  };
}

describe('ImagePullService', () => {
  const api = { pullImage: vi.fn(), getCommand: vi.fn(), lookupImage: vi.fn() };

  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    api.pullImage.mockReset();
    api.getCommand.mockReset();
    TestBed.configureTestingModule({ providers: [{ provide: ControlPlaneApiService, useValue: api }] });
  });

  afterEach(() => vi.useRealTimers());

  it('envia uma chave por host e acompanha até o estado terminal com motivo legível', async () => {
    api.pullImage.mockImplementation(async (environmentId: string) => `cmd-${environmentId}`);
    api.getCommand.mockImplementation(async (environmentId: string) =>
      environmentId === 'a'
        ? { state: 'succeeded', result: { resolvedImageDigest: 'sha256:' + 'c'.repeat(64), imageSizeBytes: 1024 } }
        : { state: 'failed', failureCode: 'registry_unauthorized' },
    );
    const service = TestBed.inject(ImagePullService);

    await service.start('docker.io/library/nginx:1.27', [
      { environmentId: 'a', name: 'srv-a' },
      { environmentId: 'b', name: 'srv-b' },
    ]);

    const keys = api.pullImage.mock.calls.map(call => call[2]);
    expect(new Set(keys).size).toBe(2);
    await vi.advanceTimersByTimeAsync(1600);
    await vi.waitFor(() => expect(service.running()).toBe(false));
    expect(service.summary()).toEqual({ succeeded: 1, failed: 1, indeterminate: 0, pending: 0 });
    expect(service.run()!.targets[1].failure).toContain('credenciais');
    expect(service.run()!.targets[0].sizeBytes).toBe(1024);
    expect(service.elapsedMs()).not.toBeNull();
  });

  it('mantém na fila o host com falha de rede e reenvia com a mesma chave', async () => {
    api.pullImage.mockRejectedValueOnce(new TypeError('network')).mockResolvedValue('cmd-1');
    api.getCommand.mockResolvedValue({ state: 'accepted' });
    const service = TestBed.inject(ImagePullService);

    await service.start('docker.io/library/nginx:latest', [{ environmentId: 'a', name: 'srv-a' }]);
    expect(service.run()!.targets[0].state).toBe('queued');
    await service.retryUnsubmitted();

    expect(api.pullImage.mock.calls[0][2]).toBe(api.pullImage.mock.calls[1][2]);
    expect(service.run()!.targets[0].state).toBe('pending');
  });
});

describe('pullIneligibility', () => {
  it('cruza permissão, conexão e capability do agente', () => {
    expect(pullIneligibility(env())).toBeNull();
    expect(pullIneligibility(env({ scopes: ['environment:read'] }))).toBe('sem permissão image:pull');
    expect(pullIneligibility(env({ agent: { connected: false, capabilities: ['ImagePull'] } }))).toBe('agente desconectado');
    expect(pullIneligibility(env({ agent: { connected: true, capabilities: ['Inventory'] } }))).toBe('agente sem suporte a download');
  });
});
