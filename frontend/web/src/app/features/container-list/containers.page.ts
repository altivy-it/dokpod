import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { Router } from '@angular/router';
import { EnvironmentDirectoryService } from '../../core/environment-directory.service';
import { ConfirmActionDrawer } from '../../core/container-actions/confirm-action.drawer';
import {
  ACTION_LABEL,
  ContainerActionsService,
  evaluateEligibility,
  type Eligibility,
} from '../../core/container-actions/container-actions.service';
import { intentionalLabel } from '../../data-access/alert-labels';
import { CONTAINER_STATUS, CONTAINER_STATUS_TONES, ENGINE_LABEL } from '../../data-access/console-labels';
import type { ContainerAction } from '../../data-access/control-plane-api.service';
import type { ContainerDisplayStatus, ContainerListItem } from '../../data-access/generated/control-plane';
import {
  DokButton,
  DokDataGrid,
  DokEmptyState,
  DokIcon,
  DokStatusChip,
  formatDateTime,
  ToastService,
  type GridColumn,
  type GridFacet,
  type GridRowAction,
  type GridSort,
} from '../../design-system';
import { ShellContextService } from '../../shell/shell-context.service';
import { StageFilterService, stageLabel } from '../../shell/stage-filter.service';
import { ContainersFacade, rowKey, type ContainerFilters, type ServerSort } from './containers.facade';
import { NewContainerDrawer } from './new-container.drawer';
import { createIneligibility } from './new-container.facade';

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
  // Spec 015: ordenação por consumo atual, feita no servidor sobre todo o conjunto filtrado.
  cpu: ['cpu', '-cpu'],
  mem: ['memory', '-memory'],
};

/** Ordenação da URL refletida no cabeçalho do grid (inverso de SERVER_SORT, só para direções distintas). */
export function gridSortFromServer(sort: string | undefined): GridSort | null {
  for (const [field, [asc, desc]] of Object.entries(SERVER_SORT)) {
    if (asc === desc) continue;
    if (sort === asc && field !== 'name') return { field, dir: 'asc' };
    if (sort === desc) return { field, dir: 'desc' };
  }
  return null;
}

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
    DokIcon,
    DokStatusChip,
    NewContainerDrawer,
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
  readonly novo = input<string | undefined>();

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
  /** Drawer "Novo container" (spec 013), também aberto por `?novo=1` vindo da Visão geral. */
  protected readonly creating = signal(false);
  protected readonly statusLabels = CONTAINER_STATUS;
  protected readonly tones: Record<string, string> = {
    ...CONTAINER_STATUS_TONES,
    // Spec 016 FR-018: parada e pausa justificadas não se confundem com falha.
    'Parado intencionalmente': 'neutral',
    'Pausado intencionalmente': 'info',
  };
  protected readonly actionLabels = ACTION_LABEL;
  protected readonly statusOrder: ContainerDisplayStatus[] = ['running', 'starting', 'restarting', 'failed', 'paused', 'stopped'];

  protected readonly columns: GridColumn<ContainerRow>[] = [
    { field: 'name', header: 'Nome', width: 190, key: true, locked: true },
    { field: 'status', header: 'Situação', type: 'status', width: 140, filterable: true, groupable: true },
    { field: 'engine', header: 'Engine', width: 100, sortable: false, filterable: true, groupable: true },
    { field: 'host', header: 'Host', width: 140, filterable: true, groupable: true },
    { field: 'image', header: 'Imagem', width: 320, mono: true, sortable: false },
    { field: 'cpu', header: 'CPU', unit: '%', type: 'number', decimals: 1, width: 84, sortable: true },
    { field: 'mem', header: 'Memória', unit: 'MiB', type: 'number', width: 110, sortable: true },
    { field: 'ports', header: 'Portas', width: 170, mono: true, sortable: false },
    { field: 'created', header: 'Criado', width: 150, type: 'datetime' },
  ];

  protected readonly rows = computed<ContainerRow[]>(() =>
    this.facade.items().map(item => ({
      id: rowKey(item),
      name: item.name,
      status: item.intentional ? intentionalLabel(item.intentional) : CONTAINER_STATUS[item.displayStatus ?? 'stopped'].label,
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

  /** Hosts do estágio global, para facetas e chips (spec 010 FR-010). */
  private readonly stageHosts = computed(() =>
    this.directory.environments().filter(env => !this.stageFilter.stage() || env.stage === this.stageFilter.stage()),
  );

  /** Facetas do botão "Filtros" do grid; valores são as chaves do servidor e os rótulos, os do board. */
  protected readonly facets = computed<Record<string, GridFacet[]>>(() => {
    const counts = this.facade.summary()?.byDisplayStatus ?? {};
    return {
      status: this.statusOrder.map(status => ({ value: status, label: CONTAINER_STATUS[status].label, count: counts[status] ?? 0 })),
      engine: [
        { value: 'docker', label: 'Docker' },
        { value: 'podman', label: 'Podman' },
      ],
      host: this.stageHosts().map(env => ({ value: env.environmentId, label: env.name, count: env.containerCount ?? undefined })),
    };
  });

  protected readonly gridFilters = computed<Record<string, string[]>>(() => {
    const f = this.facade.filters();
    return { status: f.statuses, engine: f.engines, host: f.environmentIds };
  });

  /** Estado vazio: só o filtro de situação (cards da Visão geral, spec 015) tem mensagem própria. */
  protected readonly emptyTitle = computed(() => {
    const f = this.facade.filters();
    const onlyStatus = f.statuses.length > 0 && !f.q && f.engines.length === 0 && f.environmentIds.length === 0;
    return onlyStatus ? 'Nenhum container nesta situação.' : 'Nenhum container encontrado com os filtros aplicados.';
  });

  /** Chips de filtros ativos acima do grid (board: "Engine: Docker, Podman", "Ambiente: Produção"). */
  protected readonly filterChips = computed(() => {
    const f = this.facade.filters();
    const chips: { key: 'engine' | 'stage' | 'host' | 'status'; field: string; label: string }[] = [];
    if (f.engines.length) chips.push({ key: 'engine', field: 'Engine', label: f.engines.map(e => ENGINE_LABEL[e] ?? e).join(', ') });
    if (this.stageFilter.stage()) chips.push({ key: 'stage', field: 'Ambiente', label: this.stageName() });
    if (f.environmentIds.length) chips.push({ key: 'host', field: 'Host', label: this.hostFilterName() });
    if (f.statuses.length) chips.push({ key: 'status', field: 'Situação', label: f.statuses.map(s => CONTAINER_STATUS[s].label).join(', ') });
    return chips;
  });

  protected readonly exportName = computed(() => `containers-${(this.stageFilter.stage() ? this.stageName() : 'todos').toLowerCase().replace(/[^a-z0-9]+/g, '-')}`);
  protected readonly stageName = computed(() => stageLabel(this.stageFilter.stage()));
  protected readonly updatedAt = computed(() => (this.facade.observedAt() ? formatDateTime(this.facade.observedAt()) : null));
  protected readonly hostFilterName = computed(() => {
    const ids = this.facade.filters().environmentIds;
    return ids.map(id => this.directory.byId(id)?.name ?? id.slice(0, 8)).join(', ');
  });
  protected readonly batchSummary = computed(() => this.actions.summary());
  protected readonly canCreate = computed(() => this.stageHosts().some(env => createIneligibility(env) === null));

  protected closeCreate(): void {
    this.creating.set(false);
    void this.facade.reload();
  }

  constructor() {
    inject(ShellContextService).set('Containers', ['Operação', 'Containers']);
    void this.directory.load();
    effect(() => {
      if (this.novo() !== '1') return;
      untracked(() => {
        this.creating.set(true);
        this.navigate({ novo: null });
      });
    });
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
    // A URL manda na ordenação inicial: sem isso, o efeito abaixo trocaria `sort` da URL por `name` ao abrir
    // a página (por exemplo, os cards de CPU e memória da Visão geral, spec 015).
    effect(() => {
      const fromUrl = gridSortFromServer(this.sort());
      untracked(() => {
        const current = this.gridSort();
        if (fromUrl && (current?.field !== fromUrl.field || current?.dir !== fromUrl.dir)) this.gridSort.set(fromUrl);
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
    const actions: GridRowAction[] = [{ id: 'logs', label: 'Ver logs', icon: 'list' }];
    // Spec 014 T012: terminal só quando a API concede `exec` (scope × capability × em execução).
    if (allowed.has('exec' as ContainerAction)) actions.push({ id: 'terminal', label: 'Abrir terminal', icon: 'command' });
    actions.push({ id: 'inspect', label: 'Inspecionar', icon: 'eye' });
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

  /** Falhas de envio com texto do operador; demais códigos aparecem como vieram. */
  protected failureText(code: string): string {
    if (code === 'justification_required') return 'justificativa obrigatória neste estágio';
    if (code === 'justification_invalid') return 'justificativa inválida';
    return code;
  }

  protected readonly pendingProduction = computed(() =>
    this.pending()?.eligibility.eligible.some(item => item.stage === 'production') ?? false,
  );

  protected async confirm(justification: string | null = null): Promise<void> {
    const pending = this.pending();
    if (!pending) return;
    this.submitting.set(true);
    try {
      await this.actions.run(pending.action, pending.eligibility.eligible, justification);
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

  protected onGridFilters(filters: Record<string, string[]>): void {
    const toParam = (values: string[] | undefined) => (values && values.length ? values : null);
    this.navigate({ status: toParam(filters['status']), engine: toParam(filters['engine']), environmentId: toParam(filters['host']) });
  }

  protected removeFilter(key: 'engine' | 'stage' | 'host' | 'status'): void {
    if (key === 'stage') this.stageFilter.select(null);
    else this.navigate({ [key === 'host' ? 'environmentId' : key]: null });
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
