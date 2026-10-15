import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { JustificationPolicyService } from '../../core/justification-policy.service';
import { ControlPlaneApiService } from '../../data-access/control-plane-api.service';
import type { NetworkItem } from '../../data-access/generated/control-plane';
import { activeNavId, COMING_SOON, NAV_ITEMS } from '../../shell/navigation';
import { evaluateNetworkEligibility, NETWORK_BATCH_LIMIT, networkFailureText, NetworkRemovalService, type NetworkEligibility } from './network-removal.service';
import { NetworksApiService } from './networks-api.service';
import { networkUsage } from './networks.page';
import { RemoveNetworksDrawer, typedConfirmation } from './remove-networks.drawer';

function network(name: string, overrides: Partial<NetworkItem> = {}): NetworkItem {
  return {
    environmentId: 'env-1',
    environmentName: 'srv-01',
    stage: 'development',
    engine: 'docker',
    hostConnected: true,
    networkId: name.padEnd(64, '0').replace(/[^0-9a-f]/g, 'a').slice(0, 64),
    name,
    driver: 'bridge',
    scope: 'local',
    internal: false,
    ipv6: false,
    attachable: false,
    system: false,
    subnets: [{ subnet: '10.89.77.0/24', gateway: '10.89.77.1' }],
    labels: {},
    containers: [],
    inUse: false,
    truncated: false,
    observedAt: '2026-10-15T12:00:00Z',
    canRemove: true,
    ...overrides,
  };
}

const connected = { containerId: 'a'.repeat(64), containerName: 'worker', ipv4: null, ipv6: null };

describe('Redes (spec 019)', () => {
  it('replaces the coming-soon entry with the Networks route', () => {
    expect(NAV_ITEMS.find(i => i.id === 'networks')!.route).toBe('/redes');
    expect(COMING_SOON['redes']).toBeUndefined();
    expect(activeNavId('/redes?semUso=1')).toBe('networks');
  });

  it('labels usage with system networks apart from unused ones', () => {
    expect(networkUsage({ system: true, inUse: false })).toBe('Do sistema');
    expect(networkUsage({ system: false, inUse: true })).toBe('Em uso');
    expect(networkUsage({ system: false, inUse: false })).toBe('Sem uso');
  });

  it('skips system, cluster and in-use networks (even by stopped containers), disconnected hosts and missing permission', () => {
    const result = evaluateNetworkEligibility([
      network('orfa'),
      network('bridge', { system: true, canRemove: false }),
      network('cluster', { scope: 'swarm', canRemove: false }),
      network('usada', { inUse: true, containers: [connected], canRemove: false }),
      network('remota', { hostConnected: false, canRemove: false }),
      network('restrita', { canRemove: false }),
    ]);
    expect(result.eligible.map(n => n.name)).toEqual(['orfa']);
    expect(result.skipped.map(s => [s.item.name, s.reason])).toEqual([
      ['bridge', 'rede do sistema'],
      ['cluster', 'rede de cluster'],
      ['usada', 'em uso por 1 container'],
      ['remota', 'host desconectado'],
      ['restrita', 'sem permissão ou agente sem suporte'],
    ]);
  });

  it('labels the standardized result codes (FR-008)', () => {
    expect(networkFailureText('network_in_use')).toBe('em uso');
    expect(networkFailureText('network_not_found')).toBe('não encontrada');
    expect(networkFailureText('network_system')).toBe('rede do sistema');
    expect(networkFailureText('network_remove_failed')).toBe('recusada pelo engine ou driver');
  });

  it('asks for the count only in a batch with a Production network', () => {
    const of = (...items: NetworkItem[]): NetworkEligibility => ({ eligible: items, skipped: [] });
    expect(typedConfirmation(of(network('a'), network('b', { stage: 'production' })))!.expected).toBe('2');
    expect(typedConfirmation(of(network('a'), network('b')))).toBeNull();
    expect(typedConfirmation(of(network('prod', { stage: 'production' })))).toBeNull();
  });
});

describe('NetworkRemovalService', () => {
  const api = { removeNetwork: vi.fn() };
  const commands = { getCommand: vi.fn() };

  beforeEach(() => {
    sessionStorage.clear();
    api.removeNetwork.mockReset();
    commands.getCommand.mockReset();
    TestBed.configureTestingModule({
      providers: [
        { provide: NetworksApiService, useValue: api },
        { provide: ControlPlaneApiService, useValue: commands },
      ],
    });
  });

  it('sends one keyed command per network id with the justification and tracks the results', async () => {
    const a = network('aaa');
    const b = network('bbb');
    api.removeNetwork.mockImplementation(async (_env: string, id: string) => `cmd-${id}`);
    commands.getCommand.mockImplementation(async (_env: string, id: string) =>
      id === `cmd-${a.networkId}` ? { state: 'succeeded', failureCode: null } : { state: 'failed', failureCode: 'network_in_use' },
    );
    const service = TestBed.inject(NetworkRemovalService);

    await service.run([a, b], 'Limpeza de redes antigas');

    expect(api.removeNetwork.mock.calls.map(call => [call[1], call[3]])).toEqual([
      [a.networkId, 'Limpeza de redes antigas'],
      [b.networkId, 'Limpeza de redes antigas'],
    ]);
    expect(new Set(api.removeNetwork.mock.calls.map(call => call[2])).size).toBe(2);
    await vi.waitFor(() => expect(service.running()).toBe(false));
    expect(service.summary()).toMatchObject({ succeeded: 1, failed: 1 });
    expect(service.batch()!.items.find(i => i.name === 'bbb')!.failureCode).toBe('network_in_use');
  });

  it('refuses more than the batch limit', async () => {
    const service = TestBed.inject(NetworkRemovalService);
    const many = Array.from({ length: NETWORK_BATCH_LIMIT + 1 }, (_, i) => network(`n${i}`, { networkId: i.toString(16).padStart(64, '0') }));
    await expect(service.run(many, null)).rejects.toThrow('no máximo 50');
    expect(api.removeNetwork).not.toHaveBeenCalled();
  });

  it('resumes tracking after reload without resubmitting', async () => {
    sessionStorage.setItem(
      'dokpod.network-batch.v1',
      JSON.stringify({
        id: 'b1',
        justification: null,
        startedAt: '2026-10-15T12:00:00Z',
        items: [
          { environmentId: 'env-1', environmentName: 'srv-01', networkId: '1'.repeat(64), name: 'a', idempotencyKey: 'k1', commandId: 'cmd-a', state: 'dispatched', failureCode: null },
          { environmentId: 'env-1', environmentName: 'srv-01', networkId: '2'.repeat(64), name: 'b', idempotencyKey: 'k2', commandId: null, state: 'submitting', failureCode: null },
        ],
      }),
    );
    commands.getCommand.mockResolvedValue({ state: 'succeeded', failureCode: null });

    const service = TestBed.inject(NetworkRemovalService);

    await vi.waitFor(() => expect(service.summary().succeeded).toBe(1));
    expect(service.batch()!.items[1].state).toBe('queued');
    expect(api.removeNetwork).not.toHaveBeenCalled();
  });
});

@Component({
  imports: [RemoveNetworksDrawer],
  template: `<app-remove-networks-drawer [open]="true" [eligibility]="eligibility()" (confirmed)="confirmed.push($event)" />`,
})
class Host {
  readonly eligibility = signal<NetworkEligibility>({ eligible: [network('aaa', { stage: 'production' }), network('bbb')], skipped: [] });
  readonly confirmed: (string | null)[] = [];
}

describe('remove-networks drawer (FR-006)', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [{ provide: JustificationPolicyService, useValue: { required: signal(new Set(['production'])), load: vi.fn() } }],
    });
  });

  const primary = () => Array.from(document.querySelectorAll<HTMLButtonElement>('button')).find(b => b.textContent?.trim() === 'Remover')!;
  const type = (element: HTMLInputElement | HTMLTextAreaElement, value: string) => {
    element.value = value;
    element.dispatchEvent(new Event('input'));
  };

  it('requires the count and a justification for a Production batch, without a data-loss warning', async () => {
    const fixture = TestBed.createComponent(Host);
    document.body.appendChild(fixture.nativeElement);
    fixture.detectChanges();
    await fixture.whenStable();
    expect(document.body.textContent).toContain('Containers não são desconectados');
    expect(document.body.textContent).not.toContain('definitiva');
    expect(primary().disabled).toBe(true);

    type(document.querySelector<HTMLTextAreaElement>('#rv-justification')!, 'Limpeza de redes antigas');
    type(Array.from(document.querySelectorAll<HTMLInputElement>('input'))[0], '2');
    fixture.detectChanges();
    await fixture.whenStable();
    expect(primary().disabled).toBe(false);
    primary().click();
    expect(fixture.componentInstance.confirmed).toEqual(['Limpeza de redes antigas']);
    fixture.nativeElement.remove();
  });

  it('confirms one development network with an optional justification and no typing', async () => {
    const fixture = TestBed.createComponent(Host);
    fixture.componentInstance.eligibility.set({ eligible: [network('cache')], skipped: [] });
    document.body.appendChild(fixture.nativeElement);
    fixture.detectChanges();
    await fixture.whenStable();
    expect(document.querySelector('label[for=rv-justification]')!.textContent).toContain('opcional');
    expect(document.querySelectorAll('input').length).toBe(0);
    primary().click();
    expect(fixture.componentInstance.confirmed).toEqual([null]);
    fixture.nativeElement.remove();
  });
});
