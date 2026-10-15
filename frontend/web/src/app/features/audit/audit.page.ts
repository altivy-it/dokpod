import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuditAccessService } from '../../core/audit-access.service';
import { EnvironmentDirectoryService } from '../../core/environment-directory.service';
import { AUDIT_CATEGORIES, AUDIT_OUTCOME, auditActionLabel, auditActorLabel } from '../../data-access/audit-labels';
import { AuditApiService } from './audit-api.service';
import type { AuditCategory, AuditEventView, AuditOutcome } from '../../data-access/generated/control-plane';
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
} from '../../design-system';
import { ShellContextService } from '../../shell/shell-context.service';
import { StageFilterService, stageLabel } from '../../shell/stage-filter.service';
import { AuditEventDrawer } from './audit-event.drawer';
import { AUDIT_PERIODS, AuditFacade, auditQuery, periodRange, type AuditPeriod } from './audit.facade';

interface AuditRow {
  id: string;
  time: string;
  outcome: string;
  action: string;
  actor: string;
  host: string;
  target: string;
  category: string;
  event: AuditEventView;
}

const OUTCOMES: AuditOutcome[] = ['succeeded', 'denied', 'failed', 'indeterminate'];

function asArray(value: string | string[] | undefined): string[] {
  return value === undefined ? [] : Array.isArray(value) ? value : [value];
}

/**
 * Governança → Auditoria (spec 017): trilha dos hosts com `audit:read`, sempre por período (até 90 dias),
 * filtros na URL, detalhe com eventos relacionados e exportação CSV de todo o filtro.
 */
@Component({
  selector: 'app-audit-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [AuditEventDrawer, DokButton, DokDataGrid, DokEmptyState, DokIcon, DokStatusChip, FormsModule],
  providers: [AuditFacade],
  styleUrl: './audit.page.css',
  templateUrl: './audit.page.html',
})
export class AuditPage {
  readonly periodo = input<string | undefined>();
  readonly desde = input<string | undefined>();
  readonly ate = input<string | undefined>();
  readonly categoria = input<string | string[] | undefined>();
  readonly resultado = input<string | string[] | undefined>();
  readonly ator = input<string | undefined>();
  readonly environmentId = input<string | string[] | undefined>();
  readonly q = input<string | undefined>();
  readonly evento = input<string | undefined>();
  readonly em = input<string | undefined>();

  protected readonly facade = inject(AuditFacade);
  protected readonly access = inject(AuditAccessService);
  protected readonly stageFilter = inject(StageFilterService);
  private readonly directory = inject(EnvironmentDirectoryService);
  private readonly api = inject(AuditApiService);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);

  protected readonly search = signal('');
  protected readonly actorDraft = signal('');
  protected readonly exporting = signal(false);
  protected readonly periods = AUDIT_PERIODS;
  protected readonly categories = AUDIT_CATEGORIES;
  protected readonly outcomes = OUTCOMES;
  protected readonly outcomeLabels = AUDIT_OUTCOME;
  protected readonly tones = Object.fromEntries(Object.values(AUDIT_OUTCOME).map(o => [o.label, o.tone]));

  protected readonly period = computed<AuditPeriod>(() => (AUDIT_PERIODS.some(p => p.id === this.periodo()) ? (this.periodo() as AuditPeriod) : '24h'));
  protected readonly stageName = computed(() => stageLabel(this.stageFilter.stage()));
  protected readonly oldest = computed(() => (this.access.summary()?.oldestEventAt ? formatDateTime(this.access.summary()!.oldestEventAt!) : null));
  protected readonly noAccess = computed(() => this.access.summary() !== null && !this.access.canRead());

  protected readonly columns: GridColumn<AuditRow>[] = [
    { field: 'time', header: 'Horário', type: 'datetime', width: 160, sortable: false, locked: true },
    { field: 'outcome', header: 'Resultado', type: 'status', width: 120, sortable: false, groupable: true },
    { field: 'action', header: 'Ação', width: 240, key: true, sortable: false, locked: true, groupable: true },
    { field: 'actor', header: 'Ator', width: 170, sortable: false, groupable: true },
    { field: 'host', header: 'Host', width: 160, sortable: false, filterable: true, groupable: true },
    { field: 'target', header: 'Alvo', width: 180, mono: true, sortable: false },
    { field: 'category', header: 'Categoria', width: 150, sortable: false, groupable: true },
  ];

  protected readonly rows = computed<AuditRow[]>(() =>
    this.facade.events().map(event => ({
      id: event.id,
      time: event.occurredAt,
      outcome: AUDIT_OUTCOME[event.outcome].label,
      action: auditActionLabel(event.action),
      actor: auditActorLabel(event),
      host: event.environmentName ?? '—',
      target: event.subjectId ? (event.subjectId.length > 24 ? event.subjectId.slice(0, 12) : event.subjectId) : '—',
      category: AUDIT_CATEGORIES.find(c => c.id === event.category)?.label ?? '—',
      event,
    })),
  );

  protected readonly facets = computed<Record<string, GridFacet[]>>(() => ({
    host: this.directory
      .environments()
      .filter(env => !this.stageFilter.stage() || env.stage === this.stageFilter.stage())
      .map(env => ({ value: env.environmentId, label: env.name })),
  }));
  protected readonly gridFilters = computed<Record<string, string[]>>(() => ({ host: this.facade.filters()?.environmentIds ?? [] }));

  protected readonly filterChips = computed(() => {
    const f = this.facade.filters();
    const chips: { key: 'categoria' | 'resultado' | 'ator' | 'host' | 'q'; field: string; label: string }[] = [];
    if (!f) return chips;
    if (f.categories.length) chips.push({ key: 'categoria', field: 'Categoria', label: f.categories.map(c => AUDIT_CATEGORIES.find(x => x.id === c)?.label ?? c).join(', ') });
    if (f.outcomes.length) chips.push({ key: 'resultado', field: 'Resultado', label: f.outcomes.map(o => AUDIT_OUTCOME[o].label).join(', ') });
    if (f.actor) chips.push({ key: 'ator', field: 'Ator', label: f.actor });
    if (f.environmentIds.length) chips.push({ key: 'host', field: 'Host', label: f.environmentIds.map(id => this.directory.byId(id)?.name ?? id.slice(0, 8)).join(', ') });
    if (f.text) chips.push({ key: 'q', field: 'Alvo ou correlação', label: f.text.length > 24 ? f.text.slice(0, 12) + '…' : f.text });
    return chips;
  });

  protected readonly updatedAt = computed(() => (this.facade.updatedAt() ? formatDateTime(this.facade.updatedAt()) : null));

  constructor() {
    inject(ShellContextService).set('Auditoria', ['Governança', 'Auditoria']);
    void this.directory.load();
    effect(() => {
      const range = periodRange(this.period(), this.desde(), this.ate());
      const filters = range && {
        stage: this.stageFilter.stage(),
        since: range.since,
        until: range.until,
        environmentIds: asArray(this.environmentId()).filter(id => /^[0-9a-f-]{36}$/i.test(id)),
        actor: this.ator()?.trim() || null,
        categories: asArray(this.categoria()).filter((c): c is AuditCategory => AUDIT_CATEGORIES.some(x => x.id === c)),
        outcomes: asArray(this.resultado()).filter((o): o is AuditOutcome => OUTCOMES.includes(o as AuditOutcome)),
        text: this.q()?.trim() || null,
      };
      untracked(() => {
        this.facade.apply(filters);
        this.search.set(filters?.text ?? '');
        this.actorDraft.set(filters?.actor ?? '');
      });
    });
    effect(() => {
      const search = this.search();
      untracked(() => {
        if (search !== (this.q() ?? '') && (search.length === 0 || search.trim().length >= 2)) this.navigate({ q: search || null });
      });
    });
  }

  protected selectPeriod(period: string): void {
    this.navigate(period === 'personalizado' ? { periodo: period } : { periodo: period === '24h' ? null : period, desde: null, ate: null });
  }

  protected setDay(key: 'desde' | 'ate', value: string): void {
    this.navigate({ periodo: 'personalizado', [key]: value || null });
  }

  protected applyActor(): void {
    const actor = this.actorDraft().trim();
    this.navigate({ ator: actor || null });
  }

  protected toggleCategory(category: AuditCategory): void {
    const current = this.facade.filters()?.categories ?? [];
    const next = current.includes(category) ? current.filter(c => c !== category) : [...current, category];
    this.navigate({ categoria: next.length ? next : null });
  }

  protected toggleOutcome(outcome: AuditOutcome): void {
    const current = this.facade.filters()?.outcomes ?? [];
    const next = current.includes(outcome) ? current.filter(o => o !== outcome) : [...current, outcome];
    this.navigate({ resultado: next.length ? next : null });
  }

  protected categoryActive(category: AuditCategory): boolean {
    return this.facade.filters()?.categories.includes(category) ?? false;
  }

  protected outcomeActive(outcome: AuditOutcome): boolean {
    return this.facade.filters()?.outcomes.includes(outcome) ?? false;
  }

  protected removeFilter(key: 'categoria' | 'resultado' | 'ator' | 'host' | 'q'): void {
    this.navigate(key === 'host' ? { environmentId: null } : { [key]: null });
  }

  protected onGridFilters(filters: Record<string, string[]>): void {
    const hosts = filters['host'] ?? [];
    this.navigate({ environmentId: hosts.length ? hosts : null });
  }

  protected openEvent(row: AuditRow): void {
    this.openFromView(row.event);
  }

  protected openFromView(event: AuditEventView): void {
    this.navigate({ evento: event.id, em: event.occurredAt }, false);
  }

  protected closeEvent(): void {
    this.navigate({ evento: null, em: null });
  }

  protected async exportCsv(): Promise<void> {
    const filters = this.facade.filters();
    if (!filters) return;
    this.exporting.set(true);
    try {
      const { blob, fileName } = await this.api.exportAuditEvents(auditQuery(filters));
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = fileName;
      link.click();
      setTimeout(() => URL.revokeObjectURL(url), 1000);
      this.toast.notify('success', { message: 'Exportação concluída. A exportação fica registrada na auditoria.' });
    } catch (error) {
      const problem = error as { status?: number };
      this.toast.error(
        problem.status === 400
          ? 'A exportação aceita até 10.000 eventos. Reduza o período ou os filtros.'
          : problem.status === 403
            ? 'Você não tem permissão para exportar a auditoria destes hosts.'
            : 'Não foi possível exportar a auditoria.',
      );
    } finally {
      this.exporting.set(false);
    }
  }

  private navigate(params: Record<string, string | string[] | null>, replaceUrl = true): void {
    void this.router.navigate([], { queryParams: params, queryParamsHandling: 'merge', replaceUrl });
  }
}
