import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { JustificationPolicyService } from '../../core/justification-policy.service';
import { ControlPlaneApiService } from '../../data-access/control-plane-api.service';
import type { PodAction, PodItem } from '../../data-access/generated/control-plane';
import { activeNavId, COMING_SOON, NAV_ITEMS } from '../../shell/navigation';
import { PodActionDrawer } from './pod-action.drawer';
import { evaluatePodEligibility, PodActionsService, podFailureText, type PodEligibility } from './pod-actions.service';
import { PodsApiService } from './pods-api.service';
import { podContainersText } from './pods.page';

function pod(name: string, overrides: Partial<PodItem> = {}): PodItem {
  return {
    environmentId: 'env-1',
    environmentName: 'srv-podman',
    stage: 'staging',
    hostConnected: true,
    podId: name.padEnd(64, '0').replace(/[^0-9a-f]/g, 'a').slice(0, 64),
    name,
    status: 'running',
    labels: {},
    networks: ['rede'],
    containers: [
      { containerId: 'a'.repeat(64), name: 'web', status: 'running', restarts: 0, infra: false },
      { containerId: 'b'.repeat(64), name: 'worker', status: 'exited', restarts: 1, infra: false },
    ],
    runningContainers: 1,
    agentPod: false,
    truncated: false,
    observedAt: '2026-10-16T12:00:00Z',
    allowedActions: ['stop', 'restart', 'pause'],
    ...overrides,
  };
}

describe('Pods (spec 021)', () => {
  it('replaces the coming-soon entry with the Pods route', () => {
    expect(NAV_ITEMS.find(i => i.id === 'pods')!.route).toBe('/pods');
    expect(COMING_SOON['pods']).toBeUndefined();
    expect(activeNavId('/pods?status=degraded')).toBe('pods');
  });

  it('summarizes members without the infra container', () => {
    expect(podContainersText(pod('a'))).toBe('1 de 2 em execução');
    expect(podContainersText({ containers: [{ containerId: 'c'.repeat(64), name: 'infra', status: 'running', restarts: 0, infra: true }], runningContainers: 0 })).toBe('nenhum');
  });

  it('skips the agent pod, disconnected hosts and incompatible states with a reason', () => {
    const result = evaluatePodEligibility(
      [
        pod('loja'),
        pod('pod_dokpod', { agentPod: true, allowedActions: [] }),
        pod('parado', { status: 'exited', allowedActions: ['start', 'restart'] }),
        pod('remoto', { hostConnected: false, allowedActions: [] }),
      ],
      'stop',
    );
    expect(result.eligible.map(p => p.name)).toEqual(['loja']);
    expect(result.skipped.map(s => [s.item.name, s.reason])).toEqual([
      ['pod_dokpod', 'pod do agente (protegido)'],
      ['parado', 'já está parado'],
      ['remoto', 'host desconectado'],
    ]);
  });

  it('labels partial failures', () => {
    expect(podFailureText('pod_partial_failure')).toBe('parte dos containers não mudou de estado');
    expect(podFailureText('agent_pod')).toBe('pod do agente (protegido)');
  });
});

describe('PodActionsService', () => {
  const api = { submitAction: vi.fn() };
  const commands = { getCommand: vi.fn() };

  beforeEach(() => {
    sessionStorage.clear();
    api.submitAction.mockReset();
    commands.getCommand.mockReset();
    TestBed.configureTestingModule({
      providers: [
        { provide: PodsApiService, useValue: api },
        { provide: ControlPlaneApiService, useValue: commands },
      ],
    });
  });

  it('sends one command per pod with the action and keeps the failed containers of a partial result', async () => {
    const a = pod('aaa');
    const b = pod('bbb');
    api.submitAction.mockImplementation(async (_env: string, id: string) => `cmd-${id}`);
    commands.getCommand.mockImplementation(async (_env: string, id: string) =>
      id === `cmd-${a.podId}`
        ? { state: 'succeeded', failureCode: null }
        : { state: 'failed', failureCode: 'pod_partial_failure', result: { failedContainers: ['worker'] } },
    );
    const service = TestBed.inject(PodActionsService);

    await service.run('restart', [a, b], null);

    expect(api.submitAction.mock.calls.map(call => [call[1], call[2]])).toEqual([
      [a.podId, 'restart'],
      [b.podId, 'restart'],
    ]);
    await vi.waitFor(() => expect(service.running()).toBe(false));
    const failed = service.batch()!.items.find(i => i.name === 'bbb')!;
    expect(failed.failedContainers).toEqual(['worker']);
    expect(service.summary()).toMatchObject({ succeeded: 1, failed: 1 });
  });
});

@Component({
  imports: [PodActionDrawer],
  template: `<app-pod-action-drawer [open]="true" [action]="action()" [eligibility]="eligibility()" (confirmed)="confirmed.push($event)" />`,
})
class Host {
  readonly action = signal<PodAction>('stop');
  readonly eligibility = signal<PodEligibility>({ eligible: [pod('loja', { stage: 'production' })], skipped: [] });
  readonly confirmed: (string | null)[] = [];
}

describe('pod-action drawer', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [{ provide: JustificationPolicyService, useValue: { required: signal(new Set(['production'])), load: vi.fn() } }],
    });
  });

  const button = (label: string) => Array.from(document.querySelectorAll<HTMLButtonElement>('button')).find(b => b.textContent?.trim() === label)!;

  it('requires a justification to stop in Production and none to restart', async () => {
    const fixture = TestBed.createComponent(Host);
    document.body.appendChild(fixture.nativeElement);
    fixture.detectChanges();
    await fixture.whenStable();
    expect(button('Parar').disabled).toBe(true);
    const textarea = document.querySelector<HTMLTextAreaElement>('#pa-justification')!;
    textarea.value = 'Janela de manutenção do serviço';
    textarea.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    await fixture.whenStable();
    button('Parar').click();
    expect(fixture.componentInstance.confirmed).toEqual(['Janela de manutenção do serviço']);

    fixture.componentInstance.action.set('restart');
    fixture.detectChanges();
    expect(document.querySelector('#pa-justification')).toBeNull();
    fixture.nativeElement.remove();
  });
});
