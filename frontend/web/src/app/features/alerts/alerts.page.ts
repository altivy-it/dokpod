import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AlertSummaryService } from '../../core/alert-summary.service';
import { ConsoleRealtimeService } from '../../core/console-realtime.service';
import { EnvironmentDirectoryService } from '../../core/environment-directory.service';
import { JustificationPolicyService } from '../../core/justification-policy.service';
import { ALERT_RESOLUTION, ALERT_SEVERITIES, ALERT_SEVERITY, ALERT_STATE, describeRule, describeScope } from '../../data-access/alert-labels';
import { AlertsApiService } from './alerts-api.service';
import type { Alert, AlertRule, AlertSeverity, AlertSilence, AlertState, Stage } from '../../data-access/generated/control-plane';
import {
  DokButton,
  DokCheckbox,
  DokDataGrid,
  DokDrawer,
  DokEmptyState,
  DokIcon,
  DokStatusChip,
  DokTabs,
  formatDateTime,
  ToastService,
  type GridColumn,
  type GridFacet,
  type GridRowAction,
  type TabItem,
} from '../../design-system';
import { ShellContextService } from '../../shell/shell-context.service';
import { STAGES, StageFilterService, stageLabel } from '../../shell/stage-filter.service';
import { AlertDetailDrawer } from './alert-detail.drawer';
import { actionError, AlertsFacade, type AlertFilters, type AlertView } from './alerts.facade';
import { RuleDrawer } from './rule.drawer';
import { SilenceDrawer } from './silence.drawer';

const VIEWS: AlertView[] = ['abertos', 'historico', 'regras', 'silenciamentos'];
const OPEN_STATES: AlertState[] = ['open', 'acknowledged', 'silenced'];

interface AlertRow {
  id: string;
  severity: string;
  condition: string;
  rule: string;
  host: string;
  container: string;
  state: string;
  last: string;
  occurrences: number;
  resolvedAt: string | null;
  resolution: string;
  alert: Alert;
}

interface RuleRow {
  id: string;
  name: string;
  condition: string;
  severity: string;
  scope: string;
  status: string;
  origin: string;
  rule: AlertRule;
}

interface SilenceRow {
  id: string;
  scope: string;
  starts: string;
  ends: string;
  justification: string;
  createdBy: string;
  status: string;
  silence: AlertSilence;
}

function asArray(value: string | string[] | undefined): string[] {
  return value === undefined ? [] : Array.isArray(value) ? value : [value];
}

/** Data `yyyy-MM-dd` da URL como início (desde) ou fim do dia (até), em UTC local do navegador. */
export function dayBoundary(value: string | undefined, end: boolean): string | null {
  if (!value || !/^\d{4}-\d{2}-\d{2}$/.test(value)) return null;
  const date = new Date(`${value}T${end ? '23:59:59.999' : '00:00:00'}`);
  return Number.isNaN(date.getTime()) ? null : date.toISOString();
}

const TONES: Record<string, string> = {
  ...Object.fromEntries(Object.values(ALERT_SEVERITY).map(s => [s.label, s.tone])),
  ...Object.fromEntries(Object.values(ALERT_STATE).map(s => [s.label, s.tone])),
  Ativa: 'success',
  Inativa: 'neutral',
  Vigente: 'warning',
  Agendado: 'info',
  Encerrado: 'neutral',
};

/**
 * Governança → Alertas (spec 016 FR-007): vistas Abertos, Histórico, Regras e Silenciamentos com filtros na URL,
 * detalhe com linha do tempo e ações; política de justificativa por estágio na vista Regras.
 */
@Component({
  selector: 'app-alerts-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [AlertDetailDrawer, DokButton, DokCheckbox, DokDataGrid, DokDrawer, DokEmptyState, DokIcon, DokStatusChip, DokTabs, FormsModule, RuleDrawer, SilenceDrawer],
  providers: [AlertsFacade],
  styleUrl: './alerts.page.css',
  templateUrl: './alerts.page.html',
})
export class AlertsPage {
  readonly vista = input<string | undefined>();
  readonly severidade = input<string | string[] | undefined>();
  readonly estado = input<string | string[] | undefined>();
  readonly environmentId = input<string | string[] | undefined>();
  readonly q = input<string | undefined>();
  readonly regra = input<string | undefined>();
  readonly desde = input<string | undefined>();
  readonly ate = input<string | undefined>();
  readonly alerta = input<string | undefined>();

  protected readonly facade = inject(AlertsFacade);
  protected readonly stageFilter = inject(StageFilterService);
  protected readonly summary = inject(AlertSummaryService);
  protected readonly policy = inject(JustificationPolicyService);
  private readonly directory = inject(EnvironmentDirectoryService);
  private readonly realtime = inject(ConsoleRealtimeService);
  private readonly api = inject(AlertsApiService);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);

  protected readonly search = signal('');
  protected readonly ruleDrawer = signal<{ rule: AlertRule | null } | null>(null);
  protected readonly silenceDrawer = signal(false);
  protected readonly ruleToDelete = signal<AlertRule | null>(null);
  protected readonly deleting = signal(false);
  protected readonly policyBusy = signal<Stage | null>(null);
  protected readonly tones = TONES;
  protected readonly severities = ALERT_SEVERITIES;
  protected readonly openStates = OPEN_STATES;
  protected readonly stages = STAGES;
  protected readonly severityLabels = ALERT_SEVERITY;
  protected readonly stateLabels = ALERT_STATE;

  protected readonly view = computed<AlertView>(() => (VIEWS.includes(this.vista() as AlertView) ? (this.vista() as AlertView) : 'abertos'));
  protected readonly stageName = computed(() => stageLabel(this.stageFilter.stage()));
  protected readonly detailRefresh = computed(() => this.realtime.lastInvalidation()?.kind === 'alerts' ? this.realtime.lastInvalidation()!.sequence : 0);

  protected readonly tabs = computed<TabItem[]>(() => [
    { id: 'abertos', label: 'Abertos', icon: 'alert', count: this.summary.summary()?.open ?? null },
    { id: 'historico', label: 'Histórico', icon: 'clock' },
    { id: 'regras', label: 'Regras', icon: 'sliders' },
    { id: 'silenciamentos', label: 'Silenciamentos', icon: 'bell' },
  ]);

  protected readonly alertColumns = computed<GridColumn<AlertRow>[]>(() => {
    const history = this.view() === 'historico';
    return [
      { field: 'severity', header: 'Severidade', type: 'status', width: 120, sortable: false, locked: true },
      { field: 'condition', header: 'Condição', width: 360, key: true, sortable: false, locked: true },
      { field: 'host', header: 'Host', width: 150, sortable: false, filterable: true, groupable: true },
      { field: 'container', header: 'Container', width: 170, sortable: false, mono: true },
      ...(history
        ? [
            { field: 'resolvedAt', header: 'Resolvido em', type: 'datetime', width: 150, sortable: false } as GridColumn<AlertRow>,
            { field: 'resolution', header: 'Resolução', width: 180, sortable: false } as GridColumn<AlertRow>,
          ]
        : [{ field: 'state', header: 'Situação', type: 'status', width: 130, sortable: false, groupable: true } as GridColumn<AlertRow>]),
      { field: 'last', header: 'Última ocorrência', type: 'datetime', width: 150, sortable: false },
      { field: 'occurrences', header: 'Ocorrências', type: 'number', width: 110, sortable: false },
      { field: 'rule', header: 'Regra', width: 220, sortable: false, groupable: true },
    ];
  });

  protected readonly alertRows = computed<AlertRow[]>(() =>
    this.facade.alerts().map(alert => ({
      id: alert.id,
      severity: ALERT_SEVERITY[alert.severity].label,
      condition: alert.condition,
      rule: alert.ruleName,
      host: this.hostName(alert.environmentId),
      container: alert.containerName ?? (alert.containerId ? alert.containerId.slice(0, 12) : '—'),
      state: ALERT_STATE[alert.state].label,
      last: alert.lastOccurredAt,
      occurrences: alert.occurrences,
      resolvedAt: alert.resolvedAt ?? null,
      resolution: alert.resolution ? (ALERT_RESOLUTION[alert.resolution] ?? alert.resolution) : '—',
      alert,
    })),
  );

  protected readonly ruleColumns: GridColumn<RuleRow>[] = [
    { field: 'name', header: 'Regra', width: 240, key: true, locked: true },
    { field: 'condition', header: 'Condição', width: 320, sortable: false },
    { field: 'severity', header: 'Severidade', type: 'status', width: 120, filterable: true },
    { field: 'scope', header: 'Abrangência', width: 220, sortable: false },
    { field: 'status', header: 'Situação', type: 'status', width: 110, filterable: true },
    { field: 'origin', header: 'Origem', width: 130, filterable: true },
  ];

  protected readonly ruleRows = computed<RuleRow[]>(() =>
    this.facade.rules().map(rule => ({
      id: rule.id,
      name: rule.name,
      condition: describeRule(rule),
      severity: ALERT_SEVERITY[rule.severity].label,
      scope: describeScope(rule.scope, id => this.directory.byId(id)?.name),
      status: rule.enabled ? 'Ativa' : 'Inativa',
      origin: rule.builtIn ? 'Padrão' : 'Personalizada',
      rule,
    })),
  );

  protected readonly silenceColumns: GridColumn<SilenceRow>[] = [
    { field: 'scope', header: 'Abrangência', width: 260, key: true, locked: true, sortable: false },
    { field: 'status', header: 'Situação', type: 'status', width: 110 },
    { field: 'starts', header: 'Início', type: 'datetime', width: 150 },
    { field: 'ends', header: 'Fim', type: 'datetime', width: 150 },
    { field: 'justification', header: 'Justificativa', width: 320, sortable: false },
    { field: 'createdBy', header: 'Autor', width: 160 },
  ];

  protected readonly silenceRows = computed<SilenceRow[]>(() => {
    const now = Date.now();
    return this.facade.silences().map(silence => {
      const ended = silence.endedAt ?? (Date.parse(silence.endsAt) <= now ? silence.endsAt : null);
      return {
        id: silence.id,
        scope: silence.alertId ? 'Alerta específico' : describeScope(silence.scope, id => this.directory.byId(id)?.name),
        starts: silence.startsAt,
        ends: silence.endedAt ?? silence.endsAt,
        justification: silence.justification,
        createdBy: silence.createdBy,
        status: ended ? 'Encerrado' : Date.parse(silence.startsAt) > now ? 'Agendado' : 'Vigente',
        silence,
      };
    });
  });

  protected readonly facets = computed<Record<string, GridFacet[]>>(() => ({
    host: this.directory
      .environments()
      .filter(env => !this.stageFilter.stage() || env.stage === this.stageFilter.stage())
      .map(env => ({ value: env.environmentId, label: env.name })),
  }));

  protected readonly gridFilters = computed<Record<string, string[]>>(() => ({ host: this.facade.filters()?.environmentIds ?? [] }));

  protected readonly policies = computed(() =>
    STAGES.map(stage => {
      const hosts = this.directory.environments().filter(env => env.stage === stage.id && env.enabled);
      const candidates = hosts.length ? hosts : this.directory.environments().filter(env => env.enabled);
      const canEdit = candidates.length > 0 && candidates.every(env => (env.scopes as string[]).includes('environment:manage'));
      return { stage: stage.id, label: stage.label, required: this.policy.required().has(stage.id), canEdit };
    }),
  );

  protected readonly filterChips = computed(() => {
    const f = this.facade.filters();
    const chips: { key: 'severidade' | 'estado' | 'host' | 'regra' | 'periodo'; field: string; label: string }[] = [];
    if (!f) return chips;
    if (f.severities.length) chips.push({ key: 'severidade', field: 'Severidade', label: f.severities.map(s => ALERT_SEVERITY[s].label).join(', ') });
    if (f.states.length) chips.push({ key: 'estado', field: 'Situação', label: f.states.map(s => ALERT_STATE[s].label).join(', ') });
    if (f.environmentIds.length) chips.push({ key: 'host', field: 'Host', label: f.environmentIds.map(id => this.hostName(id)).join(', ') });
    if (f.ruleId) chips.push({ key: 'regra', field: 'Regra', label: this.facade.rules().find(r => r.id === f.ruleId)?.name ?? 'regra selecionada' });
    if (f.since || f.until) chips.push({ key: 'periodo', field: 'Período', label: `${this.desde() ?? '…'} a ${this.ate() ?? 'hoje'}` });
    return chips;
  });

  protected readonly updatedAt = computed(() => (this.facade.updatedAt() ? formatDateTime(this.facade.updatedAt()) : null));
  protected readonly emptyTitle = computed(() =>
    this.view() === 'historico' ? 'Nenhum alerta resolvido no período' : this.filterChips().length || this.search() ? 'Nenhum alerta com estes filtros' : 'Nenhum alerta aberto',
  );

  constructor() {
    inject(ShellContextService).set('Alertas', ['Governança', 'Alertas']);
    void this.directory.load();
    void this.policy.load();
    effect(() => {
      const view = this.view();
      const filters: AlertFilters = {
        view,
        stage: this.stageFilter.stage(),
        severities: asArray(this.severidade()).filter((s): s is AlertSeverity => s in ALERT_SEVERITY),
        states: view === 'abertos' ? asArray(this.estado()).filter((s): s is AlertState => OPEN_STATES.includes(s as AlertState)) : [],
        environmentIds: asArray(this.environmentId()).filter(id => /^[0-9a-f-]{36}$/i.test(id)),
        text: this.q()?.trim() || null,
        ruleId: this.regra() && /^[0-9a-f-]{36}$/i.test(this.regra()!) ? this.regra()! : null,
        since: view === 'historico' ? dayBoundary(this.desde(), false) : null,
        until: view === 'historico' ? dayBoundary(this.ate(), true) : null,
      };
      untracked(() => {
        this.facade.apply(filters);
        this.search.set(filters.text ?? '');
      });
    });
    effect(() => {
      const search = this.search();
      untracked(() => {
        if (search !== (this.q() ?? '') && (search.length === 0 || search.trim().length >= 2)) this.navigate({ q: search || null });
      });
    });
  }

  protected hostName(environmentId: string): string {
    return this.directory.byId(environmentId)?.name ?? 'host';
  }

  protected selectView(view: string): void {
    this.navigate({ vista: view === 'abertos' ? null : view, estado: null, desde: null, ate: null });
  }

  protected toggleSeverity(severity: AlertSeverity): void {
    const current = this.facade.filters()?.severities ?? [];
    const next = current.includes(severity) ? current.filter(s => s !== severity) : [...current, severity];
    this.navigate({ severidade: next.length ? next : null });
  }

  protected toggleState(state: AlertState): void {
    const current = this.facade.filters()?.states ?? [];
    const next = current.includes(state) ? current.filter(s => s !== state) : [...current, state];
    this.navigate({ estado: next.length ? next : null });
  }

  protected severityActive(severity: AlertSeverity): boolean {
    return this.facade.filters()?.severities.includes(severity) ?? false;
  }

  protected stateActive(state: AlertState): boolean {
    return this.facade.filters()?.states.includes(state) ?? false;
  }

  protected severityCount(severity: AlertSeverity): number | null {
    return this.view() === 'abertos' ? (this.summary.summary()?.bySeverity[severity] ?? 0) : null;
  }

  protected setPeriod(key: 'desde' | 'ate', value: string): void {
    this.navigate({ [key]: value || null });
  }

  protected removeFilter(key: 'severidade' | 'estado' | 'host' | 'regra' | 'periodo'): void {
    if (key === 'host') this.navigate({ environmentId: null });
    else if (key === 'periodo') this.navigate({ desde: null, ate: null });
    else this.navigate({ [key]: null });
  }

  protected onGridFilters(filters: Record<string, string[]>): void {
    const hosts = filters['host'] ?? [];
    this.navigate({ environmentId: hosts.length ? hosts : null });
  }

  protected openAlert(row: AlertRow): void {
    this.navigate({ alerta: row.id }, false);
  }

  protected closeAlert(): void {
    this.navigate({ alerta: null });
  }

  protected alertChanged(): void {
    this.summary.schedule();
    void this.facade.reload(true);
  }

  protected readonly ruleActionsFor = (row: RuleRow): GridRowAction[] => {
    if (!this.facade.canManageRules()) return [{ id: 'alerts', label: 'Ver alertas da regra', icon: 'alert' }];
    const actions: GridRowAction[] = [
      { id: 'edit', label: 'Editar', icon: 'edit' },
      { id: 'toggle', label: row.rule.enabled ? 'Desativar' : 'Ativar', icon: row.rule.enabled ? 'pause' : 'play' },
      { id: 'alerts', label: 'Ver alertas da regra', icon: 'alert' },
    ];
    if (!row.rule.builtIn) actions.push({ id: 'delete', label: 'Excluir', icon: 'trash', danger: true });
    return actions;
  };

  protected readonly ruleLabel = (row: RuleRow) => row.name;

  protected async onRuleAction(event: { action: string; row: RuleRow }): Promise<void> {
    const rule = event.row.rule;
    switch (event.action) {
      case 'edit':
        this.ruleDrawer.set({ rule });
        return;
      case 'alerts':
        this.navigate({ vista: 'historico', regra: rule.id });
        return;
      case 'toggle':
        try {
          await this.api.saveAlertRule(rule.id, {
            name: rule.name,
            description: rule.description ?? null,
            conditionKind: rule.conditionKind,
            eventType: rule.eventType ?? null,
            metric: rule.metric ?? null,
            thresholdPercent: rule.thresholdPercent ?? null,
            durationSeconds: rule.durationSeconds,
            minOccurrences: rule.minOccurrences,
            severity: rule.severity,
            scope: rule.scope,
            enabled: !rule.enabled,
            version: rule.version,
          });
          this.toast.notify('success', { message: `Regra "${rule.name}" ${rule.enabled ? 'desativada' : 'ativada'}.` });
          await this.facade.reload(true);
        } catch (error) {
          this.toast.error(actionError(error));
        }
        return;
      case 'delete':
        this.ruleToDelete.set(rule);
    }
  }

  protected async deleteRule(): Promise<void> {
    const rule = this.ruleToDelete();
    if (!rule) return;
    this.deleting.set(true);
    try {
      await this.api.deleteAlertRule(rule.id);
      this.ruleToDelete.set(null);
      this.toast.notify('success', { message: `Regra "${rule.name}" excluída.` });
      this.summary.schedule();
      await this.facade.reload(true);
    } catch (error) {
      this.toast.error(actionError(error));
    } finally {
      this.deleting.set(false);
    }
  }

  protected openRule(row: RuleRow): void {
    if (this.facade.canManageRules()) this.ruleDrawer.set({ rule: row.rule });
  }

  protected ruleSaved(): void {
    this.ruleDrawer.set(null);
    this.toast.notify('success', { message: 'Regra salva. Vale para as próximas avaliações.' });
    void this.facade.reload(true);
  }

  protected readonly silenceActionsFor = (row: SilenceRow): GridRowAction[] =>
    this.facade.canManageSilences() && row.status !== 'Encerrado' ? [{ id: 'end', label: 'Encerrar agora', icon: 'x', danger: true }] : [];
  protected readonly silenceLabel = (row: SilenceRow) => row.scope;

  protected async onSilenceAction(event: { action: string; row: SilenceRow }): Promise<void> {
    if (event.action !== 'end') return;
    try {
      await this.api.endAlertSilence(event.row.id);
      this.toast.notify('success', { message: 'Silenciamento encerrado. Alertas com a condição ativa voltam a abertos.' });
      this.summary.schedule();
      await this.facade.reload(true);
    } catch (error) {
      this.toast.error(actionError(error));
    }
  }

  protected silenceSaved(): void {
    this.silenceDrawer.set(false);
    this.toast.notify('success', { message: 'Silenciamento criado.' });
    this.summary.schedule();
    void this.facade.reload(true);
  }

  protected async setPolicy(stage: Stage, required: boolean): Promise<void> {
    this.policyBusy.set(stage);
    try {
      await this.policy.set(stage, required);
      this.toast.notify('success', {
        message: `${stageLabel(stage)}: justificativa ${required ? 'obrigatória' : 'opcional'} para parar, pausar e remover.`,
      });
    } catch (error) {
      this.toast.error(actionError(error));
    } finally {
      this.policyBusy.set(null);
    }
  }

  private navigate(params: Record<string, string | string[] | null>, replaceUrl = true): void {
    void this.router.navigate([], { queryParams: params, queryParamsHandling: 'merge', replaceUrl });
  }
}
