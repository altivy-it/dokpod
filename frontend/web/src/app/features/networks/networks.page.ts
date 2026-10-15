import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { Router } from '@angular/router';
import { EnvironmentDirectoryService } from '../../core/environment-directory.service';
import { ENGINE_LABEL } from '../../data-access/console-labels';
import type { NetworkItem } from '../../data-access/generated/control-plane';
import {
  DokButton,
  DokDataGrid,
  DokEmptyState,
  DokIcon,
  DokStatusChip,
  formatDateTime,
  type GridColumn,
  type GridFacet,
  type GridRowAction,
  type GridSort,
  ToastService,
} from '../../design-system';
import { ShellContextService } from '../../shell/shell-context.service';
import { StageFilterService, stageLabel } from '../../shell/stage-filter.service';
import { NetworkDetailDrawer } from './network-detail.drawer';
import { evaluateNetworkEligibility, NetworkRemovalService, networkFailureText, type NetworkEligibility } from './network-removal.service';
import { NetworksApiService } from './networks-api.service';
import { NetworksFacade, networkKey, type NetworkFilters, type NetworkSort } from './networks.facade';
import { RemoveNetworksDrawer } from './remove-networks.drawer';

interface NetworkRow {
  id: string;
  name: string;
  usage: string;
  containers: string;
  driver: string;
  subnets: string;
  host: string;
  engine: string;
  created: string | null;
  item: NetworkItem;
}

const SERVER_SORT: Record<string, [NetworkSort, NetworkSort]> = {
  name: ['name', '-name'],
  created: ['createdAt', '-createdAt'],
  host: ['environment', 'environment'],
};

const USAGE_TONES: Record<string, string> = { 'Em uso': 'success', 'Sem uso': 'warning', 'Do sistema': 'secondary' };
const DRIVERS = ['bridge', 'overlay', 'macvlan', 'ipvlan', 'host', 'null'];

function asArray(value: string | string[] | undefined): string[] {
  return value === undefined ? [] : Array.isArray(value) ? value : [value];
}

export function networkUsage(item: Pick<NetworkItem, 'system' | 'inUse'>): string {
  return item.system ? 'Do sistema' : item.inUse ? 'Em uso' : 'Sem uso';
}

/** Operação → Redes (spec 019): inventário por host, detalhe e remoção de redes sem uso. */
@Component({
  selector: 'app-networks-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DokButton, DokDataGrid, DokEmptyState, DokIcon, DokStatusChip, NetworkDetailDrawer, RemoveNetworksDrawer],
  providers: [NetworksFacade],
  styleUrl: './networks.page.css',
  templateUrl: './networks.page.html',
})
export class NetworksPage {
  readonly environmentId = input<string | string[] | undefined>();
  readonly engine = input<string | string[] | undefined>();
  readonly driver = input<string | string[] | undefined>();
  readonly semUso = input<string | undefined>();
  readonly sistema = input<string | undefined>();
  readonly q = input<string | undefined>();
  readonly sort = input<string | undefined>();
  readonly rede = input<string | undefined>();
  readonly redeHost = input<string | undefined>();

  protected readonly facade = inject(NetworksFacade);
  protected readonly removal = inject(NetworkRemovalService);
  protected readonly stageFilter = inject(StageFilterService);
  private readonly directory = inject(EnvironmentDirectoryService);
  private readonly api = inject(NetworksApiService);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);

  protected readonly search = signal('');
  protected readonly gridSort = signal<GridSort | null>(null);
  protected readonly selection = signal<string[]>([]);
  protected readonly pending = signal<NetworkEligibility | null>(null);
  protected readonly submitting = signal(false);
  protected readonly detail = signal<NetworkItem | null>(null);
  protected readonly detailError = signal<string | null>(null);
  protected readonly tones = USAGE_TONES;
  protected readonly failureText = networkFailureText;

  protected readonly columns: GridColumn<NetworkRow>[] = [
    { field: 'name', header: 'Rede', width: 220, key: true, locked: true, mono: true },
    { field: 'usage', header: 'Uso', type: 'status', width: 120, sortable: false, filterable: true },
    { field: 'containers', header: 'Containers', width: 200, sortable: false },
    { field: 'driver', header: 'Driver', width: 100, sortable: false, filterable: true, mono: true },
    { field: 'subnets', header: 'Sub-redes', width: 170, sortable: false, mono: true },
    { field: 'host', header: 'Host', width: 150, filterable: true, groupable: true },
    { field: 'engine', header: 'Engine', width: 100, sortable: false, filterable: true, groupable: true },
    { field: 'created', header: 'Criada', type: 'datetime', width: 150 },
  ];

  protected readonly rows = computed<NetworkRow[]>(() =>
    this.facade.items().map(item => ({
      id: networkKey(item),
      name: item.name,
      usage: networkUsage(item),
      containers: item.containers.length ? item.containers.map(c => c.containerName || c.containerId.slice(0, 12)).join(', ') : '—',
      driver: item.driver || '—',
      subnets: item.subnets.map(s => s.subnet).join(', ') || '—',
      host: item.environmentName,
      engine: item.engine ? (ENGINE_LABEL[item.engine] ?? item.engine) : '—',
      created: item.createdAt ?? null,
      item,
    })),
  );

  private readonly stageHosts = computed(() =>
    this.directory.environments().filter(env => !this.stageFilter.stage() || env.stage === this.stageFilter.stage()),
  );

  protected readonly facets = computed<Record<string, GridFacet[]>>(() => ({
    usage: [
      { value: 'unused', label: 'Sem uso', count: this.facade.summary()?.unused },
      { value: 'system', label: 'Do sistema' },
      { value: 'user', label: 'Fora do sistema' },
    ],
    driver: DRIVERS.map(d => ({ value: d, label: d })),
    engine: [
      { value: 'docker', label: 'Docker' },
      { value: 'podman', label: 'Podman' },
    ],
    host: this.stageHosts().map(env => ({ value: env.environmentId, label: env.name })),
  }));

  protected readonly gridFilters = computed<Record<string, string[]>>(() => {
    const f = this.facade.filters();
    const usage = [...(f.unused ? ['unused'] : []), ...(f.system === true ? ['system'] : f.system === false ? ['user'] : [])];
    return { usage, driver: f.drivers, engine: f.engines, host: f.environmentIds };
  });

  protected readonly filterChips = computed(() => {
    const f = this.facade.filters();
    const chips: { key: 'engine' | 'stage' | 'host' | 'unused' | 'system' | 'driver'; field: string; label: string }[] = [];
    if (f.unused) chips.push({ key: 'unused', field: 'Uso', label: 'Sem uso' });
    if (f.system !== null) chips.push({ key: 'system', field: 'Tipo', label: f.system ? 'Do sistema' : 'Fora do sistema' });
    if (f.drivers.length) chips.push({ key: 'driver', field: 'Driver', label: f.drivers.join(', ') });
    if (f.engines.length) chips.push({ key: 'engine', field: 'Engine', label: f.engines.map(e => ENGINE_LABEL[e] ?? e).join(', ') });
    if (this.stageFilter.stage()) chips.push({ key: 'stage', field: 'Ambiente', label: this.stageName() });
    if (f.environmentIds.length) chips.push({ key: 'host', field: 'Host', label: f.environmentIds.map(id => this.directory.byId(id)?.name ?? id.slice(0, 8)).join(', ') });
    return chips;
  });

  /** FR-010: hosts sem inventário de redes (agente anterior ou ainda sem snapshot). */
  protected readonly unavailableHosts = computed(() => (this.facade.summary()?.unavailableHosts ?? []).map(h => h.environmentName).join(', '));
  protected readonly selectedItems = computed(() => {
    const selected = new Set(this.selection());
    return this.facade.items().filter(item => selected.has(networkKey(item)));
  });
  protected readonly batchSummary = computed(() => this.removal.summary());
  protected readonly stageName = computed(() => stageLabel(this.stageFilter.stage()));
  protected readonly updatedAt = computed(() => (this.facade.updatedAt() ? formatDateTime(this.facade.updatedAt()) : null));
  protected readonly exportName = computed(() => `redes-${(this.stageFilter.stage() ? this.stageName() : 'todos').toLowerCase().replace(/[^a-z0-9]+/g, '-')}`);

  constructor() {
    inject(ShellContextService).set('Redes', ['Operação', 'Redes']);
    void this.directory.load();
    effect(() => {
      const sistema = this.sistema();
      const filters: NetworkFilters = {
        environmentIds: asArray(this.environmentId()),
        engines: asArray(this.engine()).filter((e): e is 'docker' | 'podman' => e === 'docker' || e === 'podman'),
        drivers: asArray(this.driver()).filter(d => /^[a-z0-9][a-z0-9_.:/-]{0,63}$/.test(d)),
        unused: this.semUso() === '1' || this.semUso() === 'true',
        system: sistema === '1' ? true : sistema === '0' ? false : null,
        q: this.q() ?? '',
        sort: (this.sort() as NetworkSort | undefined) ?? 'name',
      };
      untracked(() => {
        if (JSON.stringify(this.facade.filters()) !== JSON.stringify(filters)) this.facade.filters.set(filters);
        this.search.set(filters.q);
      });
    });
    effect(() => {
      const search = this.search();
      untracked(() => {
        if (search !== (this.q() ?? '') && (search.length === 0 || search.trim().length >= 2)) this.navigate({ q: search || null });
      });
    });
    effect(() => {
      const sort = this.gridSort();
      untracked(() => {
        const mapped = sort ? SERVER_SORT[sort.field]?.[sort.dir === 'asc' ? 0 : 1] : 'name';
        if (mapped && mapped !== (this.sort() ?? 'name')) this.navigate({ sort: mapped === 'name' ? null : mapped });
      });
    });
    // Seleção sobrevive a recargas só para redes ainda listadas.
    effect(() => {
      const keys = new Set(this.facade.items().map(networkKey));
      untracked(() => {
        const kept = this.selection().filter(key => keys.has(key));
        if (kept.length !== this.selection().length) this.selection.set(kept);
      });
    });
    // Detalhe pela URL (link do detalhe do container): `?redeHost=<host>&rede=<identificador ou nome>`.
    effect(() => {
      const network = this.rede();
      const host = this.redeHost() ?? null;
      untracked(() => void this.openDetail(network, host));
    });
    // Ao terminar um lote, a lista reflete as remoções mesmo sem invalidação em tempo real.
    effect(() => {
      const running = this.removal.running();
      untracked(() => {
        if (!running && this.removal.batch()) void this.facade.reload();
      });
    });
  }

  protected readonly rowActionsFor = (row: NetworkRow): GridRowAction[] => {
    const actions: GridRowAction[] = [{ id: 'detail', label: 'Ver detalhe', icon: 'info' }];
    if (row.item.canRemove) actions.push({ id: 'remove', label: 'Remover rede', icon: 'trash', danger: true });
    return actions;
  };
  protected readonly rowLabel = (row: NetworkRow) => `${row.item.name} em ${row.host}`;

  protected onRowAction(event: { action: string; row: NetworkRow }): void {
    if (event.action === 'remove') this.prepareRemoval([event.row.item]);
    else this.showDetail(event.row.item);
  }

  protected showDetail(item: NetworkItem): void {
    this.detail.set(item);
    this.navigate({ rede: item.networkId, redeHost: item.environmentId });
  }

  protected closeDetail(): void {
    this.detail.set(null);
    this.detailError.set(null);
    this.navigate({ rede: null, redeHost: null });
  }

  protected prepareSelected(): void {
    const items = this.selectedItems();
    if (items.length === 0) {
      this.toast.notify('info', { message: 'Selecione redes na lista. Dica: filtre "sem uso" e selecione todas.' });
      return;
    }
    this.prepareRemoval(items);
  }

  protected prepareRemoval(items: NetworkItem[]): void {
    if (this.removal.running()) {
      this.toast.notify('info', { message: 'Aguarde a remoção em andamento terminar.' });
      return;
    }
    this.pending.set(evaluateNetworkEligibility(items));
  }

  protected async confirm(justification: string | null): Promise<void> {
    const pending = this.pending();
    if (!pending) return;
    this.submitting.set(true);
    try {
      await this.removal.run(pending.eligible, justification);
      this.pending.set(null);
      this.selection.set([]);
      if (this.detail()) this.closeDetail();
    } catch (error: unknown) {
      this.toast.error(error instanceof Error ? error.message : 'Não foi possível enviar os comandos.');
    } finally {
      this.submitting.set(false);
    }
  }

  protected onGridFilters(filters: Record<string, string[]>): void {
    const toParam = (values: string[] | undefined) => (values && values.length ? values : null);
    const usage = filters['usage'] ?? [];
    const system = usage.includes('system') && !usage.includes('user') ? '1' : usage.includes('user') && !usage.includes('system') ? '0' : null;
    this.navigate({
      semUso: usage.includes('unused') ? '1' : null,
      sistema: system,
      driver: toParam(filters['driver']),
      engine: toParam(filters['engine']),
      environmentId: toParam(filters['host']),
    });
  }

  protected toggleUnused(): void {
    this.navigate({ semUso: this.facade.filters().unused ? null : '1' });
  }

  protected removeFilter(key: 'engine' | 'stage' | 'host' | 'unused' | 'system' | 'driver'): void {
    if (key === 'stage') this.stageFilter.select(null);
    else this.navigate({ [{ host: 'environmentId', unused: 'semUso', system: 'sistema', driver: 'driver', engine: 'engine' }[key]]: null });
  }

  private async openDetail(network: string | undefined, environmentId: string | null): Promise<void> {
    if (!network) {
      this.detail.set(null);
      return;
    }
    if (!environmentId) return;
    const current = this.detail();
    if (current && current.environmentId === environmentId && (current.networkId === network || current.name === network)) return;
    const listed = this.facade.items().find(item => item.environmentId === environmentId && (item.networkId === network || item.name === network));
    if (listed) {
      this.detail.set(listed);
      return;
    }
    try {
      this.detail.set(await this.api.getNetwork(environmentId, network));
      this.detailError.set(null);
    } catch (error: unknown) {
      const status = (error as { status?: number } | null)?.status;
      this.detailError.set(status === 404 ? `A rede ${network} não está no inventário deste host.` : 'Não foi possível carregar a rede.');
    }
  }

  private navigate(params: Record<string, string | string[] | null>): void {
    void this.router.navigate([], { queryParams: params, queryParamsHandling: 'merge', replaceUrl: true });
  }
}
