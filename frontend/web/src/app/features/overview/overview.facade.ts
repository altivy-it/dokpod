import { DestroyRef, effect, inject, Injectable, signal, untracked } from '@angular/core';
import { ConsoleRealtimeService } from '../../core/console-realtime.service';
import { ControlPlaneApiService } from '../../data-access/control-plane-api.service';
import type { EnvironmentState, OperationalEvent, OverviewSummary, Stage } from '../../data-access/generated/control-plane';
import { StageFilterService } from '../../shell/stage-filter.service';

export interface LoadError {
  status: number | null;
  title: string;
  detail: string;
}

const EVENTS_IN_CARD = 6;
const MAX_HOST_PAGES = 10;

/** Estado da visão geral: resumo, hosts e eventos do estágio global, atualizados por invalidação. */
@Injectable()
export class OverviewFacade {
  private readonly api = inject(ControlPlaneApiService);
  private readonly realtime = inject(ConsoleRealtimeService);
  private readonly stageFilter = inject(StageFilterService);
  private version = 0;
  private refreshTimer: ReturnType<typeof setTimeout> | null = null;

  readonly summary = signal<OverviewSummary | null>(null);
  readonly hosts = signal<EnvironmentState[]>([]);
  readonly events = signal<OperationalEvent[]>([]);
  readonly loading = signal(true);
  readonly refreshing = signal(false);
  readonly error = signal<LoadError | null>(null);
  readonly loadedAt = signal<Date | null>(null);

  constructor() {
    effect(() => {
      const stage = this.stageFilter.stage();
      untracked(() => void this.load(stage, false));
    });
    effect(() => {
      const invalidation = this.realtime.lastInvalidation();
      if (!invalidation) return;
      untracked(() => {
        if (this.hosts().some(h => h.environmentId === invalidation.environmentId)) this.scheduleRefresh();
      });
    });
    inject(DestroyRef).onDestroy(() => {
      this.version++;
      if (this.refreshTimer) clearTimeout(this.refreshTimer);
    });
  }

  refresh(): Promise<void> {
    return this.load(this.stageFilter.stage(), true);
  }

  private scheduleRefresh(): void {
    if (this.refreshTimer) return;
    this.refreshTimer = setTimeout(() => {
      this.refreshTimer = null;
      void this.load(this.stageFilter.stage(), true);
    }, 1000);
  }

  private async load(stage: Stage | null, quiet: boolean): Promise<void> {
    const version = ++this.version;
    if (quiet) this.refreshing.set(true);
    else this.loading.set(true);
    try {
      const [summary, hosts, events] = await Promise.all([
        this.api.getOverview(stage),
        this.loadHosts(stage),
        this.api.listEvents({ ...(stage ? { stage } : {}), limit: EVENTS_IN_CARD }),
      ]);
      if (version !== this.version) return;
      this.summary.set(summary);
      this.hosts.set(hosts);
      this.events.set(events.events);
      this.error.set(null);
      this.loadedAt.set(new Date());
      void this.realtime.watch(hosts.map(h => h.environmentId));
    } catch (error: unknown) {
      if (version === this.version) this.error.set(toLoadError(error));
    } finally {
      if (version === this.version) {
        this.loading.set(false);
        this.refreshing.set(false);
      }
    }
  }

  private async loadHosts(stage: Stage | null): Promise<EnvironmentState[]> {
    const all: EnvironmentState[] = [];
    let cursor: string | undefined;
    for (let page = 0; page < MAX_HOST_PAGES; page++) {
      const result = await this.api.listEnvironments(cursor, stage ? { stage, limit: 100 } : { limit: 100 });
      all.push(...result.environments);
      if (!result.nextCursor) break;
      cursor = result.nextCursor;
    }
    return all;
  }
}

export function toLoadError(error: unknown): LoadError {
  const status = typeof error === 'object' && error !== null && typeof (error as { status?: unknown }).status === 'number'
    ? (error as { status: number }).status
    : null;
  if (status === 401) return { status, title: 'Sessão expirada', detail: 'Entre novamente para continuar.' };
  if (status === 403) return { status, title: 'Acesso negado', detail: 'Você não tem permissão para esta consulta.' };
  if (status === 503) {
    return { status, title: 'Plano de controle indisponível', detail: 'A autorização ou a persistência não respondeu. Tente novamente.' };
  }
  const detail = typeof error === 'object' && error !== null && typeof (error as { detail?: unknown }).detail === 'string'
    ? (error as { detail: string }).detail
    : 'Não foi possível consultar o plano de controle.';
  return { status, title: 'Falha ao carregar', detail };
}
