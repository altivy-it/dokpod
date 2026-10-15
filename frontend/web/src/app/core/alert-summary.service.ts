import { effect, inject, Injectable, signal, untracked } from '@angular/core';
import { ControlPlaneApiService } from '../data-access/control-plane-api.service';
import type { AlertSummary } from '../data-access/generated/control-plane';
import { ShellContextService } from '../shell/shell-context.service';
import { StageFilterService } from '../shell/stage-filter.service';
import { ConsoleRealtimeService } from './console-realtime.service';
import { EnvironmentDirectoryService } from './environment-directory.service';
import { SessionService } from './session.service';

const REFRESH_DEBOUNCE_MS = 500;
/** Rede de segurança quando o tempo real cai: o resumo é refeito a cada minuto. */
const FALLBACK_REFRESH_MS = 60_000;

/**
 * Alertas abertos do estágio selecionado (spec 016 FR-006): alimenta o contador do menu "Alertas" e o sino.
 * Recarrega ao trocar o estágio, a cada invalidação `alerts` do hub e, sem tempo real, a cada minuto.
 */
@Injectable({ providedIn: 'root' })
export class AlertSummaryService {
  private readonly api = inject(ControlPlaneApiService);
  private readonly context = inject(ShellContextService);
  private readonly stageFilter = inject(StageFilterService);
  private readonly realtime = inject(ConsoleRealtimeService);
  private readonly directory = inject(EnvironmentDirectoryService);
  private readonly session = inject(SessionService);

  readonly summary = signal<AlertSummary | null>(null);
  readonly error = signal(false);
  private timer: ReturnType<typeof setTimeout> | null = null;
  private readonly interval: ReturnType<typeof setInterval>;

  /** Criado pelo shell (injeção); os efeitos vivem enquanto a aplicação. */
  constructor() {
    effect(() => {
      const stage = this.stageFilter.stage();
      if (this.session.state() !== 'authenticated') return;
      untracked(() => void this.refresh(stage));
    });
    effect(() => {
      const ids = this.directory.environments().map(e => e.environmentId);
      if (ids.length) untracked(() => void this.realtime.watch(ids));
    });
    effect(() => {
      const invalidation = this.realtime.lastInvalidation();
      if (invalidation?.kind === 'alerts') untracked(() => this.schedule());
    });
    this.interval = setInterval(() => {
      if (this.realtime.status() !== 'connected') void this.refresh(this.stageFilter.stage());
    }, FALLBACK_REFRESH_MS);
  }

  /** Atualização pedida por quem acabou de mudar um alerta (reconhecer, resolver, silenciar). */
  schedule(): void {
    if (this.timer) clearTimeout(this.timer);
    this.timer = setTimeout(() => {
      this.timer = null;
      void this.refresh(this.stageFilter.stage());
    }, REFRESH_DEBOUNCE_MS);
  }

  async refresh(stage = this.stageFilter.stage()): Promise<void> {
    try {
      const summary = await this.api.getAlertSummary(stage);
      this.summary.set(summary);
      this.error.set(false);
      this.context.alertCount.set(summary.open || null);
      this.context.notifications.set(summary.open || null);
    } catch {
      this.error.set(true);
    }
  }

  stop(): void {
    clearInterval(this.interval);
    if (this.timer) clearTimeout(this.timer);
  }
}
