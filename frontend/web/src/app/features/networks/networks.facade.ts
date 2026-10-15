import { DestroyRef, effect, inject, Injectable, signal, untracked } from '@angular/core';
import { ConsoleRealtimeService } from '../../core/console-realtime.service';
import { EnvironmentDirectoryService } from '../../core/environment-directory.service';
import type { ListNetworksData, NetworkItem, NetworkListPage, Stage } from '../../data-access/generated/control-plane';
import { StageFilterService } from '../../shell/stage-filter.service';
import { toLoadError, type LoadError } from '../overview/overview.facade';
import { NetworksApiService } from './networks-api.service';

export type NetworkSort = NonNullable<NonNullable<ListNetworksData['query']>['sort']>;

export interface NetworkFilters {
  environmentIds: string[];
  engines: ('docker' | 'podman')[];
  drivers: string[];
  unused: boolean;
  /** `true` só do sistema, `false` sem as do sistema, `null` todas. */
  system: boolean | null;
  q: string;
  sort: NetworkSort;
}

export const EMPTY_NETWORK_FILTERS: NetworkFilters = { environmentIds: [], engines: [], drivers: [], unused: false, system: null, q: '', sort: 'name' };

export function networkKey(item: Pick<NetworkItem, 'environmentId' | 'networkId'>): string {
  return `${item.environmentId}/${item.networkId}`;
}

/** Inventário de redes (spec 019 US1): filtros vêm da URL; invalidação `networks` recarrega a página. */
@Injectable()
export class NetworksFacade {
  private readonly api = inject(NetworksApiService);
  private readonly realtime = inject(ConsoleRealtimeService);
  private readonly stageFilter = inject(StageFilterService);
  private readonly directory = inject(EnvironmentDirectoryService);
  private version = 0;
  private refreshTimer: ReturnType<typeof setTimeout> | null = null;

  readonly filters = signal<NetworkFilters>(EMPTY_NETWORK_FILTERS);
  readonly items = signal<NetworkItem[]>([]);
  readonly summary = signal<NetworkListPage['summary'] | null>(null);
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
      if (invalidation?.kind !== 'networks') return;
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
      const page = await this.api.listNetworks(this.query(this.stageFilter.stage(), this.filters(), cursor));
      if (version !== this.version) return;
      const known = new Set(this.items().map(networkKey));
      this.items.update(current => [...current, ...page.networks.filter(n => !known.has(networkKey(n)))]);
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

  private async load(stage: Stage | null, filters: NetworkFilters, silent = false): Promise<void> {
    const version = ++this.version;
    if (!silent) this.loading.set(true);
    try {
      const page = await this.api.listNetworks(this.query(stage, filters));
      if (version !== this.version) return;
      this.items.set(page.networks);
      this.summary.set(page.summary);
      this.nextCursor.set(page.nextCursor ?? null);
      this.updatedAt.set(new Date().toISOString());
      this.error.set(null);
      // Assina também os hosts sem redes na página, para ver a primeira rede criada fora do Dokpod (SC-002).
      const hosts = this.directory.environments().filter(env => !stage || env.stage === stage).map(env => env.environmentId);
      void this.realtime.watch([...new Set([...page.networks.map(n => n.environmentId), ...hosts])]);
    } catch (error: unknown) {
      if (version === this.version && !silent) this.error.set(toLoadError(error));
    } finally {
      if (version === this.version) this.loading.set(false);
    }
  }

  private query(stage: Stage | null, filters: NetworkFilters, cursor?: string): NonNullable<ListNetworksData['query']> {
    return {
      ...(stage ? { stage } : {}),
      ...(filters.environmentIds.length ? { environmentId: filters.environmentIds } : {}),
      ...(filters.engines.length ? { engine: filters.engines } : {}),
      ...(filters.unused ? { unused: true } : {}),
      ...(filters.drivers.length ? { driver: filters.drivers } : {}),
      ...(filters.system !== null ? { system: filters.system } : {}),
      ...(filters.q.trim().length >= 2 ? { q: filters.q.trim() } : {}),
      sort: filters.sort,
      limit: 50,
      ...(cursor ? { cursor } : {}),
    };
  }
}
