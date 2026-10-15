import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { Router } from '@angular/router';
import { EnvironmentDirectoryService } from '../../core/environment-directory.service';
import type { RegistryDetail, RegistryItem, RegistryOrigin, RegistryPolicy } from '../../data-access/generated/control-plane';
import {
  DokButton,
  DokDataGrid,
  DokEmptyState,
  DokIcon,
  DokStatusChip,
  DokTabs,
  formatDateTime,
  type GridColumn,
  type GridFacet,
  type GridRowAction,
  type GridSort,
  type TabItem,
  ToastService,
} from '../../design-system';
import { ShellContextService } from '../../shell/shell-context.service';
import { StageFilterService, stageLabel } from '../../shell/stage-filter.service';
import { toLoadError, type LoadError } from '../overview/overview.facade';
import { RegistriesApiService, type PolicyStage } from './registries-api.service';
import { RegistriesFacade, type RegistryFilters, type RegistrySort } from './registries.facade';
import { RegistryDetailDrawer } from './registry-detail.drawer';
import { ORIGIN_LABEL, POLICY_STAGE_LABEL } from './registry-labels';
import { RegistryPolicyDrawer } from './registry-policy.drawer';

interface RegistryRow {
  id: string;
  registry: string;
  situation: string;
  origins: string;
  hosts: number;
  images: number;
  size: number;
  credential: string;
  item: RegistryItem;
}

const SERVER_SORT: Record<string, [RegistrySort, RegistrySort]> = {
  registry: ['registry', '-registry'],
  hosts: ['hosts', '-hosts'],
  images: ['images', '-images'],
};
const SITUATION_TONES: Record<string, string> = { Permitido: 'success', Inseguro: 'warning', 'Fora da política': 'warning', Bloqueado: 'error' };
const ORIGINS = Object.keys(ORIGIN_LABEL) as RegistryOrigin[];
type View = 'registros' | 'politica';

function asArray(value: string | string[] | undefined): string[] {
  return value === undefined ? [] : Array.isArray(value) ? value : [value];
}

/** Situação mais grave do registro: bloqueado, inseguro, fora da política ou permitido. */
export function registrySituation(item: Pick<RegistryItem, 'blockedHosts' | 'insecureHosts' | 'outsidePolicyHosts'>): string {
  if (item.blockedHosts > 0) return 'Bloqueado';
  if (item.insecureHosts > 0) return 'Inseguro';
  if (item.outsidePolicyHosts > 0) return 'Fora da política';
  return 'Permitido';
}

/** Infraestrutura → Registros (spec 020): registros dos hosts e política de registros permitidos por estágio. */
@Component({
  selector: 'app-registries-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DokButton, DokDataGrid, DokEmptyState, DokIcon, DokStatusChip, DokTabs, RegistryDetailDrawer, RegistryPolicyDrawer],
  providers: [RegistriesFacade],
  styleUrl: './registries.page.css',
  templateUrl: './registries.page.html',
})
export class RegistriesPage {
  readonly vista = input<string | undefined>();
  readonly environmentId = input<string | string[] | undefined>();
  readonly origem = input<string | string[] | undefined>();
  readonly inseguros = input<string | undefined>();
  readonly foraDaPolitica = input<string | undefined>();
  readonly q = input<string | undefined>();
  readonly sort = input<string | undefined>();
  readonly registro = input<string | undefined>();

  protected readonly facade = inject(RegistriesFacade);
  protected readonly stageFilter = inject(StageFilterService);
  private readonly directory = inject(EnvironmentDirectoryService);
  private readonly api = inject(RegistriesApiService);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);

  protected readonly search = signal('');
  protected readonly gridSort = signal<GridSort | null>(null);
  protected readonly detail = signal<RegistryDetail | null>(null);
  protected readonly detailError = signal<string | null>(null);
  protected readonly policies = signal<RegistryPolicy[] | null>(null);
  protected readonly policyError = signal<LoadError | null>(null);
  protected readonly editing = signal<RegistryPolicy | null>(null);
  protected readonly saving = signal(false);
  protected readonly tones = SITUATION_TONES;
  protected readonly stageLabels = POLICY_STAGE_LABEL;
  protected readonly formatDateTime = formatDateTime;

  protected readonly view = computed<View>(() => (this.vista() === 'politica' ? 'politica' : 'registros'));
  protected readonly tabs = computed<TabItem[]>(() => [
    { id: 'registros', label: 'Registros', icon: 'inbox', count: this.facade.summary()?.total ?? null },
    { id: 'politica', label: 'Política por estágio', icon: 'shield' },
  ]);

  protected readonly columns: GridColumn<RegistryRow>[] = [
    { field: 'registry', header: 'Registro', width: 240, key: true, locked: true, mono: true },
    { field: 'situation', header: 'Situação', type: 'status', width: 140, sortable: false },
    { field: 'origins', header: 'Origem', width: 220, sortable: false, filterable: true },
    { field: 'hosts', header: 'Hosts', type: 'number', width: 90 },
    { field: 'images', header: 'Imagens', type: 'number', width: 100 },
    { field: 'size', header: 'Tamanho', unit: 'MiB', type: 'number', decimals: 1, width: 110, sortable: false },
    { field: 'credential', header: 'Credencial', width: 130, sortable: false },
  ];

  protected readonly rows = computed<RegistryRow[]>(() =>
    this.facade.items().map(item => ({
      id: item.registry,
      registry: item.registry,
      situation: registrySituation(item),
      origins: item.origins.map(o => ORIGIN_LABEL[o]).join(', ') + (item.mirrorOf ? ` (de ${item.mirrorOf})` : ''),
      hosts: item.hosts,
      images: item.images,
      size: item.sizeBytes / (1024 * 1024),
      credential: item.credentialHosts === 0 ? '—' : item.credentialHosts === 1 ? 'em 1 host' : `em ${item.credentialHosts} hosts`,
      item,
    })),
  );

  private readonly stageHosts = computed(() =>
    this.directory.environments().filter(env => !this.stageFilter.stage() || env.stage === this.stageFilter.stage()),
  );
  protected readonly facets = computed<Record<string, GridFacet[]>>(() => ({
    origins: ORIGINS.map(o => ({ value: o, label: ORIGIN_LABEL[o] })),
    host: this.stageHosts().map(env => ({ value: env.environmentId, label: env.name })),
  }));
  protected readonly gridFilters = computed<Record<string, string[]>>(() => ({ origins: this.facade.filters().origins, host: this.facade.filters().environmentIds }));
  protected readonly filterChips = computed(() => {
    const f = this.facade.filters();
    const chips: { key: 'origin' | 'stage' | 'host' | 'insecure' | 'outside'; field: string; label: string }[] = [];
    if (f.insecure) chips.push({ key: 'insecure', field: 'Situação', label: 'Inseguros' });
    if (f.outsidePolicy) chips.push({ key: 'outside', field: 'Situação', label: 'Fora da política' });
    if (f.origins.length) chips.push({ key: 'origin', field: 'Origem', label: f.origins.map(o => ORIGIN_LABEL[o]).join(', ') });
    if (this.stageFilter.stage()) chips.push({ key: 'stage', field: 'Ambiente', label: this.stageName() });
    if (f.environmentIds.length) chips.push({ key: 'host', field: 'Host', label: f.environmentIds.map(id => this.directory.byId(id)?.name ?? id.slice(0, 8)).join(', ') });
    return chips;
  });
  protected readonly unavailableHosts = computed(() => (this.facade.summary()?.unavailableHosts ?? []).map(h => h.environmentName).join(', '));
  protected readonly stageName = computed(() => stageLabel(this.stageFilter.stage()));
  protected readonly updatedAt = computed(() => (this.facade.updatedAt() ? formatDateTime(this.facade.updatedAt()) : null));
  protected readonly exportName = computed(() => `registros-${(this.stageFilter.stage() ? this.stageName() : 'todos').toLowerCase().replace(/[^a-z0-9]+/g, '-')}`);

  constructor() {
    inject(ShellContextService).set('Registros', ['Infraestrutura', 'Registros']);
    void this.directory.load();
    effect(() => {
      const filters: RegistryFilters = {
        environmentIds: asArray(this.environmentId()),
        origins: asArray(this.origem()).filter((o): o is RegistryOrigin => ORIGINS.includes(o as RegistryOrigin)),
        insecure: this.inseguros() === '1',
        outsidePolicy: this.foraDaPolitica() === '1',
        q: this.q() ?? '',
        sort: (this.sort() as RegistrySort | undefined) ?? 'registry',
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
        const mapped = sort ? SERVER_SORT[sort.field]?.[sort.dir === 'asc' ? 0 : 1] : 'registry';
        if (mapped && mapped !== (this.sort() ?? 'registry')) this.navigate({ sort: mapped === 'registry' ? null : mapped });
      });
    });
    effect(() => {
      const registry = this.registro();
      untracked(() => void this.openDetail(registry));
    });
    effect(() => {
      if (this.view() === 'politica') untracked(() => void this.loadPolicies());
    });
  }

  protected readonly rowActionsFor = (): GridRowAction[] => [{ id: 'detail', label: 'Ver configuração por host', icon: 'info' }];
  protected readonly rowLabel = (row: RegistryRow) => row.registry;

  protected selectView(view: string): void {
    this.navigate({ vista: view === 'politica' ? 'politica' : null });
  }

  protected showDetail(item: RegistryItem): void {
    this.navigate({ registro: item.registry });
  }

  protected closeDetail(): void {
    this.navigate({ registro: null });
  }

  protected toggle(key: 'inseguros' | 'foraDaPolitica'): void {
    const current = key === 'inseguros' ? this.facade.filters().insecure : this.facade.filters().outsidePolicy;
    this.navigate({ [key]: current ? null : '1' });
  }

  protected onGridFilters(filters: Record<string, string[]>): void {
    const toParam = (values: string[] | undefined) => (values && values.length ? values : null);
    this.navigate({ origem: toParam(filters['origins']), environmentId: toParam(filters['host']) });
  }

  protected removeFilter(key: 'origin' | 'stage' | 'host' | 'insecure' | 'outside'): void {
    if (key === 'stage') this.stageFilter.select(null);
    else this.navigate({ [{ origin: 'origem', host: 'environmentId', insecure: 'inseguros', outside: 'foraDaPolitica' }[key]]: null });
  }

  protected async savePolicy(stage: PolicyStage, registries: string[]): Promise<void> {
    this.saving.set(true);
    try {
      await this.api.savePolicy(stage, registries);
      this.editing.set(null);
      this.toast.success(`Política de ${POLICY_STAGE_LABEL[stage]} salva: ${registries.length ? registries.join(', ') : 'qualquer registro'}.`);
      await this.loadPolicies();
      void this.facade.reload();
    } catch (error: unknown) {
      const problem = error as { code?: string } | null;
      this.toast.error(
        problem?.code === 'authorization_denied' ? 'Alterar a política exige administração de todos os hosts do estágio.'
        : problem?.code === 'registry_invalid' ? 'Há registro inválido na lista.'
        : 'Não foi possível salvar a política.',
      );
    } finally {
      this.saving.set(false);
    }
  }

  private async loadPolicies(): Promise<void> {
    try {
      this.policies.set(await this.api.listPolicies());
      this.policyError.set(null);
    } catch (error: unknown) {
      this.policyError.set(toLoadError(error));
    }
  }

  private async openDetail(registry: string | undefined): Promise<void> {
    if (!registry) {
      this.detail.set(null);
      this.detailError.set(null);
      return;
    }
    try {
      this.detail.set(await this.api.getRegistry(registry));
      this.detailError.set(null);
    } catch (error: unknown) {
      const status = (error as { status?: number } | null)?.status;
      this.detail.set(null);
      this.detailError.set(status === 404 ? `O registro ${registry} não aparece em nenhum host que você pode ler.` : 'Não foi possível carregar o registro.');
    }
  }

  private navigate(params: Record<string, string | string[] | null>): void {
    void this.router.navigate([], { queryParams: params, queryParamsHandling: 'merge', replaceUrl: true });
  }
}
