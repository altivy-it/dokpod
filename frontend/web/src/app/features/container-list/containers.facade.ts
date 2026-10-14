import { DestroyRef, effect, inject, Injectable, signal, untracked } from '@angular/core';
import { ConsoleRealtimeService } from '../../core/console-realtime.service';
import { ControlPlaneApiService } from '../../data-access/control-plane-api.service';
import type {
  ContainerDisplayStatus,
  ContainerListItem,
  ContainerListPage,
  ListContainersData,
  Stage,
} from '../../data-access/generated/control-plane';
import { StageFilterService } from '../../shell/stage-filter.service';
import { toLoadError, type LoadError } from '../overview/overview.facade';

export type ServerSort = NonNullable<NonNullable<ListContainersData['query']>['sort']>;

export interface ContainerFilters {
  environmentIds: string[];
  engines: ('docker' | 'podman')[];
  statuses: ContainerDisplayStatus[];
  q: string;
  sort: ServerSort;
}

export const EMPTY_FILTERS: ContainerFilters = { environmentIds: [], engines: [], statuses: [], q: '', sort: 'name' };

export function rowKey(item: Pick<ContainerListItem, 'environmentId' | 'containerId'>): string {
  return `${item.environmentId}/${item.containerId}`;
}

/**
 * Lista unificada (spec 010 US1): filtros e estágio vêm da URL; invalidações atualizam as linhas no lugar,
 * sem reordenar, e novas linhas só aparecem como aviso até o usuário atualizar.
 */
@Injectable()
export class ContainersFacade {
  private readonly api = inject(ControlPlaneApiService);
  private readonly realtime = inject(ConsoleRealtimeService);
  private readonly stageFilter = inject(StageFilterService);
  private version = 0;
  private refreshTimer: ReturnType<typeof setTimeout> | null = null;

  readonly filters = signal<ContainerFilters>(EMPTY_FILTERS);
  readonly items = signal<ContainerListItem[]>([]);
  readonly summary = signal<ContainerListPage['summary'] | null>(null);
  readonly observedAt = signal<string | null>(null);
  readonly nextCursor = signal<string | null>(null);
  readonly loading = signal(true);
  readonly loadingMore = signal(false);
  readonly error = signal<LoadError | null>(null);
  readonly newRows = signal(0);

  constructor() {
    effect(() => {
      const stage = this.stageFilter.stage();
      const filters = this.filters();
      untracked(() => void this.load(stage, filters));
    });
    effect(() => {
      const invalidation = this.realtime.lastInvalidation();
      if (invalidation?.kind !== 'inventory') return;
      untracked(() => this.scheduleRefresh());
    });
    inject(DestroyRef).onDestroy(() => {
      this.version++;
      if (this.refreshTimer) clearTimeout(this.refreshTimer);
    });
  }

  reload(): Promise<void> {
    this.newRows.set(0);
    return this.load(this.stageFilter.stage(), this.filters());
  }

  async loadMore(): Promise<void> {
    const cursor = this.nextCursor();
    if (!cursor || this.loadingMore()) return;
    const version = this.version;
    this.loadingMore.set(true);
    try {
      const page = await this.api.listAllContainers(this.query(this.stageFilter.stage(), this.filters(), cursor));
      if (version !== this.version) return;
      const known = new Set(this.items().map(rowKey));
      this.items.update(current => [...current, ...page.containers.filter(c => !known.has(rowKey(c)))]);
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
      void this.refreshInPlace();
    }, 1000);
  }

  /** Atualiza as linhas carregadas no lugar; não insere nem reordena (FR-009). */
  private async refreshInPlace(): Promise<void> {
    const version = this.version;
    const loadedCount = this.items().length;
    try {
      const page = await this.api.listAllContainers({ ...this.query(this.stageFilter.stage(), this.filters()), limit: Math.min(100, Math.max(50, loadedCount)) });
      if (version !== this.version) return;
      const fresh = new Map(page.containers.map(c => [rowKey(c), c]));
      const current = this.items();
      const currentKeys = new Set(current.map(rowKey));
      // Página completa (sem cursor): linhas ausentes foram removidas no engine; senão, preserva.
      const complete = !page.nextCursor;
      this.items.set(current.flatMap(item => {
        const updated = fresh.get(rowKey(item));
        return updated ? [updated] : complete ? [] : [item];
      }));
      this.newRows.set(page.containers.filter(c => !currentKeys.has(rowKey(c))).length);
      this.summary.set(page.summary);
      this.observedAt.set(page.observedAt ?? null);
    } catch {
      // Mantém os dados atuais; a próxima invalidação tenta novamente.
    }
  }

  private async load(stage: Stage | null, filters: ContainerFilters): Promise<void> {
    const version = ++this.version;
    this.loading.set(true);
    try {
      const page = await this.api.listAllContainers(this.query(stage, filters));
      if (version !== this.version) return;
      this.items.set(page.containers);
      this.summary.set(page.summary);
      this.observedAt.set(page.observedAt ?? null);
      this.nextCursor.set(page.nextCursor ?? null);
      this.error.set(null);
      this.newRows.set(0);
      void this.realtime.watch([...new Set(page.containers.map(c => c.environmentId))]);
    } catch (error: unknown) {
      if (version === this.version) this.error.set(toLoadError(error));
    } finally {
      if (version === this.version) this.loading.set(false);
    }
  }

  private query(stage: Stage | null, filters: ContainerFilters, cursor?: string): NonNullable<ListContainersData['query']> {
    return {
      ...(stage ? { stage } : {}),
      ...(filters.environmentIds.length ? { environmentId: filters.environmentIds } : {}),
      ...(filters.engines.length ? { engine: filters.engines } : {}),
      ...(filters.statuses.length ? { displayStatus: filters.statuses } : {}),
      ...(filters.q.trim().length >= 2 ? { q: filters.q.trim() } : {}),
      sort: filters.sort,
      limit: 50,
      ...(cursor ? { cursor } : {}),
    };
  }
}
