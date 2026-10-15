import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { vi } from 'vitest';
import { AlertSummaryService } from '../core/alert-summary.service';
import { EnvironmentDirectoryService } from '../core/environment-directory.service';
import type { Alert, AlertSummary } from '../data-access/generated/control-plane';
import { DokNotificationsPanel } from './notifications-panel';

const alert = (id: string, severity: Alert['severity'], condition: string): Alert => ({
  id,
  ruleId: 'rule',
  ruleName: 'Container encerrado com erro',
  environmentId: 'env',
  containerId: 'c1',
  containerName: 'gateway-01',
  severity,
  state: 'open',
  condition,
  openedAt: new Date(Date.now() - 5 * 60_000).toISOString(),
  lastOccurredAt: new Date(Date.now() - 3 * 60_000).toISOString(),
  occurrences: 1,
  canManage: true,
});

@Component({ imports: [DokNotificationsPanel], template: `<dok-notifications-panel [count]="3" stage="production" />` })
class Host {}

describe('dok-notifications-panel', () => {
  const summary = signal<AlertSummary | null>(null);
  const error = signal(false);
  const refresh = vi.fn();

  beforeEach(() => {
    summary.set(null);
    error.set(false);
    refresh.mockReset();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: AlertSummaryService, useValue: { summary, error, refresh } },
        { provide: EnvironmentDirectoryService, useValue: { byId: () => ({ name: 'srv-prod-01' }) } },
      ],
    });
  });

  it('lists open alerts linking to the Alerts page and closes with Escape returning focus', async () => {
    refresh.mockImplementation(async () =>
      summary.set({
        open: 3,
        bySeverity: { critical: 0, high: 2, medium: 1, low: 0 },
        recent: [alert('a1', 'high', 'Container gateway-01 encerrado com erro (código 1)'), alert('a2', 'medium', 'CPU de srv-prod-01 em 91%')],
      }),
    );
    const fixture = TestBed.createComponent(Host);
    document.body.appendChild(fixture.nativeElement);
    fixture.detectChanges();
    const bell: HTMLButtonElement = fixture.nativeElement.querySelector('button[aria-haspopup=dialog]');
    expect(bell.getAttribute('aria-label')).toBe('Alertas abertos, 3');

    bell.click();
    fixture.detectChanges();
    expect(refresh).toHaveBeenCalledWith('production');
    await fixture.whenStable();
    fixture.detectChanges();

    const items = Array.from<HTMLAnchorElement>(fixture.nativeElement.querySelectorAll('a.dok-notif__item'));
    expect(items.map(a => a.querySelector('.dok-notif__text')!.textContent!.trim())).toEqual([
      'Container gateway-01 encerrado com erro (código 1)',
      'CPU de srv-prod-01 em 91%',
    ]);
    expect(items[0].getAttribute('href')).toContain('/alertas?alerta=a1');
    expect(items[0].textContent).toContain('srv-prod-01 · Container encerrado com erro');
    expect(fixture.nativeElement.querySelector('.dok-notif__foot a').textContent).toContain('Ver todos os alertas');
    expect(bell.getAttribute('aria-expanded')).toBe('true');

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role=dialog]')).toBeNull();
    expect(document.activeElement).toBe(bell);
    fixture.nativeElement.remove();
  });

  it('shows the empty state and load failures', async () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    fixture.nativeElement.querySelector('button').click();
    error.set(true);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role=alert]').textContent).toContain('Não foi possível');

    error.set(false);
    summary.set({ open: 0, bySeverity: {}, recent: [] });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Nenhum alerta aberto.');
  });
});
