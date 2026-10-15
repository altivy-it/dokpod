import { effect, inject, Injectable, signal, untracked } from '@angular/core';
import { ConsoleRealtimeService } from '../../core/console-realtime.service';
import { ControlPlaneApiService } from '../../data-access/control-plane-api.service';
import type { Alert, AlertRule, AlertSeverity, AlertSilence, AlertState, Stage } from '../../data-access/generated/control-plane';

export type AlertView = 'abertos' | 'historico' | 'regras' | 'silenciamentos';

export interface AlertFilters {
  view: AlertView;
  stage: Stage | null;
  severities: AlertSeverity[];
  states: AlertState[];
  environmentIds: string[];
  text: string | null;
  ruleId: string | null;
  since: string | null;
  until: string | null;
}

export interface LoadError {
  status: number | null;
  title: string;
  detail: string;
}

const PAGE_SIZE = 50;

export function loadError(error: unknown, what: string): LoadError {
  const status = (error as { status?: number })?.status ?? null;
  if (status === 403) return { status, title: 'Sem permissão', detail: `Você não tem acesso a ${what}.` };
  return { status, title: `Não foi possível carregar ${what}`, detail: 'Tente novamente em instantes.' };
}

/** Mensagem do problema devolvido pela API nas ações de governança. */
export function actionError(error: unknown): string {
  const problem = error as { status?: number; code?: string; detail?: string };
  if (problem?.status === 403) return 'Você não tem permissão para esta ação (alert:manage em todos os hosts abrangidos).';
  if (problem?.code === 'alert_resolved') return 'O alerta já foi resolvido.';
  if (problem?.code === 'version_conflict') return 'A regra foi alterada por outra pessoa. Recarregue e tente de novo.';
  if (problem?.code === 'built_in_rule') return 'Regras padrão não podem ser excluídas; desative-as.';
  if (problem?.status === 400) return 'Dados inválidos. Revise os campos e tente de novo.';
  return problem?.detail ?? 'Não foi possível concluir a ação.';
}

/**
 * Estado da tela Alertas (spec 016 FR-007): listas de abertos e histórico paginadas no servidor,
 * regras e silenciamentos; invalidações `alerts` do hub recarregam a vista atual.
 */
@Injectable()
export class AlertsFacade {
  private readonly api = inject(ControlPlaneApiService);
  private readonly realtime = inject(ConsoleRealtimeService);

  readonly filters = signal<AlertFilters | null>(null);
  readonly alerts = signal<Alert[]>([]);
  readonly nextCursor = signal<string | null>(null);
  readonly loading = signal(false);
  readonly loadingMore = signal(false);
  readonly error = signal<LoadError | null>(null);
  readonly updatedAt = signal<string | null>(null);

  readonly rules = signal<AlertRule[]>([]);
  readonly canManageRules = signal(false);
  readonly silences = signal<AlertSilence[]>([]);
  readonly canManageSilences = signal(false);
  readonly showEndedSilences = signal(false);

  private generation = 0;
  private refreshTimer: ReturnType<typeof setTimeout> | null = null;

  constructor() {
    effect(() => {
      const invalidation = this.realtime.lastInvalidation();
      if (invalidation?.kind !== 'alerts') return;
      untracked(() => {
        if (this.refreshTimer) clearTimeout(this.refreshTimer);
        this.refreshTimer = setTimeout(() => void this.reload(true), 400);
      });
    });
  }

  apply(filters: AlertFilters): void {
    const previous = this.filters();
    if (previous && JSON.stringify(previous) === JSON.stringify(filters)) return;
    this.filters.set(filters);
    void this.reload();
  }

  async reload(silent = false): Promise<void> {
    const filters = this.filters();
    if (!filters) return;
    const generation = ++this.generation;
    if (!silent) this.loading.set(true);
    this.error.set(null);
    try {
      switch (filters.view) {
        case 'regras': {
          const page = await this.api.listAlertRules();
          if (generation !== this.generation) return;
          this.rules.set(page.rules);
          this.canManageRules.set(page.canManage);
          break;
        }
        case 'silenciamentos': {
          const page = await this.api.listAlertSilences(!this.showEndedSilences());
          if (generation !== this.generation) return;
          this.silences.set(page.silences);
          this.canManageSilences.set(page.canManage);
          // Regras para descrever o alerta silenciado pelo nome da regra.
          if (!this.rules().length) this.rules.set((await this.api.listAlertRules()).rules);
          break;
        }
        default: {
          const page = await this.api.listAlerts(this.query(filters, null));
          if (generation !== this.generation) return;
          this.alerts.set(page.alerts);
          this.nextCursor.set(page.nextCursor ?? null);
        }
      }
      this.updatedAt.set(new Date().toISOString());
    } catch (error) {
      if (generation !== this.generation) return;
      const what = filters.view === 'regras' ? 'as regras' : filters.view === 'silenciamentos' ? 'os silenciamentos' : 'os alertas';
      this.error.set(loadError(error, what));
    } finally {
      if (generation === this.generation) this.loading.set(false);
    }
  }

  async loadMore(): Promise<void> {
    const filters = this.filters();
    const cursor = this.nextCursor();
    if (!filters || !cursor || this.loadingMore()) return;
    this.loadingMore.set(true);
    try {
      const page = await this.api.listAlerts(this.query(filters, cursor));
      this.alerts.set([...this.alerts(), ...page.alerts]);
      this.nextCursor.set(page.nextCursor ?? null);
    } catch (error) {
      this.error.set(loadError(error, 'mais alertas'));
    } finally {
      this.loadingMore.set(false);
    }
  }

  toggleEndedSilences(): void {
    this.showEndedSilences.set(!this.showEndedSilences());
    void this.reload();
  }

  private query(filters: AlertFilters, cursor: string | null) {
    return {
      view: filters.view === 'historico' ? ('history' as const) : ('open' as const),
      ...(filters.stage ? { stage: filters.stage } : {}),
      ...(filters.severities.length ? { severity: filters.severities } : {}),
      ...(filters.states.length ? { state: filters.states } : {}),
      ...(filters.environmentIds.length ? { environmentId: filters.environmentIds } : {}),
      ...(filters.text ? { q: filters.text } : {}),
      ...(filters.ruleId ? { ruleId: filters.ruleId } : {}),
      ...(filters.since ? { since: filters.since } : {}),
      ...(filters.until ? { until: filters.until } : {}),
      ...(cursor ? { cursor } : {}),
      limit: PAGE_SIZE,
    };
  }
}
