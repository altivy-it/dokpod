import { DestroyRef, effect, inject, Injectable, signal, untracked } from '@angular/core';
import { ConsoleRealtimeService } from '../../core/console-realtime.service';
import { EnvironmentDirectoryService } from '../../core/environment-directory.service';
import type { ListVolumesData, Stage, VolumeItem, VolumeListPage } from '../../data-access/generated/control-plane';
import { StageFilterService } from '../../shell/stage-filter.service';
import { toLoadError, type LoadError } from '../overview/overview.facade';
import { VolumesApiService } from './volumes-api.service';

export type VolumeSort = NonNullable<NonNullable<ListVolumesData['query']>['sort']>;

export interface VolumeFilters {
  environmentIds: string[];
  engines: ('docker' | 'podman')[];
  unused: boolean;
  /** `true` só anônimos, `false` só nomeados, `null` todos. */
  anonymous: boolean | null;
  q: string;
  sort: VolumeSort;
}

export const EMPTY_VOLUME_FILTERS: VolumeFilters = { environmentIds: [], engines: [], unused: false, anonymous: null, q: '', sort: 'name' };

export function volumeKey(item: Pick<VolumeItem, 'environmentId' | 'name'>): string {
  return `${item.environmentId}/${item.name}`;
}

/** Inventário de volumes (spec 018 US1): filtros vêm da URL; invalidação `volumes` recarrega a página. */
@Injectable()
export class VolumesFacade {
  private readonly api = inject(VolumesApiService);
  private readonly realtime = inject(ConsoleRealtimeService);
  private readonly stageFilter = inject(StageFilterService);
  private readonly directory = inject(EnvironmentDirectoryService);
  private version = 0;
  private refreshTimer: ReturnType<typeof setTimeout> | null = null;

  readonly filters = signal<VolumeFilters>(EMPTY_VOLUME_FILTERS);
  readonly items = signal<VolumeItem[]>([]);
  readonly summary = signal<VolumeListPage['summary'] | null>(null);
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
      if (invalidation?.kind !== 'volumes') return;
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
      const page = await this.api.listVolumes(this.query(this.stageFilter.stage(), this.filters(), cursor));
      if (version !== this.version) return;
      const known = new Set(this.items().map(volumeKey));
      this.items.update(current => [...current, ...page.volumes.filter(v => !known.has(volumeKey(v)))]);
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

  private async load(stage: Stage | null, filters: VolumeFilters, silent = false): Promise<void> {
    const version = ++this.version;
    if (!silent) this.loading.set(true);
    try {
      const page = await this.api.listVolumes(this.query(stage, filters));
      if (version !== this.version) return;
      this.items.set(page.volumes);
      this.summary.set(page.summary);
      this.nextCursor.set(page.nextCursor ?? null);
      this.updatedAt.set(new Date().toISOString());
      this.error.set(null);
      // Assina também os hosts sem volumes na página, para ver o primeiro volume criado fora do Dokpod (SC-002).
      const hosts = this.directory.environments().filter(env => !stage || env.stage === stage).map(env => env.environmentId);
      void this.realtime.watch([...new Set([...page.volumes.map(v => v.environmentId), ...hosts])]);
    } catch (error: unknown) {
      if (version === this.version && !silent) this.error.set(toLoadError(error));
    } finally {
      if (version === this.version) this.loading.set(false);
    }
  }

  private query(stage: Stage | null, filters: VolumeFilters, cursor?: string): NonNullable<ListVolumesData['query']> {
    return {
      ...(stage ? { stage } : {}),
      ...(filters.environmentIds.length ? { environmentId: filters.environmentIds } : {}),
      ...(filters.engines.length ? { engine: filters.engines } : {}),
      ...(filters.unused ? { unused: true } : {}),
      ...(filters.anonymous !== null ? { anonymous: filters.anonymous } : {}),
      ...(filters.q.trim().length >= 2 ? { q: filters.q.trim() } : {}),
      sort: filters.sort,
      limit: 50,
      ...(cursor ? { cursor } : {}),
    };
  }
}
