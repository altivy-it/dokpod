import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { Router } from '@angular/router';
import { EnvironmentDirectoryService } from '../../core/environment-directory.service';
import type { PodAction, PodItem, PodStatus } from '../../data-access/generated/control-plane';
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
import { PodActionDrawer } from './pod-action.drawer';
import { evaluatePodEligibility, PodActionsService, podFailureText, type PodEligibility } from './pod-actions.service';
import { PodDetailDrawer } from './pod-detail.drawer';
import { POD_ACTION, POD_STATUS } from './pod-labels';
import { PodsApiService } from './pods-api.service';
import { PodsFacade, podKey, type PodFilters, type PodSort } from './pods.facade';

interface PodRow {
  id: string;
  name: string;
  status: string;
  containers: string;
  host: string;
  networks: string;
  created: string | null;
  item: PodItem;
}

const SERVER_SORT: Record<string, [PodSort, PodSort]> = {
  name: ['name', '-name'],
  created: ['createdAt', '-createdAt'],
  host: ['environment', 'environment'],
};
const STATUS_ORDER: PodStatus[] = ['running', 'degraded', 'paused', 'exited', 'created', 'error'];
const TONES: Record<string, string> = Object.fromEntries(Object.values(POD_STATUS).map(s => [s.label, s.tone]));

function asArray(value: string | string[] | undefined): string[] {
  return value === undefined ? [] : Array.isArray(value) ? value : [value];
}

/** Resumo dos containers do pod, sem o de infraestrutura. */
export function podContainersText(item: Pick<PodItem, 'containers' | 'runningContainers'>): string {
  const total = item.containers.filter(c => !c.infra).length;
  return total === 0 ? 'nenhum' : `${item.runningContainers} de ${total} em execução`;
}

/** Operação → Pods (spec 021): pods do Podman, detalhe e ações sobre o pod inteiro. */
@Component({
  selector: 'app-pods-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DokButton, DokDataGrid, DokEmptyState, DokIcon, DokStatusChip, PodActionDrawer, PodDetailDrawer],
  providers: [PodsFacade],
  styleUrl: './pods.page.css',
  templateUrl: './pods.page.html',
})
export class PodsPage {
  readonly environmentId = input<string | string[] | undefined>();
  readonly status = input<string | string[] | undefined>();
  readonly q = input<string | undefined>();
  readonly sort = input<string | undefined>();
  readonly pod = input<string | undefined>();
  readonly podHost = input<string | undefined>();

  protected readonly facade = inject(PodsFacade);
  protected readonly actions = inject(PodActionsService);
  protected readonly stageFilter = inject(StageFilterService);
  private readonly directory = inject(EnvironmentDirectoryService);
  private readonly api = inject(PodsApiService);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);

  protected readonly search = signal('');
  protected readonly gridSort = signal<GridSort | null>(null);
  protected readonly selection = signal<string[]>([]);
  protected readonly pending = signal<{ action: PodAction; eligibility: PodEligibility } | null>(null);
  protected readonly submitting = signal(false);
  protected readonly detail = signal<PodItem | null>(null);
  protected readonly detailError = signal<string | null>(null);
  protected readonly moreOpen = signal(false);
  protected readonly tones = TONES;
  protected readonly actionLabels = POD_ACTION;
  protected readonly failureText = podFailureText;

  protected readonly columns: GridColumn<PodRow>[] = [
    { field: 'name', header: 'Pod', width: 240, key: true, locked: true, mono: true },
    { field: 'status', header: 'Situação', type: 'status', width: 140, sortable: false, filterable: true },
    { field: 'containers', header: 'Containers', width: 190, sortable: false },
    { field: 'host', header: 'Host', width: 160, filterable: true, groupable: true },
    { field: 'networks', header: 'Redes', width: 180, sortable: false, mono: true },
    { field: 'created', header: 'Criado', type: 'datetime', width: 150 },
  ];

  protected readonly rows = computed<PodRow[]>(() =>
    this.facade.items().map(item => ({
      id: podKey(item),
      name: item.agentPod ? `${item.name} (pod do agente)` : item.name,
      status: POD_STATUS[item.status].label,
      containers: podContainersText(item),
      host: item.environmentName,
      networks: item.networks.join(', ') || '—',
      created: item.createdAt ?? null,
      item,
    })),
  );

  private readonly stageHosts = computed(() =>
    this.directory.environments().filter(env => !this.stageFilter.stage() || env.stage === this.stageFilter.stage()),
  );
  protected readonly facets = computed<Record<string, GridFacet[]>>(() => ({
    status: STATUS_ORDER.map(s => ({ value: s, label: POD_STATUS[s].label, count: this.facade.summary()?.byStatus[s] ?? 0 })),
    host: this.stageHosts().map(env => ({ value: env.environmentId, label: env.name })),
  }));
  protected readonly gridFilters = computed<Record<string, string[]>>(() => ({ status: this.facade.filters().statuses, host: this.facade.filters().environmentIds }));
  protected readonly summaryChips = computed(() => {
    const counts = this.facade.summary()?.byStatus ?? {};
    return STATUS_ORDER.filter(s => (counts[s] ?? 0) > 0).map(s => ({ status: s, count: counts[s] ?? 0, ...POD_STATUS[s] }));
  });
  protected readonly filterChips = computed(() => {
    const f = this.facade.filters();
    const chips: { key: 'status' | 'stage' | 'host'; field: string; label: string }[] = [];
    if (f.statuses.length) chips.push({ key: 'status', field: 'Situação', label: f.statuses.map(s => POD_STATUS[s].label).join(', ') });
    if (this.stageFilter.stage()) chips.push({ key: 'stage', field: 'Ambiente', label: this.stageName() });
    if (f.environmentIds.length) chips.push({ key: 'host', field: 'Host', label: f.environmentIds.map(id => this.directory.byId(id)?.name ?? id.slice(0, 8)).join(', ') });
    return chips;
  });
  protected readonly withoutPodsHosts = computed(() => (this.facade.summary()?.withoutPodsHosts ?? []).map(h => h.environmentName).join(', '));
  protected readonly unavailableHosts = computed(() => (this.facade.summary()?.unavailableHosts ?? []).map(h => h.environmentName).join(', '));
  protected readonly selectedItems = computed(() => {
    const selected = new Set(this.selection());
    return this.facade.items().filter(item => selected.has(podKey(item)));
  });
  protected readonly batchSummary = computed(() => this.actions.summary());
  protected readonly stageName = computed(() => stageLabel(this.stageFilter.stage()));
  protected readonly updatedAt = computed(() => (this.facade.updatedAt() ? formatDateTime(this.facade.updatedAt()) : null));
  protected readonly exportName = computed(() => `pods-${(this.stageFilter.stage() ? this.stageName() : 'todos').toLowerCase().replace(/[^a-z0-9]+/g, '-')}`);

  constructor() {
    inject(ShellContextService).set('Pods', ['Operação', 'Pods']);
    void this.directory.load();
    effect(() => {
      const filters: PodFilters = {
        environmentIds: asArray(this.environmentId()),
        statuses: asArray(this.status()).filter((s): s is PodStatus => STATUS_ORDER.includes(s as PodStatus)),
        q: this.q() ?? '',
        sort: (this.sort() as PodSort | undefined) ?? 'name',
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
    effect(() => {
      const keys = new Set(this.facade.items().map(podKey));
      untracked(() => {
        const kept = this.selection().filter(key => keys.has(key));
        if (kept.length !== this.selection().length) this.selection.set(kept);
      });
    });
    // Detalhe pela URL (link do detalhe do container): `?podHost=<host>&pod=<identificador ou nome>`.
    effect(() => {
      const pod = this.pod();
      const host = this.podHost() ?? null;
      untracked(() => void this.openDetail(pod, host));
    });
    // Itens do detalhe acompanham a lista (situação nova após uma ação).
    effect(() => {
      const items = this.facade.items();
      untracked(() => {
        const current = this.detail();
        const fresh = current && items.find(i => i.environmentId === current.environmentId && i.podId === current.podId);
        if (fresh && fresh !== current) this.detail.set(fresh);
      });
    });
    effect(() => {
      const running = this.actions.running();
      untracked(() => {
        if (!running && this.actions.batch()) void this.facade.reload();
      });
    });
  }

  protected readonly rowActionsFor = (row: PodRow): GridRowAction[] => [
    { id: 'detail', label: 'Ver containers do pod', icon: 'info' },
    ...row.item.allowedActions.map(action => ({ id: action, label: `${POD_ACTION[action].verb} pod`, icon: POD_ACTION[action].icon })),
  ];
  protected readonly rowLabel = (row: PodRow) => `${row.item.name} em ${row.host}`;

  protected onRowAction(event: { action: string; row: PodRow }): void {
    if (event.action === 'detail') this.showDetail(event.row.item);
    else this.prepare(event.action as PodAction, [event.row.item]);
  }

  protected showDetail(item: PodItem): void {
    this.detail.set(item);
    this.navigate({ pod: item.podId, podHost: item.environmentId });
  }

  protected closeDetail(): void {
    this.detail.set(null);
    this.detailError.set(null);
    this.navigate({ pod: null, podHost: null });
  }

  protected prepareSelected(action: PodAction): void {
    this.moreOpen.set(false);
    const items = this.selectedItems();
    if (items.length === 0) {
      this.toast.notify('info', { message: 'Selecione pods na lista para aplicar a ação.' });
      return;
    }
    this.prepare(action, items);
  }

  protected prepare(action: PodAction, items: PodItem[]): void {
    if (this.actions.running()) {
      this.toast.notify('info', { message: 'Aguarde a ação em andamento terminar.' });
      return;
    }
    this.pending.set({ action, eligibility: evaluatePodEligibility(items, action) });
  }

  protected async confirm(justification: string | null): Promise<void> {
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

  protected toggleStatus(status: PodStatus): void {
    const current = this.facade.filters().statuses;
    const next = current.includes(status) ? current.filter(s => s !== status) : [...current, status];
    this.navigate({ status: next.length ? next : null });
  }

  protected onGridFilters(filters: Record<string, string[]>): void {
    const toParam = (values: string[] | undefined) => (values && values.length ? values : null);
    this.navigate({ status: toParam(filters['status']), environmentId: toParam(filters['host']) });
  }

  protected removeFilter(key: 'status' | 'stage' | 'host'): void {
    if (key === 'stage') this.stageFilter.select(null);
    else this.navigate({ [key === 'host' ? 'environmentId' : 'status']: null });
  }

  private async openDetail(pod: string | undefined, environmentId: string | null): Promise<void> {
    if (!pod) {
      this.detail.set(null);
      return;
    }
    if (!environmentId) return;
    const current = this.detail();
    if (current && current.environmentId === environmentId && (current.podId === pod || current.name === pod)) return;
    const listed = this.facade.items().find(item => item.environmentId === environmentId && (item.podId === pod || item.name === pod));
    if (listed) {
      this.detail.set(listed);
      return;
    }
    try {
      this.detail.set(await this.api.getPod(environmentId, pod));
      this.detailError.set(null);
    } catch (error: unknown) {
      const status = (error as { status?: number } | null)?.status;
      this.detailError.set(status === 404 ? `O pod ${pod} não está no inventário deste host.` : 'Não foi possível carregar o pod.');
    }
  }

  private navigate(params: Record<string, string | string[] | null>): void {
    void this.router.navigate([], { queryParams: params, queryParamsHandling: 'merge', replaceUrl: true });
  }
}
