import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import type { ContainerListItem } from '../../data-access/generated/control-plane';
import { JustificationPolicyService } from '../justification-policy.service';
import { ConfirmActionDrawer } from './confirm-action.drawer';
import type { Eligibility } from './container-actions.service';

const container = (stage: ContainerListItem['stage']): ContainerListItem => ({
  containerId: 'c1',
  name: 'api',
  imageReference: 'img',
  state: 'running',
  revision: 'c1:1',
  observedAt: '2026-10-14T12:00:00Z',
  environmentId: 'env',
  environmentName: 'srv-01',
  stage,
  hostConnected: true,
  allowedActions: ['stop', 'restart'],
});

@Component({
  imports: [ConfirmActionDrawer],
  template: `<app-confirm-action-drawer [open]="true" [action]="action()" [eligibility]="eligibility()" (confirmed)="confirmed.push($event)" />`,
})
class Host {
  readonly action = signal<'stop' | 'restart'>('stop');
  readonly eligibility = signal<Eligibility>({ eligible: [container('production')], skipped: [] });
  readonly confirmed: (string | null)[] = [];
}

describe('confirm-action drawer justification (spec 016)', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [{ provide: JustificationPolicyService, useValue: { required: signal(new Set(['production'])), load: vi.fn() } }],
    });
  });

  function primary(fixture: { nativeElement: HTMLElement }): HTMLButtonElement {
    return Array.from(fixture.nativeElement.ownerDocument.querySelectorAll<HTMLButtonElement>('button')).find(b => b.textContent?.includes('Parar'))!;
  }

  it('blocks stop in Production until a 10+ character justification and emits it trimmed', async () => {
    const fixture = TestBed.createComponent(Host);
    document.body.appendChild(fixture.nativeElement);
    fixture.detectChanges();
    await fixture.whenStable();
    const textarea = document.querySelector<HTMLTextAreaElement>('#ca-justification')!;
    expect(document.querySelector('label[for=ca-justification]')!.textContent).toContain('obrigatória');
    expect(primary(fixture).disabled).toBe(true);

    textarea.value = '  Janela de manutenção  ';
    textarea.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    await fixture.whenStable();
    expect(primary(fixture).disabled).toBe(false);
    primary(fixture).click();
    expect(fixture.componentInstance.confirmed).toEqual(['Janela de manutenção']);
    fixture.nativeElement.remove();
  });

  it('has an optional field outside the policy and none for restart', async () => {
    const fixture = TestBed.createComponent(Host);
    fixture.componentInstance.eligibility.set({ eligible: [container('development')], skipped: [] });
    document.body.appendChild(fixture.nativeElement);
    fixture.detectChanges();
    await fixture.whenStable();
    expect(document.querySelector('label[for=ca-justification]')!.textContent).toContain('opcional');
    primary(fixture).click();
    expect(fixture.componentInstance.confirmed).toEqual([null]);

    fixture.componentInstance.action.set('restart');
    fixture.detectChanges();
    expect(document.querySelector('#ca-justification')).toBeNull();
    fixture.nativeElement.remove();
  });
});
