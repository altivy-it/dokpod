import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { ConsoleRealtimeService } from '../../core/console-realtime.service';
import { ControlPlaneApiService } from '../../data-access/control-plane-api.service';
import { LogsPanel } from './logs.panel';

type Handlers = { next: (chunk: unknown) => void; error: (e: unknown) => void; complete: () => void };

@Component({ imports: [LogsPanel], template: `<app-logs-panel environmentId="env-1" containerId="abc" containerName="api" />` })
class Host {}

describe('LogsPanel live session', () => {
  const handlers: Handlers[] = [];
  const realtime = {
    streamLogs: vi.fn(async (_e: string, _c: string, _q: string | null, h: Handlers) => {
      handlers.push(h);
      return { dispose: vi.fn() };
    }),
    reconnect: vi.fn().mockResolvedValue(undefined),
  };
  const api = {
    getContainerLogs: vi.fn().mockResolvedValue({
      lines: [{ timestamp: '2026-10-14T10:00:00Z', stream: 'stdout', level: 'INFO', text: '<script>alert(1)</script>' }],
      truncated: false,
      omittedLines: 0,
    }),
  };

  beforeEach(() => {
    handlers.length = 0;
    realtime.streamLogs.mockClear();
    realtime.reconnect.mockClear();
    TestBed.configureTestingModule({
      providers: [
        { provide: ConsoleRealtimeService, useValue: realtime },
        { provide: ControlPlaneApiService, useValue: api },
      ],
    });
  });

  async function settle(fixture: { detectChanges: () => void }): Promise<void> {
    for (let i = 0; i < 6; i++) await Promise.resolve();
    fixture.detectChanges();
  }

  it('renders log lines as text, never as markup', async () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    await settle(fixture);
    const log: HTMLElement = fixture.nativeElement.querySelector('[role=log]');
    expect(log.textContent).toContain('<script>alert(1)</script>');
    expect(log.querySelector('script')).toBeNull();
  });

  it('reconnects and resumes transparently when the session needs a token refresh', async () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    await settle(fixture);
    fixture.nativeElement.querySelector('button.lg-live').click();
    await settle(fixture);
    expect(realtime.streamLogs).toHaveBeenCalledTimes(1);

    handlers[0].next({ sessionId: 's', lines: [], omittedLines: 0, ended: true, endReason: 'session_refresh_required' });
    await settle(fixture);

    expect(realtime.reconnect).toHaveBeenCalledTimes(1);
    expect(realtime.streamLogs).toHaveBeenCalledTimes(2);
    expect(fixture.nativeElement.querySelector('button.lg-live').getAttribute('aria-pressed')).toBe('true');
  });

  it('stops and explains when the permission is revoked', async () => {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    await settle(fixture);
    fixture.nativeElement.querySelector('button.lg-live').click();
    await settle(fixture);

    handlers[0].next({ sessionId: 's', lines: [], omittedLines: 0, ended: true, endReason: 'authorization_revoked' });
    await settle(fixture);

    expect(realtime.reconnect).not.toHaveBeenCalled();
    expect(fixture.nativeElement.querySelector('button.lg-live').getAttribute('aria-pressed')).toBe('false');
    expect(fixture.nativeElement.querySelector('[role=status]').textContent).toContain('permissão de logs foi revogada');
  });
});
