import { DestroyRef, effect, inject, Injectable, signal, untracked } from '@angular/core';
import { ConsoleRealtimeService } from '../../core/console-realtime.service';
import { ControlPlaneApiService } from '../../data-access/control-plane-api.service';
import type { ImageItem, ImageListPage, ListImagesData, Stage } from '../../data-access/generated/control-plane';
import { StageFilterService } from '../../shell/stage-filter.service';
import { toLoadError, type LoadError } from '../overview/overview.facade';

export type ImageSort = NonNullable<NonNullable<ListImagesData['query']>['sort']>;

export interface ImageFilters {
  environmentIds: string[];
  engines: ('docker' | 'podman')[];
  unused: boolean;
  q: string;
  sort: ImageSort;
}

export const EMPTY_IMAGE_FILTERS: ImageFilters = { environmentIds: [], engines: [], unused: false, q: '', sort: 'name' };

export function imageKey(item: Pick<ImageItem, 'environmentId' | 'imageId'>): string {
  return `${item.environmentId}/${item.imageId}`;
}

/** Inventário de imagens (spec 012 US1): filtros vêm da URL; invalidação `images`/`inventory` recarrega a página. */
@Injectable()
export class ImagesFacade {
  private readonly api = inject(ControlPlaneApiService);
  private readonly realtime = inject(ConsoleRealtimeService);
  private readonly stageFilter = inject(StageFilterService);
  private version = 0;
  private refreshTimer: ReturnType<typeof setTimeout> | null = null;

  readonly filters = signal<ImageFilters>(EMPTY_IMAGE_FILTERS);
  readonly items = signal<ImageItem[]>([]);
  readonly summary = signal<ImageListPage['summary'] | null>(null);
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
      if (invalidation?.kind !== 'images' && invalidation?.kind !== 'inventory') return;
      if (!this.items().some(i => i.environmentId === invalidation.environmentId) && this.items().length > 0) return;
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
      const page = await this.api.listImages(this.query(this.stageFilter.stage(), this.filters(), cursor));
      if (version !== this.version) return;
      const known = new Set(this.items().map(imageKey));
      this.items.update(current => [...current, ...page.images.filter(i => !known.has(imageKey(i)))]);
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

  private async load(stage: Stage | null, filters: ImageFilters, silent = false): Promise<void> {
    const version = ++this.version;
    if (!silent) this.loading.set(true);
    try {
      const page = await this.api.listImages(this.query(stage, filters));
      if (version !== this.version) return;
      this.items.set(page.images);
      this.summary.set(page.summary);
      this.nextCursor.set(page.nextCursor ?? null);
      this.updatedAt.set(new Date().toISOString());
      this.error.set(null);
      void this.realtime.watch([...new Set(page.images.map(i => i.environmentId))]);
    } catch (error: unknown) {
      if (version === this.version && !silent) this.error.set(toLoadError(error));
    } finally {
      if (version === this.version) this.loading.set(false);
    }
  }

  private query(stage: Stage | null, filters: ImageFilters, cursor?: string): NonNullable<ListImagesData['query']> {
    return {
      ...(stage ? { stage } : {}),
      ...(filters.environmentIds.length ? { environmentId: filters.environmentIds } : {}),
      ...(filters.engines.length ? { engine: filters.engines } : {}),
      ...(filters.unused ? { unused: true } : {}),
      ...(filters.q.trim().length >= 2 ? { q: filters.q.trim() } : {}),
      sort: filters.sort,
      limit: 50,
      ...(cursor ? { cursor } : {}),
    };
  }
}
