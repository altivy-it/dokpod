import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { Router } from '@angular/router';
import { EnvironmentDirectoryService } from '../../core/environment-directory.service';
import { ENGINE_LABEL } from '../../data-access/console-labels';
import type { VolumeItem } from '../../data-access/generated/control-plane';
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
import { RemoveVolumesDrawer } from './remove-volumes.drawer';
import { VolumeDetailDrawer } from './volume-detail.drawer';
import { evaluateVolumeEligibility, VolumeRemovalService, volumeFailureText, type VolumeEligibility } from './volume-removal.service';
import { VolumesApiService } from './volumes-api.service';
import { VolumesFacade, volumeKey, type VolumeFilters, type VolumeSort } from './volumes.facade';

interface VolumeRow {
  id: string;
  name: string;
  usage: string;
  containers: string;
  kind: string;
  host: string;
  engine: string;
  driver: string;
  created: string | null;
  item: VolumeItem;
}

const SERVER_SORT: Record<string, [VolumeSort, VolumeSort]> = {
  name: ['name', '-name'],
  created: ['createdAt', '-createdAt'],
  host: ['environment', 'environment'],
};

const USAGE_TONES: Record<string, string> = { 'Em uso': 'success', 'Sem uso': 'warning' };

function asArray(value: string | string[] | undefined): string[] {
  return value === undefined ? [] : Array.isArray(value) ? value : [value];
}

/** Nome exibido: volumes anônimos (64 hex) aparecem abreviados; o nome completo fica no detalhe. */
export function volumeDisplayName(item: Pick<VolumeItem, 'name' | 'anonymous'>): string {
  return item.anonymous ? `${item.name.slice(0, 12)}… (anônimo)` : item.name;
}

/** Operação → Volumes (spec 018): inventário por host, detalhe e remoção de volumes sem uso. */
@Component({
  selector: 'app-volumes-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DokButton, DokDataGrid, DokEmptyState, DokIcon, DokStatusChip, RemoveVolumesDrawer, VolumeDetailDrawer],
  providers: [VolumesFacade],
  styleUrl: './volumes.page.css',
  templateUrl: './volumes.page.html',
})
export class VolumesPage {
  readonly environmentId = input<string | string[] | undefined>();
  readonly engine = input<string | string[] | undefined>();
  readonly semUso = input<string | undefined>();
  readonly anonimo = input<string | undefined>();
  readonly q = input<string | undefined>();
  readonly sort = input<string | undefined>();
  readonly volume = input<string | undefined>();
  readonly volumeHost = input<string | undefined>();

  protected readonly facade = inject(VolumesFacade);
  protected readonly removal = inject(VolumeRemovalService);
  protected readonly stageFilter = inject(StageFilterService);
  private readonly directory = inject(EnvironmentDirectoryService);
  private readonly api = inject(VolumesApiService);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);

  protected readonly search = signal('');
  protected readonly gridSort = signal<GridSort | null>(null);
  protected readonly selection = signal<string[]>([]);
  protected readonly pending = signal<VolumeEligibility | null>(null);
  protected readonly submitting = signal(false);
  protected readonly detail = signal<VolumeItem | null>(null);
  protected readonly detailError = signal<string | null>(null);
  protected readonly tones = USAGE_TONES;
  protected readonly failureText = volumeFailureText;

  protected readonly columns: GridColumn<VolumeRow>[] = [
    { field: 'name', header: 'Volume', width: 260, key: true, locked: true, mono: true },
    { field: 'usage', header: 'Uso', type: 'status', width: 110, sortable: false, filterable: true },
    { field: 'containers', header: 'Containers', width: 220, sortable: false },
    { field: 'kind', header: 'Tipo', width: 110, sortable: false, filterable: true },
    { field: 'host', header: 'Host', width: 150, filterable: true, groupable: true },
    { field: 'engine', header: 'Engine', width: 100, sortable: false, filterable: true, groupable: true },
    { field: 'driver', header: 'Driver', width: 100, sortable: false, mono: true },
    { field: 'created', header: 'Criado', type: 'datetime', width: 150 },
  ];

  protected readonly rows = computed<VolumeRow[]>(() =>
    this.facade.items().map(item => ({
      id: volumeKey(item),
      name: volumeDisplayName(item),
      usage: item.inUse ? 'Em uso' : 'Sem uso',
      containers: item.mounts.length ? item.mounts.map(m => m.containerName || m.containerId.slice(0, 12)).join(', ') : '—',
      kind: item.anonymous ? 'Anônimo' : 'Nomeado',
      host: item.environmentName,
      engine: item.engine ? (ENGINE_LABEL[item.engine] ?? item.engine) : '—',
      driver: item.driver || '—',
      created: item.createdAt ?? null,
      item,
    })),
  );

  private readonly stageHosts = computed(() =>
    this.directory.environments().filter(env => !this.stageFilter.stage() || env.stage === this.stageFilter.stage()),
  );

  protected readonly facets = computed<Record<string, GridFacet[]>>(() => ({
    usage: [{ value: 'unused', label: 'Sem uso', count: this.facade.summary()?.unused }],
    kind: [
      { value: 'named', label: 'Nomeado' },
      { value: 'anonymous', label: 'Anônimo' },
    ],
    engine: [
      { value: 'docker', label: 'Docker' },
      { value: 'podman', label: 'Podman' },
    ],
    host: this.stageHosts().map(env => ({ value: env.environmentId, label: env.name })),
  }));

  protected readonly gridFilters = computed<Record<string, string[]>>(() => {
    const f = this.facade.filters();
    return {
      usage: f.unused ? ['unused'] : [],
      kind: f.anonymous === null ? [] : [f.anonymous ? 'anonymous' : 'named'],
      engine: f.engines,
      host: f.environmentIds,
    };
  });

  protected readonly filterChips = computed(() => {
    const f = this.facade.filters();
    const chips: { key: 'engine' | 'stage' | 'host' | 'unused' | 'kind'; field: string; label: string }[] = [];
    if (f.unused) chips.push({ key: 'unused', field: 'Uso', label: 'Sem uso' });
    if (f.anonymous !== null) chips.push({ key: 'kind', field: 'Tipo', label: f.anonymous ? 'Anônimo' : 'Nomeado' });
    if (f.engines.length) chips.push({ key: 'engine', field: 'Engine', label: f.engines.map(e => ENGINE_LABEL[e] ?? e).join(', ') });
    if (this.stageFilter.stage()) chips.push({ key: 'stage', field: 'Ambiente', label: this.stageName() });
    if (f.environmentIds.length) chips.push({ key: 'host', field: 'Host', label: f.environmentIds.map(id => this.directory.byId(id)?.name ?? id.slice(0, 8)).join(', ') });
    return chips;
  });

  /** FR-010: hosts sem inventário de volumes (agente anterior ou ainda sem snapshot). */
  protected readonly unavailableHosts = computed(() => (this.facade.summary()?.unavailableHosts ?? []).map(h => h.environmentName).join(', '));
  protected readonly selectedItems = computed(() => {
    const selected = new Set(this.selection());
    return this.facade.items().filter(item => selected.has(volumeKey(item)));
  });
  protected readonly batchSummary = computed(() => this.removal.summary());
  protected readonly stageName = computed(() => stageLabel(this.stageFilter.stage()));
  protected readonly updatedAt = computed(() => (this.facade.updatedAt() ? formatDateTime(this.facade.updatedAt()) : null));
  protected readonly exportName = computed(() => `volumes-${(this.stageFilter.stage() ? this.stageName() : 'todos').toLowerCase().replace(/[^a-z0-9]+/g, '-')}`);

  constructor() {
    inject(ShellContextService).set('Volumes', ['Operação', 'Volumes']);
    void this.directory.load();
    effect(() => {
      const anonimo = this.anonimo();
      const filters: VolumeFilters = {
        environmentIds: asArray(this.environmentId()),
        engines: asArray(this.engine()).filter((e): e is 'docker' | 'podman' => e === 'docker' || e === 'podman'),
        unused: this.semUso() === '1' || this.semUso() === 'true',
        anonymous: anonimo === '1' ? true : anonimo === '0' ? false : null,
        q: this.q() ?? '',
        sort: (this.sort() as VolumeSort | undefined) ?? 'name',
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
    // Seleção sobrevive a recargas só para volumes ainda listados.
    effect(() => {
      const keys = new Set(this.facade.items().map(volumeKey));
      untracked(() => {
        const kept = this.selection().filter(key => keys.has(key));
        if (kept.length !== this.selection().length) this.selection.set(kept);
      });
    });
    // Detalhe pela URL (link do detalhe do container): `?volumeHost=<host>&volume=<nome>`.
    effect(() => {
      const name = this.volume();
      const host = this.volumeHost() ?? null;
      untracked(() => void this.openDetail(name, host));
    });
    // Ao terminar um lote, a lista reflete as remoções mesmo sem invalidação em tempo real.
    effect(() => {
      const running = this.removal.running();
      untracked(() => {
        if (!running && this.removal.batch()) void this.facade.reload();
      });
    });
  }

  protected readonly rowActionsFor = (row: VolumeRow): GridRowAction[] => {
    const actions: GridRowAction[] = [{ id: 'detail', label: 'Ver detalhe', icon: 'info' }];
    if (row.item.canRemove) actions.push({ id: 'remove', label: 'Remover volume', icon: 'trash', danger: true });
    return actions;
  };
  protected readonly rowLabel = (row: VolumeRow) => `${row.item.name} em ${row.host}`;

  protected onRowAction(event: { action: string; row: VolumeRow }): void {
    if (event.action === 'remove') this.prepareRemoval([event.row.item]);
    else this.showDetail(event.row.item);
  }

  protected showDetail(item: VolumeItem): void {
    this.detail.set(item);
    this.navigate({ volume: item.name, volumeHost: item.environmentId });
  }

  protected closeDetail(): void {
    this.detail.set(null);
    this.detailError.set(null);
    this.navigate({ volume: null, volumeHost: null });
  }

  protected prepareSelected(): void {
    const items = this.selectedItems();
    if (items.length === 0) {
      this.toast.notify('info', { message: 'Selecione volumes na lista. Dica: filtre "sem uso" e selecione todos.' });
      return;
    }
    this.prepareRemoval(items);
  }

  protected prepareRemoval(items: VolumeItem[]): void {
    if (this.removal.running()) {
      this.toast.notify('info', { message: 'Aguarde a remoção em andamento terminar.' });
      return;
    }
    this.pending.set(evaluateVolumeEligibility(items));
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
    const kind = filters['kind'] ?? [];
    this.navigate({
      semUso: filters['usage']?.includes('unused') ? '1' : null,
      anonimo: kind.length === 1 ? (kind[0] === 'anonymous' ? '1' : '0') : null,
      engine: toParam(filters['engine']),
      environmentId: toParam(filters['host']),
    });
  }

  protected toggleUnused(): void {
    this.navigate({ semUso: this.facade.filters().unused ? null : '1' });
  }

  protected removeFilter(key: 'engine' | 'stage' | 'host' | 'unused' | 'kind'): void {
    if (key === 'stage') this.stageFilter.select(null);
    else this.navigate({ [{ host: 'environmentId', unused: 'semUso', kind: 'anonimo', engine: 'engine' }[key]]: null });
  }

  private async openDetail(name: string | undefined, environmentId: string | null): Promise<void> {
    if (!name) {
      this.detail.set(null);
      return;
    }
    if (!environmentId) return;
    const current = this.detail();
    if (current && current.name === name && current.environmentId === environmentId) return;
    const listed = this.facade.items().find(item => item.name === name && item.environmentId === environmentId);
    if (listed) {
      this.detail.set(listed);
      return;
    }
    try {
      this.detail.set(await this.api.getVolume(environmentId, name));
      this.detailError.set(null);
    } catch (error: unknown) {
      const status = (error as { status?: number } | null)?.status;
      this.detailError.set(status === 404 ? `O volume ${name} não está no inventário deste host.` : 'Não foi possível carregar o volume.');
    }
  }

  private navigate(params: Record<string, string | string[] | null>): void {
    void this.router.navigate([], { queryParams: params, queryParamsHandling: 'merge', replaceUrl: true });
  }
}
