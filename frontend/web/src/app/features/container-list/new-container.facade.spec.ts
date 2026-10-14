import { TestBed } from '@angular/core/testing';
import { ControlPlaneApiService } from '../../data-access/control-plane-api.service';
import type { ContainerCreationRequest, EnvironmentState } from '../../data-access/generated/control-plane';
import { healthPreset } from './new-container.drawer';
import { createFailureLabel, createIneligibility, NewContainerFacade } from './new-container.facade';

const CANARY = 'canario-013-ui';
const request: ContainerCreationRequest = {
  image: 'docker.io/library/nginx:1.27',
  name: 'web-01',
  ports: [{ hostPort: 8081, containerPort: 80, protocol: 'tcp' }],
  environment: [{ name: 'SENHA', value: CANARY, sensitive: true }],
  autoRemove: false,
};

describe('NewContainerFacade', () => {
  const api = { createContainer: vi.fn(), getCommand: vi.fn(), listHostMetrics: vi.fn(), listEnvironments: vi.fn(), lookupImage: vi.fn() };

  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    Object.values(api).forEach(fn => fn.mockReset());
    TestBed.configureTestingModule({ providers: [NewContainerFacade, { provide: ControlPlaneApiService, useValue: api }] });
  });

  afterEach(() => vi.useRealTimers());

  it('reusa a chave de idempotência no retry após falha de rede e acompanha até "Em execução"', async () => {
    api.createContainer.mockRejectedValueOnce(new TypeError('network')).mockResolvedValue({ commandId: 'cmd-1' });
    api.getCommand.mockResolvedValue({ state: 'succeeded', result: { createdContainerId: 'c'.repeat(64) } });
    const facade = TestBed.inject(NewContainerFacade);

    await facade.submit('env-1', request);
    expect(facade.tracking()?.state).toBe('idle');
    await facade.submit('env-1', request);

    expect(api.createContainer.mock.calls[0][2]).toBe(api.createContainer.mock.calls[1][2]);
    await vi.advanceTimersByTimeAsync(1300);
    await vi.waitFor(() => expect(facade.tracking()?.state).toBe('succeeded'));
    expect(facade.tracking()?.containerId).toBe('c'.repeat(64));
  });

  it('nunca guarda valores de variáveis no estado, na URL ou no console', async () => {
    const log = vi.spyOn(console, 'log');
    const error = vi.spyOn(console, 'error');
    api.createContainer.mockResolvedValue({ commandId: 'cmd-2' });
    api.getCommand.mockResolvedValue({ state: 'failed', failureCode: 'port_conflict' });
    const facade = TestBed.inject(NewContainerFacade);

    await facade.submit('env-1', request);
    await vi.advanceTimersByTimeAsync(1300);
    await vi.waitFor(() => expect(facade.tracking()?.state).toBe('failed'));

    expect(JSON.stringify(facade.tracking())).not.toContain(CANARY);
    expect(location.href).not.toContain(CANARY);
    expect(JSON.stringify(sessionStorage)).not.toContain(CANARY);
    expect(JSON.stringify(localStorage)).not.toContain(CANARY);
    for (const call of [...log.mock.calls, ...error.mock.calls]) expect(JSON.stringify(call)).not.toContain(CANARY);
    expect(createFailureLabel(facade.tracking()?.failureCode)).toContain('removido');
  });

  it('mostra erros por campo da API e renova a chave após resposta definitiva', async () => {
    api.createContainer.mockRejectedValueOnce({ status: 400, code: 'invalid_container_spec', errors: { name: ['invalid_name'] } }).mockResolvedValue({ commandId: 'cmd-3' });
    const facade = TestBed.inject(NewContainerFacade);

    await facade.submit('env-1', request);
    expect(facade.serverErrors()).toEqual({ name: ['invalid_name'] });
    await facade.submit('env-1', request);
    expect(api.createContainer.mock.calls[0][2]).not.toBe(api.createContainer.mock.calls[1][2]);
  });
});

describe('createIneligibility e preset de health check', () => {
  const env = (overrides: Partial<EnvironmentState> = {}): EnvironmentState => ({
    environmentId: 'e', name: 'srv', host: 'srv', enabled: true, scopes: ['container:create'],
    agent: { connected: true, capabilities: ['ContainerCreate'] }, ...overrides,
  });

  it('cruza permissão, capability e conexão', () => {
    expect(createIneligibility(env())).toBeNull();
    expect(createIneligibility(env({ scopes: [] }))).toBe('sem permissão container:create');
    expect(createIneligibility(env({ agent: { connected: true, capabilities: [] } }))).toBe('agente sem suporte a criação');
    expect(createIneligibility(env({ agent: { connected: false, capabilities: ['ContainerCreate'] } }))).toBe('agente desconectado');
  });

  it('usa a primeira porta do container', () => {
    expect(healthPreset(8080)[1]).toContain('http://localhost:8080/healthz');
  });
});
