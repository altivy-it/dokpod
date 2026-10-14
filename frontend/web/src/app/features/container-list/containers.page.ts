import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { EnvironmentDirectoryService } from '../../core/environment-directory.service';
import { ConfirmActionDrawer } from '../../core/container-actions/confirm-action.drawer';
import {
  ACTION_LABEL,
  ContainerActionsService,
  evaluateEligibility,
  type Eligibility,
} from '../../core/container-actions/container-actions.service';
import { CONTAINER_STATUS, CONTAINER_STATUS_TONES, ENGINE_LABEL } from '../../data-access/console-labels';
import type { ContainerAction } from '../../data-access/control-plane-api.service';
import type { ContainerDisplayStatus, ContainerListItem } from '../../data-access/generated/control-plane';
import {
  DokButton,
  DokDataGrid,
  DokEmptyState,
  DokEngineBadge,
  DokGridCell,
  DokIcon,
  DokSelect,
  DokStatusChip,
  formatDateTime,
  ToastService,
  type GridColumn,
  type GridRowAction,
  type GridSort,
} from '../../design-system';
import { ShellContextService } from '../../shell/shell-context.service';
import { StageFilterService, stageLabel } from '../../shell/stage-filter.service';
import { ContainersFacade, rowKey, type ContainerFilters, type ServerSort } from './containers.facade';

interface ContainerRow {
  id: string;
  name: string;
  status: string;
  engine: string;
  host: string;
  image: string;
  cpu: number | null;
  mem: number | null;
  ports: string;
  created: string | null;
  item: ContainerListItem;
}

const SERVER_SORT: Record<string, [ServerSort, ServerSort]> = {
  name: ['name', '-name'],
  status: ['displayStatus', 'displayStatus'],
  host: ['environment', 'environment'],
  created: ['createdAt', '-createdAt'],
};

function asArray(value: string | string[] | undefined): string[] {
  return value === undefined ? [] : Array.isArray(value) ? value : [value];
}

/** Lista unificada de containers (board `02 · Containers`, spec 010). */
@Component({
  selector: 'app-containers-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ConfirmActionDrawer,
    DokButton,
    DokDataGrid,
    DokEmptyState,
    DokEngineBadge,
    DokGridCell,
    DokIcon,
    DokSelect,
    DokStatusChip,
    FormsModule,
  ],
  providers: [ContainersFacade],
  styleUrl: './containers.page.css',
  templateUrl: './containers.page.html',
  host: { '(document:mousedown)': 'onDocumentDown($event)' },
})
export class ContainersPage {
  readonly environmentId = input<string | string[] | undefined>();
  readonly engine = input<string | string[] | undefined>();
  readonly status = input<string | string[] | undefined>();
  readonly q = input<string | undefined>();
  readonly sort = input<string | undefined>();

  protected readonly facade = inject(ContainersFacade);
  protected readonly actions = inject(ContainerActionsService);
  protected readonly stageFilter = inject(StageFilterService);
  private readonly directory = inject(EnvironmentDirectoryService);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);

  protected readonly selection = signal<string[]>([]);
  protected readonly search = signal('');
  protected readonly gridSort = signal<GridSort | null>(null);
  protected readonly pending = signal<{ action: ContainerAction; eligibility: Eligibility } | null>(null);
  protected readonly submitting = signal(false);
  protected readonly moreOpen = signal(false);
  protected readonly statusLabels = CONTAINER_STATUS;
  protected readonly tones = CONTAINER_STATUS_TONES;
  protected readonly actionLabels = ACTION_LABEL;
  protected readonly statusOrder: ContainerDisplayStatus[] = ['running', 'starting', 'restarting', 'failed', 'paused', 'stopped'];

  protected readonly columns: GridColumn<ContainerRow>[] = [
    { field: 'name', header: 'Nome', width: 190, key: true },
    { field: 'status', header: 'Situação', type: 'status', width: 140 },
    { field: 'engine', header: 'Engine', width: 110, sortable: false },
    { field: 'host', header: 'Host', width: 140 },
    { field: 'image', header: 'Imagem', width: 320, mono: true, sortable: false },
    { field: 'cpu', header: 'CPU %', type: 'number', decimals: 1, width: 84, sortable: false },
    { field: 'mem', header: 'Memória MiB', type: 'number', width: 116, sortable: false },
    { field: 'ports', header: 'Portas', width: 170, mono: true, sortable: false },
    { field: 'created', header: 'Criado em', width: 150, type: 'datetime' },
  ];

  protected readonly rows = computed<ContainerRow[]>(() =>
    this.facade.items().map(item => ({
      id: rowKey(item),
      name: item.name,
      status: CONTAINER_STATUS[item.displayStatus ?? 'stopped'].label,
      engine: item.engine ? (ENGINE_LABEL[item.engine] ?? item.engine) : '—',
      host: item.environmentName,
      image: item.imageReference,
      cpu: item.resources?.cpuPercent ?? null,
      mem: item.resources?.memoryUsedBytes != null ? item.resources.memoryUsedBytes / (1024 * 1024) : null,
      ports: formatPorts(item),
      created: item.createdAt ?? null,
      item,
    })),
  );

  protected readonly selectedItems = computed(() => {
    const selected = new Set(this.selection());
    return this.facade.items().filter(item => selected.has(rowKey(item)));
  });

  protected readonly summaryChips = computed(() => {
    const counts = this.facade.summary()?.byDisplayStatus ?? {};
    return this.statusOrder
      .filter(status => (counts[status] ?? 0) > 0)
      .map(status => ({ status, count: counts[status] ?? 0, ...CONTAINER_STATUS[status] }));
  });

  protected readonly hostOptions = computed(() => [
    { value: '', label: 'Todos os hosts' },
    ...this.directory
      .environments()
      .filter(env => !this.stageFilter.stage() || env.stage === this.stageFilter.stage())
      .map(env => ({ value: env.environmentId, label: env.name, caption: env.agent?.engine ? ENGINE_LABEL[env.agent.engine] : undefined })),
  ]);
  protected readonly engineOptions = [
    { value: '', label: 'Docker e Podman' },
    { value: 'docker', label: 'Docker', dot: 'var(--chart-2)' },
    { value: 'podman', label: 'Podman', dot: 'var(--chart-5)' },
  ];
  protected readonly stageName = computed(() => stageLabel(this.stageFilter.stage()));
  protected readonly updatedAt = computed(() => (this.facade.observedAt() ? formatDateTime(this.facade.observedAt()) : null));
  protected readonly hostFilterName = computed(() => {
    const ids = this.facade.filters().environmentIds;
    return ids.map(id => this.directory.byId(id)?.name ?? id.slice(0, 8)).join(', ');
  });
  protected readonly batchSummary = computed(() => this.actions.summary());
  protected readonly selectedHost = computed(() => this.facade.filters().environmentIds[0] ?? '');
  protected readonly selectedEngine = computed(() => this.facade.filters().engines[0] ?? '');

  constructor() {
    inject(ShellContextService).set('Containers', ['Operação', 'Containers']);
    effect(() => {
      const filters: ContainerFilters = {
        environmentIds: asArray(this.environmentId()),
        engines: asArray(this.engine()).filter((e): e is 'docker' | 'podman' => e === 'docker' || e === 'podman'),
        statuses: asArray(this.status()).filter((s): s is ContainerDisplayStatus => s in CONTAINER_STATUS),
        q: this.q() ?? '',
        sort: (this.sort() as ServerSort | undefined) ?? 'name',
      };
      untracked(() => {
        const current = this.facade.filters();
        if (JSON.stringify(current) !== JSON.stringify(filters)) this.facade.filters.set(filters);
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
    effect(() => {
      // Seleção sobrevive a atualizações: descarta só chaves que saíram da lista.
      const keys = new Set(this.facade.items().map(rowKey));
      untracked(() => {
        const kept = this.selection().filter(key => keys.has(key));
        if (kept.length !== this.selection().length) this.selection.set(kept);
      });
    });
  }

  protected readonly rowActionsFor = (row: ContainerRow): GridRowAction[] => {
    const allowed = new Set(row.item.allowedActions);
    const actions: GridRowAction[] = [
      { id: 'logs', label: 'Ver logs', icon: 'list' },
      { id: 'terminal', label: 'Abrir terminal', icon: 'command' },
      { id: 'inspect', label: 'Inspecionar', icon: 'eye' },
    ];
    for (const action of ['start', 'restart', 'stop', 'pause', 'unpause', 'delete'] as ContainerAction[]) {
      if (allowed.has(action)) actions.push({ id: action, label: ACTION_LABEL[action].verb, icon: ACTION_LABEL[action].icon, danger: action === 'delete' });
    }
    return actions;
  };
  protected readonly rowLabel = (row: ContainerRow) => `${row.name} em ${row.host}`;

  protected onRowAction(event: { action: string; row: ContainerRow }): void {
    const tab = { logs: 'logs', terminal: 'terminal', inspect: 'inspecionar' }[event.action];
    if (tab) {
      this.openDetail(event.row.item, tab);
      return;
    }
    this.prepare(event.action as ContainerAction, [event.row.item]);
  }

  protected openDetail(item: ContainerListItem, tab = 'logs'): void {
    void this.router.navigate(['/containers', item.environmentId, item.containerId], {
      queryParams: { aba: tab },
      queryParamsHandling: 'merge',
    });
  }

  protected openRow(row: ContainerRow): void {
    this.openDetail(row.item, 'resumo');
  }

  protected prepareBatch(action: ContainerAction): void {
    this.moreOpen.set(false);
    if (this.selectedItems().length === 0) {
      this.toast.notify('info', { message: 'Selecione containers na lista para aplicar a ação em lote.' });
      return;
    }
    this.prepare(action, this.selectedItems());
  }

  private prepare(action: ContainerAction, items: ContainerListItem[]): void {
    if (this.actions.running()) {
      this.toast.notify('warning', { message: 'Aguarde o lote em andamento terminar.' });
      return;
    }
    this.pending.set({ action, eligibility: evaluateEligibility(items, action) });
  }

  protected readonly pendingProduction = computed(() =>
    this.pending()?.eligibility.eligible.some(item => item.stage === 'production') ?? false,
  );

  protected async confirm(): Promise<void> {
    const pending = this.pending();
    if (!pending) return;
    this.submitting.set(true);
    try {
      await this.actions.run(pending.action, pending.eligibility.eligible);
      this.pending.set(null);
      this.selection.set([]);
    } catch (error: unknown) {
      this.toast.error(error instanceof Error ? error.message : 'Não foi possível enviar os comandos.');
    } finally {
      this.submitting.set(false);
    }
  }

  protected toggleStatus(status: ContainerDisplayStatus): void {
    const current = this.facade.filters().statuses;
    const next = current.includes(status) ? current.filter(s => s !== status) : [...current, status];
    this.navigate({ status: next.length ? next : null });
  }

  protected setHost(environmentId: string): void {
    this.navigate({ environmentId: environmentId || null });
  }

  protected setEngine(engine: string): void {
    this.navigate({ engine: engine || null });
  }

  protected clearStatus(): void {
    this.navigate({ status: null });
  }

  protected onDocumentDown(event: MouseEvent): void {
    if (this.moreOpen() && !(event.target as HTMLElement).closest('.cl-more')) this.moreOpen.set(false);
  }

  private navigate(params: Record<string, string | string[] | null>): void {
    void this.router.navigate([], { queryParams: params, queryParamsHandling: 'merge', replaceUrl: true });
  }
}

function formatPorts(item: ContainerListItem): string {
  const ports = item.ports ?? [];
  if (ports.length === 0) return '—';
  return ports
    .map(port => (port.hostPort ? `${port.hostPort}→${port.containerPort}/${port.protocol}` : `${port.containerPort}/${port.protocol}`))
    .join(', ');
}
