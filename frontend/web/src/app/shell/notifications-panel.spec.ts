import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { vi } from 'vitest';
import { ControlPlaneApiService } from '../data-access/control-plane-api.service';
import { DokNotificationsPanel } from './notifications-panel';

const event = (id: string, type: string, severity: string, minutesAgo: number) => ({
  eventId: id,
  environmentId: 'env',
  environmentName: 'srv-prod-01',
  type,
  severity,
  occurredAt: new Date(Date.now() - minutesAgo * 60_000).toISOString(),
  receivedAt: new Date().toISOString(),
  clockSkewDetected: false,
  subjectName: 'gateway-01',
  parameters: {},
  aggregatedCount: 1,
});

@Component({ imports: [DokNotificationsPanel], template: `<dok-notifications-panel [count]="3" stage="production" />` })
class Host {}

describe('dok-notifications-panel', () => {
  const listEvents = vi.fn();

  beforeEach(() => {
    listEvents.mockReset();
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: ControlPlaneApiService, useValue: { listEvents } }],
    });
  });

  it('loads attention events on open and closes with Escape returning focus', async () => {
    listEvents.mockResolvedValue({
      events: [
        event('1', 'container.died', 'error', 3),
        event('2', 'agent.connected', 'info', 5),
        event('3', 'container.auto_restarted', 'warning', 9),
      ],
    });
    const fixture = TestBed.createComponent(Host);
    document.body.appendChild(fixture.nativeElement);
    fixture.detectChanges();
    const bell: HTMLButtonElement = fixture.nativeElement.querySelector('button[aria-haspopup=dialog]');
    expect(bell.getAttribute('aria-label')).toBe('Eventos que pedem atenção, 3 nas últimas 24 horas');

    bell.click();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role=status]').textContent).toContain('Carregando');
    await fixture.whenStable();
    fixture.detectChanges();

    expect(listEvents).toHaveBeenCalledWith(expect.objectContaining({ stage: 'production', limit: 50 }));
    const items = Array.from<HTMLElement>(fixture.nativeElement.querySelectorAll('li'));
    expect(items.map(li => li.querySelector('.dok-notif__text')!.textContent!.trim())).toEqual([
      'Container encerrado com erro',
      'Reinício automático pela política do container',
    ]);
    expect(bell.getAttribute('aria-expanded')).toBe('true');

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role=dialog]')).toBeNull();
    expect(document.activeElement).toBe(bell);
    fixture.nativeElement.remove();
  });

  it('reports load failures', async () => {
    listEvents.mockRejectedValue(new Error('boom'));
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    fixture.nativeElement.querySelector('button').click();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role=alert]').textContent).toContain('Não foi possível');
  });
});
