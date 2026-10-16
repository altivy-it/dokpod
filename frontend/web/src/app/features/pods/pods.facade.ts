import { DestroyRef, effect, inject, Injectable, signal, untracked } from '@angular/core';
import { ConsoleRealtimeService } from '../../core/console-realtime.service';
import { EnvironmentDirectoryService } from '../../core/environment-directory.service';
import type { ListPodsData, PodItem, PodListPage, PodStatus, Stage } from '../../data-access/generated/control-plane';
import { StageFilterService } from '../../shell/stage-filter.service';
import { toLoadError, type LoadError } from '../overview/overview.facade';
import { PodsApiService } from './pods-api.service';

export type PodSort = NonNullable<NonNullable<ListPodsData['query']>['sort']>;

export interface PodFilters {
  environmentIds: string[];
  statuses: PodStatus[];
  q: string;
  sort: PodSort;
}

export const EMPTY_POD_FILTERS: PodFilters = { environmentIds: [], statuses: [], q: '', sort: 'name' };

export function podKey(item: Pick<PodItem, 'environmentId' | 'podId'>): string {
  return `${item.environmentId}/${item.podId}`;
}

/** Pods dos hosts Podman (spec 021 US1): filtros vêm da URL; invalidações `pods` e `inventory` recarregam a página. */
@Injectable()
export class PodsFacade {
  private readonly api = inject(PodsApiService);
  private readonly realtime = inject(ConsoleRealtimeService);
  private readonly stageFilter = inject(StageFilterService);
  private readonly directory = inject(EnvironmentDirectoryService);
  private version = 0;
  private refreshTimer: ReturnType<typeof setTimeout> | null = null;

  readonly filters = signal<PodFilters>(EMPTY_POD_FILTERS);
  readonly items = signal<PodItem[]>([]);
  readonly summary = signal<PodListPage['summary'] | null>(null);
  readonly nextCursor = signal<string | null>(null);
  readonly loading = signal(true);
  readonly loadingMore = signal(false);
  readonly error = signal<LoadError | null>(null);
  readonly updatedAt = signal<string | null>(null);

  constructor() {
    effect(() => {
      const stage = this.stageFilter.stage();
      const filters = this.filters();
      untracked(() => void this.load(stage, filters));
    });
    effect(() => {
      const invalidation = this.realtime.lastInvalidation();
      if (invalidation?.kind !== 'pods' && invalidation?.kind !== 'inventory') return;
      untracked(() => this.scheduleRefresh());
    });
    inject(DestroyRef).onDestroy(() => {
      this.version++;
      if (this.refreshTimer) clearTimeout(this.refreshTimer);
    });
  }

  reload(): Promise<void> {
    return this.load(this.stageFilter.stage(), this.filters());
  }

  async loadMore(): Promise<void> {
    const cursor = this.nextCursor();
    if (!cursor || this.loadingMore()) return;
    const version = this.version;
    this.loadingMore.set(true);
    try {
      const page = await this.api.listPods(this.query(this.stageFilter.stage(), this.filters(), cursor));
      if (version !== this.version) return;
      const known = new Set(this.items().map(podKey));
      this.items.update(current => [...current, ...page.pods.filter(p => !known.has(podKey(p)))]);
      this.nextCursor.set(page.nextCursor ?? null);
    } catch (error: unknown) {
      this.error.set(toLoadError(error));
    } finally {
      this.loadingMore.set(false);
    }
  }

  private scheduleRefresh(): void {
    if (this.refreshTimer) return;
    this.refreshTimer = setTimeout(() => {
      this.refreshTimer = null;
      void this.load(this.stageFilter.stage(), this.filters(), true);
    }, 1500);
  }

  private async load(stage: Stage | null, filters: PodFilters, silent = false): Promise<void> {
    const version = ++this.version;
    if (!silent) this.loading.set(true);
    try {
      const page = await this.api.listPods(this.query(stage, filters));
      if (version !== this.version) return;
      this.items.set(page.pods);
      this.summary.set(page.summary);
      this.nextCursor.set(page.nextCursor ?? null);
      this.updatedAt.set(new Date().toISOString());
      this.error.set(null);
      // Assina também os hosts sem pods na página, para ver o primeiro pod criado fora do Dokpod (SC-002).
      const hosts = this.directory.environments().filter(env => !stage || env.stage === stage).map(env => env.environmentId);
      void this.realtime.watch([...new Set([...page.pods.map(p => p.environmentId), ...hosts])]);
    } catch (error: unknown) {
      if (version === this.version && !silent) this.error.set(toLoadError(error));
    } finally {
      if (version === this.version) this.loading.set(false);
    }
  }

  private query(stage: Stage | null, filters: PodFilters, cursor?: string): NonNullable<ListPodsData['query']> {
    return {
      ...(stage ? { stage } : {}),
      ...(filters.environmentIds.length ? { environmentId: filters.environmentIds } : {}),
      ...(filters.statuses.length ? { status: filters.statuses } : {}),
      ...(filters.q.trim().length >= 2 ? { q: filters.q.trim() } : {}),
      sort: filters.sort,
      limit: 50,
      ...(cursor ? { cursor } : {}),
    };
  }
}
