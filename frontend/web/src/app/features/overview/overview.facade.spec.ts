import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { vi } from 'vitest';
import { ConsoleRealtimeService } from '../../core/console-realtime.service';
import { ControlPlaneApiService } from '../../data-access/control-plane-api.service';
import { OverviewFacade, toLoadError } from './overview.facade';

describe('OverviewFacade', () => {
  const api = {
    getOverview: vi.fn(),
    listEnvironments: vi.fn(),
    listEvents: vi.fn(),
  };
  const lastInvalidation = signal<{ environmentId: string; kind: string } | null>(null);
  const realtime = { lastInvalidation, watch: vi.fn().mockResolvedValue(undefined) };

  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] });
    Object.values(api).forEach(fn => fn.mockReset());
    api.getOverview.mockResolvedValue({ hosts: { total: 1, bySituation: {} }, containers: { total: 3, byDisplayStatus: {} }, engines: [], attentionEvents24h: 0 });
    api.listEnvironments.mockResolvedValue({ environments: [{ environmentId: 'env-1', name: 'srv-prod-01' }], nextCursor: null });
    api.listEvents.mockResolvedValue({ events: [] });
    lastInvalidation.set(null);
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: '**', children: [] }]),
        OverviewFacade,
        { provide: ControlPlaneApiService, useValue: api },
        { provide: ConsoleRealtimeService, useValue: realtime },
      ],
    });
  });

  afterEach(() => vi.useRealTimers());

  async function settle(): Promise<void> {
    TestBed.tick();
    for (let i = 0; i < 6; i++) await Promise.resolve();
  }

  it('loads summary, hosts and events for the global stage and watches the hosts', async () => {
    await TestBed.inject(Router).navigateByUrl('/?stage=production');
    const facade = TestBed.inject(OverviewFacade);
    await settle();

    expect(api.getOverview).toHaveBeenCalledWith('production');
    expect(api.listEnvironments).toHaveBeenCalledWith(undefined, { stage: 'production', limit: 100 });
    expect(api.listEvents).toHaveBeenCalledWith({ stage: 'production', limit: 6 });
    expect(facade.summary()?.containers.total).toBe(3);
    expect(facade.loading()).toBe(false);
    expect(realtime.watch).toHaveBeenCalledWith(['env-1']);
  });

  it('coalesces invalidations of watched hosts into one quiet refresh', async () => {
    const facade = TestBed.inject(OverviewFacade);
    await settle();
    api.getOverview.mockClear();

    lastInvalidation.set({ environmentId: 'env-1', kind: 'inventory' });
    TestBed.tick();
    lastInvalidation.set({ environmentId: 'env-1', kind: 'events' });
    TestBed.tick();
    lastInvalidation.set({ environmentId: 'other', kind: 'inventory' });
    TestBed.tick();
    vi.advanceTimersByTime(1000);
    await settle();

    expect(api.getOverview).toHaveBeenCalledTimes(1);
    expect(facade.loading()).toBe(false);
  });

  it('keeps previous data and exposes a mapped error on failure', async () => {
    const facade = TestBed.inject(OverviewFacade);
    await settle();
    api.getOverview.mockRejectedValue({ status: 403 });

    await facade.refresh();

    expect(facade.error()?.title).toBe('Acesso negado');
    expect(facade.summary()?.containers.total).toBe(3);
  });

  it('maps API errors to user-facing messages', () => {
    expect(toLoadError({ status: 401 }).title).toBe('Sessão expirada');
    expect(toLoadError({ status: 503 }).title).toBe('Plano de controle indisponível');
    expect(toLoadError({ status: 500, detail: 'x' })).toEqual({ status: 500, title: 'Falha ao carregar', detail: 'x' });
    expect(toLoadError(new Error('rede')).status).toBeNull();
  });
});
