import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { Router } from '@angular/router';
import { EnvironmentDirectoryService } from '../../core/environment-directory.service';
import { ENGINE_LABEL } from '../../data-access/console-labels';
import type { ImageItem } from '../../data-access/generated/control-plane';
import {
  DokButton,
  DokDataGrid,
  DokEmptyState,
  DokIcon,
  DokStatusChip,
  formatBytes,
  formatDateTime,
  type GridColumn,
  type GridFacet,
  type GridRowAction,
  type GridSort,
} from '../../design-system';
import { ShellContextService } from '../../shell/shell-context.service';
import { StageFilterService, stageLabel } from '../../shell/stage-filter.service';
import { ImagesFacade, imageKey, type ImageFilters, type ImageSort } from './images.facade';
import { pullIneligibility, PullImageDrawer } from './pull-image.drawer';

interface ImageRow {
  id: string;
  name: string;
  usage: string;
  host: string;
  engine: string;
  size: number;
  created: string | null;
  imageId: string;
  item: ImageItem;
}

const SERVER_SORT: Record<string, [ImageSort, ImageSort]> = {
  name: ['name', '-name'],
  size: ['size', '-size'],
  created: ['createdAt', '-createdAt'],
};

const USAGE_TONES: Record<string, string> = { 'Em uso': 'success', 'Sem uso': 'warning' };

function asArray(value: string | string[] | undefined): string[] {
  return value === undefined ? [] : Array.isArray(value) ? value : [value];
}

/** Nome exibido: primeira tag (sem `docker.io/library/`), ou `<sem tag>` com o ID curto. */
export function imageDisplayName(item: Pick<ImageItem, 'repoTags' | 'imageId'>): string {
  const tag = item.repoTags[0];
  if (!tag) return `<sem tag> ${item.imageId.replace('sha256:', '').slice(0, 12)}`;
  return tag.replace(/^docker\.io\/library\//, '').replace(/^docker\.io\//, '');
}

/** Inventário de imagens por host (spec 012 US1) no padrão da lista de containers. */
@Component({
  selector: 'app-images-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DokButton, DokDataGrid, DokEmptyState, DokIcon, DokStatusChip, PullImageDrawer],
  providers: [ImagesFacade],
  styleUrl: './images.page.css',
  templateUrl: './images.page.html',
})
export class ImagesPage {
  readonly environmentId = input<string | string[] | undefined>();
  readonly engine = input<string | string[] | undefined>();
  readonly semUso = input<string | undefined>();
  readonly q = input<string | undefined>();
  readonly sort = input<string | undefined>();
  readonly baixar = input<string | undefined>();

  protected readonly facade = inject(ImagesFacade);
  protected readonly stageFilter = inject(StageFilterService);
  private readonly directory = inject(EnvironmentDirectoryService);
  private readonly router = inject(Router);

  protected readonly search = signal('');
  protected readonly gridSort = signal<GridSort | null>(null);
  protected readonly drawer = signal<{ reference: string; hosts: string[] } | null>(null);
  protected readonly tones = USAGE_TONES;
  protected readonly formatBytes = formatBytes;

  protected readonly columns: GridColumn<ImageRow>[] = [
    { field: 'name', header: 'Imagem', width: 300, key: true, locked: true, mono: true },
    { field: 'usage', header: 'Uso', type: 'status', width: 120, sortable: false, filterable: true },
    { field: 'host', header: 'Host', width: 150, sortable: false, filterable: true, groupable: true },
    { field: 'engine', header: 'Engine', width: 100, sortable: false, filterable: true, groupable: true },
    { field: 'size', header: 'Tamanho', unit: 'MiB', type: 'number', decimals: 1, width: 110 },
    { field: 'created', header: 'Criada', type: 'datetime', width: 150 },
    { field: 'imageId', header: 'ID', width: 140, mono: true, sortable: false },
  ];

  protected readonly rows = computed<ImageRow[]>(() =>
    this.facade.items().map(item => ({
      id: imageKey(item),
      name: imageDisplayName(item),
      usage: item.inUse ? 'Em uso' : 'Sem uso',
      host: item.environmentName,
      engine: item.engine ? (ENGINE_LABEL[item.engine] ?? item.engine) : '—',
      size: item.sizeBytes / (1024 * 1024),
      created: item.createdAt ?? null,
      imageId: item.imageId.replace('sha256:', '').slice(0, 12),
      item,
    })),
  );

  private readonly stageHosts = computed(() =>
    this.directory.environments().filter(env => !this.stageFilter.stage() || env.stage === this.stageFilter.stage()),
  );
  protected readonly canPullAnywhere = computed(() => this.stageHosts().some(env => pullIneligibility(env) === null));

  protected readonly facets = computed<Record<string, GridFacet[]>>(() => ({
    usage: [
      { value: 'unused', label: 'Sem uso', count: this.facade.summary()?.unused },
    ],
    engine: [
      { value: 'docker', label: 'Docker' },
      { value: 'podman', label: 'Podman' },
    ],
    host: this.stageHosts().map(env => ({ value: env.environmentId, label: env.name })),
  }));

  protected readonly gridFilters = computed<Record<string, string[]>>(() => {
    const f = this.facade.filters();
    return { usage: f.unused ? ['unused'] : [], engine: f.engines, host: f.environmentIds };
  });

  protected readonly filterChips = computed(() => {
    const f = this.facade.filters();
    const chips: { key: 'engine' | 'stage' | 'host' | 'unused'; field: string; label: string }[] = [];
    if (f.unused) chips.push({ key: 'unused', field: 'Uso', label: 'Sem uso' });
    if (f.engines.length) chips.push({ key: 'engine', field: 'Engine', label: f.engines.map(e => ENGINE_LABEL[e] ?? e).join(', ') });
    if (this.stageFilter.stage()) chips.push({ key: 'stage', field: 'Ambiente', label: this.stageName() });
    if (f.environmentIds.length) chips.push({ key: 'host', field: 'Host', label: f.environmentIds.map(id => this.directory.byId(id)?.name ?? id.slice(0, 8)).join(', ') });
    return chips;
  });

  protected readonly stageName = computed(() => stageLabel(this.stageFilter.stage()));
  protected readonly updatedAt = computed(() => (this.facade.updatedAt() ? formatDateTime(this.facade.updatedAt()) : null));
  protected readonly exportName = computed(() => `imagens-${(this.stageFilter.stage() ? this.stageName() : 'todos').toLowerCase().replace(/[^a-z0-9]+/g, '-')}`);

  constructor() {
    inject(ShellContextService).set('Imagens', ['Operação', 'Imagens']);
    void this.directory.load();
    effect(() => {
      const filters: ImageFilters = {
        environmentIds: asArray(this.environmentId()),
        engines: asArray(this.engine()).filter((e): e is 'docker' | 'podman' => e === 'docker' || e === 'podman'),
        unused: this.semUso() === '1' || this.semUso() === 'true',
        q: this.q() ?? '',
        sort: (this.sort() as ImageSort | undefined) ?? 'name',
      };
      untracked(() => {
        if (JSON.stringify(this.facade.filters()) !== JSON.stringify(filters)) this.facade.filters.set(filters);
        this.search.set(filters.q);
      });
    });
    effect(() => {
      if (this.baixar() !== '1') return;
      untracked(() => {
        this.openPull('', asArray(this.environmentId()));
        this.navigate({ baixar: null });
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
  }

  protected readonly rowActionsFor = (row: ImageRow): GridRowAction[] => {
    const actions: GridRowAction[] = [];
    if (row.item.canPull && row.item.repoTags.length) actions.push({ id: 'pull', label: 'Baixar novamente', icon: 'download' });
    actions.push({ id: 'containers', label: 'Ver containers do host', icon: 'box' });
    return actions;
  };
  protected readonly rowLabel = (row: ImageRow) => `${row.name} em ${row.host}`;

  protected onRowAction(event: { action: string; row: ImageRow }): void {
    if (event.action === 'pull') this.openPull(event.row.item.repoTags[0] ?? '', [event.row.item.environmentId]);
    else void this.router.navigate(['/containers'], { queryParams: { environmentId: event.row.item.environmentId }, queryParamsHandling: 'merge' });
  }

  protected openPull(reference = '', hosts: string[] = []): void {
    this.drawer.set({ reference, hosts });
  }

  protected onGridFilters(filters: Record<string, string[]>): void {
    const toParam = (values: string[] | undefined) => (values && values.length ? values : null);
    this.navigate({
      semUso: filters['usage']?.includes('unused') ? '1' : null,
      engine: toParam(filters['engine']),
      environmentId: toParam(filters['host']),
    });
  }

  protected toggleUnused(): void {
    this.navigate({ semUso: this.facade.filters().unused ? null : '1' });
  }

  protected removeFilter(key: 'engine' | 'stage' | 'host' | 'unused'): void {
    if (key === 'stage') this.stageFilter.select(null);
    else this.navigate({ [key === 'host' ? 'environmentId' : key === 'unused' ? 'semUso' : key]: null });
  }

  private navigate(params: Record<string, string | string[] | null>): void {
    void this.router.navigate([], { queryParams: params, queryParamsHandling: 'merge', replaceUrl: true });
  }
}
