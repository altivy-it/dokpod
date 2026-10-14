import { NgTemplateOutlet } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  contentChildren,
  Directive,
  ElementRef,
  input,
  model,
  output,
  signal,
  TemplateRef,
  viewChild,
  inject,
} from '@angular/core';
import { formatDateTime, formatNumber } from '../format/format';
import { DokIcon } from '../icon/dok-icon';
import { DokSearchField } from '../search/dok-search-field';
import { tone } from '../tone';

export type GridColumnType = 'text' | 'number' | 'status' | 'datetime';

export interface GridColumn<T = Record<string, unknown>> {
  field: string;
  header: string;
  type?: GridColumnType;
  width?: number;
  decimals?: number;
  unit?: string;
  mono?: boolean;
  key?: boolean;
  filterable?: boolean;
  sortable?: boolean;
  value?: (row: T) => unknown;
}

export interface GridRowAction {
  id: string;
  label: string;
  icon?: string;
  danger?: boolean;
}

export interface GridSort {
  field: string;
  dir: 'asc' | 'desc';
}

export type GridDensity = 'compact' | 'standard' | 'comfortable';

/** Template de célula: `<ng-template dokCell="status" let-row>…</ng-template>`. */
@Directive({ selector: 'ng-template[dokCell]' })
export class DokGridCell {
  readonly field = input.required<string>({ alias: 'dokCell' });
  readonly template = inject(TemplateRef<{ $implicit: unknown }>);
}

const ROW_HEIGHT: Record<GridDensity, number> = { compact: 36, standard: 44, comfortable: 52 };
const DENSITIES: { id: GridDensity; label: string }[] = [
  { id: 'compact', label: 'Compacta' },
  { id: 'standard', label: 'Padrão' },
  { id: 'comfortable', label: 'Confortável' },
];

function normalize(value: unknown): string {
  return String(value ?? '')
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .toLowerCase();
}

/**
 * Grid de dados (EnhancedDataGrid do kit) sobre `table` nativa.
 * Teclado: Tab percorre busca, filtros, cabeçalhos ordenáveis, caixas de seleção e menus de linha;
 * Espaço/Enter ativam; o menu de linha usa setas, Home/End e Esc (foco volta ao gatilho).
 * `clientSide` aplica busca, filtro e ordenação nas linhas carregadas; caso contrário apenas emite.
 */
@Component({
  selector: 'dok-data-grid',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DokIcon, DokSearchField, NgTemplateOutlet],
  host: { style: 'display: block; min-width: 0', '(document:mousedown)': 'onDocumentDown($event)' },
  templateUrl: './dok-data-grid.html',
})
export class DokDataGrid<T extends object = Record<string, unknown>> {
  readonly title = input.required<string>();
  readonly entityLabel = input<[string, string]>(['item', 'itens']);
  readonly rows = input.required<T[]>();
  readonly columns = input.required<GridColumn<T>[]>();
  readonly rowId = input<(row: T) => string>(row => String((row as Record<string, unknown>)['id']));
  readonly rowLabel = input<(row: T) => string>(row => String((row as Record<string, unknown>)['name'] ?? ''));
  readonly statusTones = input<Record<string, string>>({});
  readonly rowActions = input<GridRowAction[]>([]);
  readonly rowActionsFor = input<((row: T) => GridRowAction[]) | null>(null);
  readonly selection = model<string[]>([]);
  readonly selectable = input(true);
  readonly sort = model<GridSort | null>(null);
  readonly density = model<GridDensity>('standard');
  readonly searchPlaceholder = input('Buscar');
  readonly search = model('');
  readonly updatedAt = input<string | null>(null);
  readonly subtitleOverride = input<string | null>(null);
  readonly exportName = input<string | null>(null);
  readonly loading = input(false);
  readonly hasMore = input(false);
  readonly loadingMore = input(false);
  readonly totalCount = input<number | null>(null);
  readonly clientSide = input(true);
  readonly height = input<string | null>(null);
  readonly pageSize = input(25);
  readonly emptyTitle = input<string | null>(null);

  readonly rowAction = output<{ action: string; row: T }>();
  readonly rowOpen = output<T>();
  readonly loadMore = output<void>();
  readonly filtersChange = output<Record<string, string[]>>();

  protected readonly cellTemplates = contentChildren(DokGridCell);
  protected readonly filters = signal<Record<string, string[]>>({});
  protected readonly openMenu = signal<'filters' | 'density' | null>(null);
  protected readonly rowMenu = signal<{ id: string; row: T; top: number; right: number; actions: GridRowAction[] } | null>(null);
  protected readonly page = signal(0);
  protected readonly densities = DENSITIES;
  private readonly rowMenuEl = viewChild<ElementRef<HTMLElement>>('rowMenuEl');
  private rowMenuTrigger: HTMLElement | null = null;

  protected readonly filterColumns = computed(() => this.columns().filter(c => c.filterable));
  protected readonly activeFilters = computed(() =>
    Object.entries(this.filters()).filter(([, values]) => values.length > 0),
  );
  protected readonly rowHeight = computed(() => ROW_HEIGHT[this.density()]);
  protected readonly templates = computed(() => {
    const map: Record<string, TemplateRef<{ $implicit: unknown }>> = {};
    for (const cell of this.cellTemplates()) map[cell.field()] = cell.template;
    return map;
  });

  protected readonly data = computed(() => {
    const rows = this.rows();
    if (!this.clientSide()) return rows;
    const filters = this.activeFilters();
    const query = normalize(this.search()).trim();
    const columns = this.columns();
    let out = rows.filter(row => {
      for (const [field, values] of filters) {
        const column = columns.find(c => c.field === field);
        if (!values.includes(String(this.raw(row, column ?? { field, header: field })))) return false;
      }
      if (!query) return true;
      return columns.some(c => normalize(this.text(row, c)).includes(query));
    });
    const sort = this.sort();
    if (sort) {
      const column = columns.find(c => c.field === sort.field);
      const factor = sort.dir === 'desc' ? -1 : 1;
      out = [...out].sort((a, b) => {
        const x = column ? this.raw(a, column) : null;
        const y = column ? this.raw(b, column) : null;
        if (column?.type === 'number') return ((Number(x) || 0) - (Number(y) || 0)) * factor;
        return String(x ?? '').localeCompare(String(y ?? ''), 'pt-BR', { numeric: true }) * factor;
      });
    }
    return out;
  });

  protected readonly pages = computed(() => Math.max(1, Math.ceil(this.data().length / this.pageSize())));
  protected readonly pageIndex = computed(() => Math.min(this.page(), this.pages() - 1));
  protected readonly pageRows = computed(() => {
    const start = this.pageIndex() * this.pageSize();
    return this.data().slice(start, start + this.pageSize());
  });
  protected readonly pageIds = computed(() => this.pageRows().map(row => this.rowId()(row)));
  protected readonly headState = computed<boolean | 'mixed'>(() => {
    const ids = this.pageIds();
    const selected = this.selection();
    const count = ids.filter(id => selected.includes(id)).length;
    return count === 0 ? false : count === ids.length ? true : 'mixed';
  });
  protected readonly subtitle = computed(() => {
    if (this.subtitleOverride()) return this.subtitleOverride();
    const [singular, plural] = this.entityLabel();
    const total = this.totalCount() ?? this.rows().length;
    const filtered = this.clientSide() && (this.activeFilters().length || this.search()) ? `${formatNumber(this.data().length)} de ` : '';
    const updated = this.updatedAt() ? ` · atualizado em ${this.updatedAt()}` : '';
    return `${filtered}${formatNumber(total)} ${total === 1 ? singular : plural}${updated}`;
  });
  protected readonly selectionText = computed(() => {
    const count = this.selection().length;
    const [singular, plural] = this.entityLabel();
    if (count === 0) return 'Nenhum selecionado';
    return count === 1 ? `1 ${singular} selecionado` : `${formatNumber(count)} ${plural} selecionados`;
  });
  protected readonly rangeText = computed(() => {
    const total = this.data().length;
    if (!total) return '0 de 0';
    const start = this.pageIndex() * this.pageSize();
    return `${start + 1}–${Math.min(start + this.pageSize(), total)} de ${formatNumber(total)}`;
  });
  protected readonly pageList = computed(() => {
    const out: (number | '…')[] = [];
    const n = this.pages();
    const current = this.pageIndex();
    for (let i = 0; i < n; i++) {
      if (i < 3 || i === n - 1 || Math.abs(i - current) <= 1) out.push(i);
      else if (out[out.length - 1] !== '…') out.push('…');
    }
    return out;
  });

  protected raw(row: T, column: GridColumn<T>): unknown {
    return column.value ? column.value(row) : (row as Record<string, unknown>)[column.field];
  }

  protected text(row: T, column: GridColumn<T>): string {
    const value = this.raw(row, column);
    if (value === null || value === undefined || value === '') return '—';
    if (column.type === 'number') return formatNumber(Number(value), column.decimals ?? 0);
    if (column.type === 'datetime') return formatDateTime(value as string);
    return String(value);
  }

  protected statusColor(value: unknown): string {
    return tone(this.statusTones()[String(value)] ?? 'neutral').color;
  }

  protected facetValues(column: GridColumn<T>): { value: string; count: number }[] {
    const counts = new Map<string, number>();
    for (const row of this.rows()) {
      const value = String(this.raw(row, column) ?? '');
      counts.set(value, (counts.get(value) ?? 0) + 1);
    }
    return [...counts.entries()]
      .sort((a, b) => a[0].localeCompare(b[0], 'pt-BR'))
      .map(([value, count]) => ({ value, count }));
  }

  protected isFiltered(field: string, value: string): boolean {
    return this.filters()[field]?.includes(value) ?? false;
  }

  protected toggleFilter(field: string, value: string): void {
    const current = this.filters()[field] ?? [];
    const next = current.includes(value) ? current.filter(v => v !== value) : [...current, value];
    this.setFilter(field, next);
  }

  protected setFilter(field: string, values: string[]): void {
    this.filters.update(current => ({ ...current, [field]: values }));
    this.page.set(0);
    this.filtersChange.emit(this.filters());
  }

  protected clearFilters(): void {
    this.filters.set({});
    this.page.set(0);
    this.filtersChange.emit({});
  }

  protected headerFor(field: string): string {
    return this.columns().find(c => c.field === field)?.header ?? field;
  }

  protected toggleSort(column: GridColumn<T>): void {
    if (column.sortable === false) return;
    const sort = this.sort();
    if (!sort || sort.field !== column.field) this.sort.set({ field: column.field, dir: 'asc' });
    else if (sort.dir === 'asc') this.sort.set({ field: column.field, dir: 'desc' });
    else this.sort.set(null);
    this.page.set(0);
  }

  protected ariaSort(column: GridColumn<T>): string {
    const sort = this.sort();
    if (!sort || sort.field !== column.field) return 'none';
    return sort.dir === 'asc' ? 'ascending' : 'descending';
  }

  protected isSelected(row: T): boolean {
    return this.selection().includes(this.rowId()(row));
  }

  protected toggleRow(row: T): void {
    const id = this.rowId()(row);
    const selected = this.selection();
    this.selection.set(selected.includes(id) ? selected.filter(x => x !== id) : [...selected, id]);
  }

  protected toggleAll(): void {
    const ids = this.pageIds();
    const selected = this.selection();
    if (this.headState() === true) this.selection.set(selected.filter(id => !ids.includes(id)));
    else this.selection.set([...selected, ...ids.filter(id => !selected.includes(id))]);
  }

  protected onSearch(value: string): void {
    this.search.set(value);
    this.page.set(0);
  }

  protected toggleToolbarMenu(menu: 'filters' | 'density'): void {
    this.openMenu.set(this.openMenu() === menu ? null : menu);
  }

  protected setDensity(density: GridDensity): void {
    this.density.set(density);
    this.openMenu.set(null);
  }

  protected openRowMenu(event: MouseEvent, row: T): void {
    const id = this.rowId()(row);
    if (this.rowMenu()?.id === id) {
      this.closeRowMenu();
      return;
    }
    const trigger = event.currentTarget as HTMLElement;
    const rect = trigger.getBoundingClientRect();
    this.rowMenuTrigger = trigger;
    const actions = this.rowActionsFor()?.(row) ?? this.rowActions();
    this.rowMenu.set({ id, row, top: rect.bottom + 4, right: window.innerWidth - rect.right, actions });
    queueMicrotask(() => this.menuItems()[0]?.focus());
  }

  protected chooseRowAction(action: GridRowAction): void {
    const menu = this.rowMenu();
    if (!menu) return;
    this.closeRowMenu();
    this.rowAction.emit({ action: action.id, row: menu.row });
  }

  protected onRowMenuKey(event: KeyboardEvent): void {
    const items = this.menuItems();
    const index = items.indexOf(document.activeElement as HTMLElement);
    let next: number | null = null;
    if (event.key === 'ArrowDown') next = (index + 1) % items.length;
    else if (event.key === 'ArrowUp') next = (index - 1 + items.length) % items.length;
    else if (event.key === 'Home') next = 0;
    else if (event.key === 'End') next = items.length - 1;
    else if (event.key === 'Escape' || event.key === 'Tab') {
      event.preventDefault();
      this.closeRowMenu();
      return;
    }
    if (next !== null) {
      event.preventDefault();
      items[next]?.focus();
    }
  }

  protected onRowClick(event: MouseEvent, row: T): void {
    if ((event.target as HTMLElement).closest('button, a, input')) return;
    this.rowOpen.emit(row);
  }

  protected onDocumentDown(event: MouseEvent): void {
    const target = event.target as Node;
    if (this.rowMenu() && !this.rowMenuEl()?.nativeElement.contains(target) && !this.rowMenuTrigger?.contains(target)) {
      this.rowMenu.set(null);
    }
    if (this.openMenu() && !(target as HTMLElement).closest?.('.alt-enhanced-data-grid__mb')) {
      this.openMenu.set(null);
    }
  }

  protected onToolbarKey(event: KeyboardEvent): void {
    if (event.key === 'Escape' && this.openMenu()) {
      event.preventDefault();
      this.openMenu.set(null);
    }
  }

  protected exportCsv(): void {
    const columns = this.columns();
    // Proteção contra injeção de fórmula em planilhas (spec 010 T014).
    const neutralize = (value: string) => (/^[=+\-@\t\r]/.test(value) ? `'${value}` : value);
    const escape = (raw: string) => {
      const value = neutralize(raw);
      return /[";\n]/.test(value) ? `"${value.replace(/"/g, '""')}"` : value;
    };
    const lines = [columns.map(c => escape(c.header)).join(';')];
    for (const row of this.data()) lines.push(columns.map(c => escape(this.text(row, c))).join(';'));
    const blob = new Blob([`﻿${lines.join('\n')}`], { type: 'text/csv;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `${this.exportName() ?? 'export'}.csv`;
    link.click();
    URL.revokeObjectURL(url);
  }

  protected goTo(page: number): void {
    this.page.set(Math.max(0, Math.min(page, this.pages() - 1)));
  }

  protected isPageNumber(value: number | '…'): value is number {
    return value !== '…';
  }

  private closeRowMenu(): void {
    this.rowMenu.set(null);
    const trigger = this.rowMenuTrigger;
    this.rowMenuTrigger = null;
    trigger?.focus();
  }

  private menuItems(): HTMLElement[] {
    return Array.from(this.rowMenuEl()?.nativeElement.querySelectorAll<HTMLElement>('[role="menuitem"]') ?? []);
  }
}
