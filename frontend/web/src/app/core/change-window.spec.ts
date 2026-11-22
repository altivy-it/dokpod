import { TestBed } from '@angular/core/testing';
import type { ChangeWindowStatus, ContainerListItem } from '../data-access/generated/control-plane';
import { ChangeWindowNotice } from '../features/change-window/change-window-notice.component';
import {
  changeWindowReason,
  changeWindowReasonFor,
  isChangeWindowClosed,
  publishChangeWindows,
} from './change-window';
import { evaluateEligibility } from './container-actions/container-actions.service';

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
});
