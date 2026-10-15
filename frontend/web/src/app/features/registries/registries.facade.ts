import { DestroyRef, effect, inject, Injectable, signal, untracked } from '@angular/core';
import { ConsoleRealtimeService } from '../../core/console-realtime.service';
import { EnvironmentDirectoryService } from '../../core/environment-directory.service';
import type { ListRegistriesData, RegistryItem, RegistryListPage, RegistryOrigin, Stage } from '../../data-access/generated/control-plane';
import { StageFilterService } from '../../shell/stage-filter.service';
import { toLoadError, type LoadError } from '../overview/overview.facade';
import { RegistriesApiService } from './registries-api.service';

export type RegistrySort = NonNullable<NonNullable<ListRegistriesData['query']>['sort']>;

export interface RegistryFilters {
  environmentIds: string[];
  origins: RegistryOrigin[];
  insecure: boolean;
  outsidePolicy: boolean;
  q: string;
  sort: RegistrySort;
}

export const EMPTY_REGISTRY_FILTERS: RegistryFilters = { environmentIds: [], origins: [], insecure: false, outsidePolicy: false, q: '', sort: 'registry' };

/** Registros dos hosts (spec 020 US1): filtros vêm da URL; invalidações `registries` e `images` recarregam a página. */
@Injectable()
export class RegistriesFacade {
  private readonly api = inject(RegistriesApiService);
  private readonly realtime = inject(ConsoleRealtimeService);
  private readonly stageFilter = inject(StageFilterService);
  private readonly directory = inject(EnvironmentDirectoryService);
  private version = 0;
  private refreshTimer: ReturnType<typeof setTimeout> | null = null;

  readonly filters = signal<RegistryFilters>(EMPTY_REGISTRY_FILTERS);
  readonly items = signal<RegistryItem[]>([]);
  readonly summary = signal<RegistryListPage['summary'] | null>(null);
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
      if (invalidation?.kind !== 'registries' && invalidation?.kind !== 'images') return;
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
      const page = await this.api.listRegistries(this.query(this.stageFilter.stage(), this.filters(), cursor));
      if (version !== this.version) return;
      const known = new Set(this.items().map(r => r.registry));
      this.items.update(current => [...current, ...page.registries.filter(r => !known.has(r.registry))]);
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

  private async load(stage: Stage | null, filters: RegistryFilters, silent = false): Promise<void> {
    const version = ++this.version;
    if (!silent) this.loading.set(true);
    try {
      const page = await this.api.listRegistries(this.query(stage, filters));
      if (version !== this.version) return;
      this.items.set(page.registries);
      this.summary.set(page.summary);
      this.nextCursor.set(page.nextCursor ?? null);
      this.updatedAt.set(new Date().toISOString());
      this.error.set(null);
      void this.realtime.watch(this.directory.environments().filter(env => !stage || env.stage === stage).map(env => env.environmentId));
    } catch (error: unknown) {
      if (version === this.version && !silent) this.error.set(toLoadError(error));
    } finally {
      if (version === this.version) this.loading.set(false);
    }
  }

  private query(stage: Stage | null, filters: RegistryFilters, cursor?: string): NonNullable<ListRegistriesData['query']> {
    return {
      ...(stage ? { stage } : {}),
      ...(filters.environmentIds.length ? { environmentId: filters.environmentIds } : {}),
      ...(filters.origins.length ? { origin: filters.origins } : {}),
      ...(filters.insecure ? { insecure: true } : {}),
      ...(filters.outsidePolicy ? { outsidePolicy: true } : {}),
      ...(filters.q.trim().length >= 2 ? { q: filters.q.trim() } : {}),
      sort: filters.sort,
      limit: 50,
      ...(cursor ? { cursor } : {}),
    };
  }
}
