import { TestBed } from '@angular/core/testing';
import type { ChangeWindowStatus, ContainerListItem } from '../data-access/generated/control-plane';
import { ChangeWindowNotice } from '../features/change-window/change-window-notice.component';
import {
  changeWindowReason,
  changeWindowReasonFor,
  isChangeWindowClosed,
  publishChangeWindows,
} from './change-window';
import { ConfirmActionDrawer } from './container-actions/confirm-action.drawer';
import { evaluateEligibility, overrideEligibility } from './container-actions/container-actions.service';
import { JustificationPolicyService } from './justification-policy.service';

const closed: ChangeWindowStatus = {
  state: 'closed',
  closesAt: null,
  nextOpensAt: '2026-11-25T01:00:00Z',
  timeZone: 'America/Sao_Paulo',
};
const open: ChangeWindowStatus = {
  state: 'open',
  closesAt: '2026-11-25T09:00:00Z',
  nextOpensAt: null,
  timeZone: 'America/Sao_Paulo',
};

function item(environmentId: string, changeWindow?: ChangeWindowStatus): ContainerListItem {
  return {
    environmentId,
    environmentName: environmentId,
    containerId: environmentId.padEnd(64, '0'),
    name: `web-${environmentId}`,
    state: 'running',
    hostConnected: true,
    allowedActions: ['stop', 'restart'],
    changeWindow,
  } as unknown as ContainerListItem;
}

describe('Janela de mudança na tela (spec 026 T027)', () => {
  beforeEach(() => vi.useFakeTimers({ now: new Date('2026-11-24T18:00:00Z') }));
  afterEach(() => {
    publishChangeWindows([]);
    vi.useRealTimers();
  });

  it('fora da janela dá o motivo com a próxima abertura no fuso do estágio', () => {
    publishChangeWindows([]);
    expect(isChangeWindowClosed(closed)).toBe(true);
    expect(changeWindowReason(closed)).toBe('fora da janela de mudança (abre em 24/11/2026, 22:00 (America/Sao_Paulo))');
    expect(isChangeWindowClosed({ state: 'none', closesAt: null, nextOpensAt: null, timeZone: null })).toBe(false);
    expect(changeWindowReason(undefined)).toBeNull();
  });

  it('troca de estado sozinho em nextOpensAt e em closesAt, sem nova consulta', () => {
    publishChangeWindows([['host-a', closed]]);
    expect(changeWindowReasonFor('host-a')).not.toBeNull();

    vi.advanceTimersByTime(7 * 3600_000 + 1000);
    expect(changeWindowReasonFor('host-a')).toBeNull();

    publishChangeWindows([['host-a', open]]);
    expect(changeWindowReasonFor('host-a')).toBeNull();
    vi.advanceTimersByTime(8 * 3600_000 + 1000);
    expect(changeWindowReasonFor('host-a')).toBe('fora da janela de mudança');
  });

  it('num lote misto, só os itens fora da janela ficam de fora, cada um com o motivo', () => {
    publishChangeWindows([['host-b', { state: 'none', closesAt: null, nextOpensAt: null, timeZone: null }]]);
    const result = evaluateEligibility([item('host-a', closed), item('host-b')], 'stop');

    expect(result.eligible.map(entry => entry.environmentId)).toEqual(['host-b']);
    expect(result.skipped).toEqual([{ item: expect.objectContaining({ environmentId: 'host-a' }), reason: changeWindowReason(closed) }]);
  });

  it('o aviso mostra o texto e some quando a janela abre', async () => {
    publishChangeWindows([]);
    const fixture = TestBed.createComponent(ChangeWindowNotice);
    fixture.componentRef.setInput('status', closed);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Fora da janela de mudança');
    expect(fixture.nativeElement.textContent).toContain('24/11/2026, 22:00 (America/Sao_Paulo)');

    publishChangeWindows([['host-a', closed]]);
    vi.advanceTimersByTime(7 * 3600_000 + 1000);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent.trim()).toBe('');
  });

  it('no modo exceção, entra o host com change:override e o outro mostra o motivo (US3)', () => {
    publishChangeWindows([
      ['host-a', closed, ['container:stop', 'change:override']],
      ['host-c', closed, ['container:stop']],
    ]);
    const items = [item('host-a', closed), item('host-c', closed)];

    expect(evaluateEligibility(items, 'stop').eligible).toEqual([]);
    const withOverride = overrideEligibility(items, 'stop')!;
    expect(withOverride.eligible.map(entry => entry.environmentId)).toEqual(['host-a']);
    expect(withOverride.skipped[0].reason).toContain('sem change:override neste host');
    expect(overrideEligibility([item('host-c', closed)], 'stop')).toBeNull();
  });

  it('o drawer oferece a exceção, exige a justificativa e confirma no modo exceção', async () => {
    publishChangeWindows([['host-a', closed, ['container:stop', 'change:override']]]);
    const items = [item('host-a', closed)];
    TestBed.configureTestingModule({
      providers: [{ provide: JustificationPolicyService, useValue: { required: () => ({}), load: () => Promise.resolve() } }],
    });
    const fixture = TestBed.createComponent(ConfirmActionDrawer);
    fixture.componentRef.setInput('open', true);
    fixture.componentRef.setInput('action', 'restart');
    fixture.componentRef.setInput('eligibility', evaluateEligibility(items, 'restart'));
    fixture.componentRef.setInput('overrideEligibility', overrideEligibility(items, 'restart'));
    const confirmed: string[] = [];
    fixture.componentInstance.overrideConfirmed.subscribe(text => confirmed.push(text));
    fixture.detectChanges();

    const root = document.body;
    const checkbox = [...root.querySelectorAll('input[type="checkbox"]')].find(input =>
      input.closest('label')?.textContent?.includes('Executar fora da janela (exceção de emergência)'),
    ) as HTMLInputElement;
    expect(checkbox).toBeTruthy();
    checkbox.click();
    fixture.detectChanges();
    expect(root.textContent).toContain('Justificativa da exceção (obrigatória)');

    const textarea = root.querySelector('#ca-justification') as HTMLTextAreaElement;
    textarea.value = 'Incidente INC-123: container travado';
    textarea.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    (fixture.componentInstance as unknown as { confirm: () => void }).confirm();

    expect(confirmed).toEqual(['Incidente INC-123: container travado']);
  });
});
