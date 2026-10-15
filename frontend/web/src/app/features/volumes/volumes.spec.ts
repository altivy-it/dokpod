import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { JustificationPolicyService } from '../../core/justification-policy.service';
import { ControlPlaneApiService } from '../../data-access/control-plane-api.service';
import type { VolumeItem } from '../../data-access/generated/control-plane';
import { activeNavId, COMING_SOON, NAV_ITEMS } from '../../shell/navigation';
import { RemoveVolumesDrawer, typedConfirmation } from './remove-volumes.drawer';
import { evaluateVolumeEligibility, VOLUME_BATCH_LIMIT, volumeFailureText, VolumeRemovalService, type VolumeEligibility } from './volume-removal.service';
import { VolumesApiService } from './volumes-api.service';
import { volumeDisplayName } from './volumes.page';

function volume(name: string, overrides: Partial<VolumeItem> = {}): VolumeItem {
  return {
    environmentId: 'env-1',
    environmentName: 'srv-01',
    stage: 'development',
    engine: 'docker',
    hostConnected: true,
    name,
    anonymous: false,
    driver: 'local',
    scope: 'local',
    labels: {},
    mounts: [],
    inUse: false,
    truncated: false,
    observedAt: '2026-10-15T12:00:00Z',
    canRemove: true,
    ...overrides,
  };
}

const mounted = { containerId: 'a'.repeat(64), containerName: 'api', destination: '/dados', readOnly: false };

describe('Volumes (spec 018)', () => {
  it('replaces the coming-soon entry with the Volumes route', () => {
    expect(NAV_ITEMS.find(i => i.id === 'volumes')!.route).toBe('/volumes');
    expect(COMING_SOON['volumes']).toBeUndefined();
    expect(activeNavId('/volumes?semUso=1')).toBe('volumes');
  });

  it('abbreviates anonymous volumes and keeps named ones', () => {
    expect(volumeDisplayName({ name: 'f'.repeat(64), anonymous: true })).toBe(`${'f'.repeat(12)}… (anônimo)`);
    expect(volumeDisplayName({ name: 'dados', anonymous: false })).toBe('dados');
  });

  it('skips volumes in use (even by stopped containers), disconnected hosts and missing permission', () => {
    const result = evaluateVolumeEligibility([
      volume('orfao'),
      volume('dados', { inUse: true, mounts: [mounted, { ...mounted, containerId: 'b'.repeat(64) }], canRemove: false }),
      volume('remoto', { hostConnected: false, canRemove: false }),
      volume('restrito', { canRemove: false }),
    ]);
    expect(result.eligible.map(v => v.name)).toEqual(['orfao']);
    expect(result.skipped.map(s => [s.item.name, s.reason])).toEqual([
      ['dados', 'em uso por 2 containers'],
      ['remoto', 'host desconectado'],
      ['restrito', 'sem permissão ou agente sem suporte'],
    ]);
  });

  it('labels the standardized result codes (FR-008)', () => {
    expect(volumeFailureText('volume_in_use')).toBe('em uso');
    expect(volumeFailureText('volume_not_found')).toBe('não encontrado');
    expect(volumeFailureText('volume_remove_failed')).toBe('recusado pelo engine ou driver');
    expect(volumeFailureText(null)).toBe('falha');
  });

  it('asks for the count in a batch, the name for one Production volume and nothing otherwise', () => {
    const of = (...items: VolumeItem[]): VolumeEligibility => ({ eligible: items, skipped: [] });
    expect(typedConfirmation(of(volume('a'), volume('b')))!.expected).toBe('2');
    expect(typedConfirmation(of(volume('dados-prod', { stage: 'production' })))!.expected).toBe('dados-prod');
    expect(typedConfirmation(of(volume('dados-dev')))).toBeNull();
  });
});

describe('VolumeRemovalService', () => {
  const api = { removeVolume: vi.fn() };
  const commands = { getCommand: vi.fn() };

  beforeEach(() => {
    sessionStorage.clear();
    api.removeVolume.mockReset();
    commands.getCommand.mockReset();
    TestBed.configureTestingModule({
      providers: [
        { provide: VolumesApiService, useValue: api },
        { provide: ControlPlaneApiService, useValue: commands },
      ],
    });
  });

  it('sends one keyed command per volume with the justification and tracks the results', async () => {
    api.removeVolume.mockImplementation(async (_env: string, name: string) => `cmd-${name}`);
    commands.getCommand.mockImplementation(async (_env: string, id: string) =>
      id === 'cmd-a' ? { state: 'succeeded', failureCode: null } : { state: 'failed', failureCode: 'volume_in_use' },
    );
    const service = TestBed.inject(VolumeRemovalService);

    await service.run([volume('a'), volume('b')], 'Limpeza de órfãos');

    expect(api.removeVolume.mock.calls.map(call => [call[1], call[3]])).toEqual([
      ['a', 'Limpeza de órfãos'],
      ['b', 'Limpeza de órfãos'],
    ]);
    expect(new Set(api.removeVolume.mock.calls.map(call => call[2])).size).toBe(2);
    await vi.waitFor(() => expect(service.running()).toBe(false));
    expect(service.summary()).toMatchObject({ succeeded: 1, failed: 1 });
    expect(service.batch()!.items.find(i => i.name === 'b')!.failureCode).toBe('volume_in_use');
  });

  it('refuses more than the batch limit', async () => {
    const service = TestBed.inject(VolumeRemovalService);
    const many = Array.from({ length: VOLUME_BATCH_LIMIT + 1 }, (_, i) => volume(`v${i}`));
    await expect(service.run(many, null)).rejects.toThrow('no máximo 50');
    expect(api.removeVolume).not.toHaveBeenCalled();
  });

  it('keeps network failures queued and resends them with the same key only on request', async () => {
    api.removeVolume.mockRejectedValueOnce(new TypeError('network'));
    const service = TestBed.inject(VolumeRemovalService);
    await service.run([volume('a')], null);
    const first = service.batch()!.items[0];
    expect(first.state).toBe('queued');
    expect(service.running()).toBe(false);
    expect(service.summary().unsubmitted).toBe(1);

    api.removeVolume.mockResolvedValueOnce('cmd-a');
    commands.getCommand.mockResolvedValue({ state: 'succeeded', failureCode: null });
    await service.retryUnsubmitted();
    expect(api.removeVolume.mock.calls[1][2]).toBe(first.idempotencyKey);
    await vi.waitFor(() => expect(service.summary().succeeded).toBe(1));
  });

  it('resumes tracking after reload without resubmitting', async () => {
    sessionStorage.setItem(
      'dokpod.volume-batch.v1',
      JSON.stringify({
        id: 'b1',
        justification: null,
        startedAt: '2026-10-15T12:00:00Z',
        items: [
          { environmentId: 'env-1', environmentName: 'srv-01', name: 'a', idempotencyKey: 'k1', commandId: 'cmd-a', state: 'dispatched', failureCode: null },
          { environmentId: 'env-1', environmentName: 'srv-01', name: 'b', idempotencyKey: 'k2', commandId: null, state: 'submitting', failureCode: null },
        ],
      }),
    );
    commands.getCommand.mockResolvedValue({ state: 'succeeded', failureCode: null });

    const service = TestBed.inject(VolumeRemovalService);

    await vi.waitFor(() => expect(service.summary().succeeded).toBe(1));
    expect(service.batch()!.items[1].state).toBe('queued');
    expect(api.removeVolume).not.toHaveBeenCalled();
  });
});

@Component({
  imports: [RemoveVolumesDrawer],
  template: `<app-remove-volumes-drawer [open]="true" [eligibility]="eligibility()" (confirmed)="confirmed.push($event)" />`,
})
class Host {
  readonly eligibility = signal<VolumeEligibility>({ eligible: [volume('dados-prod', { stage: 'production' })], skipped: [] });
  readonly confirmed: (string | null)[] = [];
}

describe('remove-volumes drawer (FR-006)', () => {
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

  it('warns about permanent deletion and requires the name and a justification in Production', async () => {
    const fixture = TestBed.createComponent(Host);
    fixture.componentInstance.eligibility.update(e => ({ ...e, skipped: [{ item: volume('dados', { inUse: true }), reason: 'em uso por 1 container' }] }));
    document.body.appendChild(fixture.nativeElement);
    fixture.detectChanges();
    await fixture.whenStable();
    expect(document.body.textContent).toContain('apagados de forma definitiva');
    expect(document.body.textContent).toContain('em uso por 1 container');
    expect(primary().disabled).toBe(true);

    type(document.querySelector<HTMLTextAreaElement>('#rv-justification')!, 'Limpeza de volume órfão');
    fixture.detectChanges();
    expect(primary().disabled).toBe(true);

    const typed = Array.from(document.querySelectorAll<HTMLInputElement>('input')).find(i => !i.closest('textarea'))!;
    type(typed, 'dados-prod');
    fixture.detectChanges();
    await fixture.whenStable();
    expect(primary().disabled).toBe(false);
    primary().click();
    expect(fixture.componentInstance.confirmed).toEqual(['Limpeza de volume órfão']);
    fixture.nativeElement.remove();
  });

  it('confirms one development volume with an optional justification and no typing', async () => {
    const fixture = TestBed.createComponent(Host);
    fixture.componentInstance.eligibility.set({ eligible: [volume('cache')], skipped: [] });
    document.body.appendChild(fixture.nativeElement);
    fixture.detectChanges();
    await fixture.whenStable();
    expect(document.querySelector('label[for=rv-justification]')!.textContent).toContain('opcional');
    primary().click();
    expect(fixture.componentInstance.confirmed).toEqual([null]);
    fixture.nativeElement.remove();
  });
});
