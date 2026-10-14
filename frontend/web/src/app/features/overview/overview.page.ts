import { ChangeDetectionStrategy, Component, computed, effect, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CONTAINER_STATUS, describeEvent, ENGINE_LABEL, HOST_SITUATION } from '../../data-access/console-labels';
import type { EnvironmentState } from '../../data-access/generated/control-plane';
import {
  DokButton,
  DokCard,
  DokDonutChart,
  DokEmptyState,
  DokEngineBadge,
  DokInfoKpi,
  DokLineChart,
  DokStatusChip,
  DokStatusCircle,
  formatDateTime,
  formatNumber,
  formatTime,
} from '../../design-system';
import { ShellContextService } from '../../shell/shell-context.service';
import { StageFilterService, stageLabel } from '../../shell/stage-filter.service';
import { OverviewFacade } from './overview.facade';

const STAGE_EYEBROW: Record<string, string> = {
  production: 'Cluster de produção',
  staging: 'Cluster de homologação',
  development: 'Cluster de desenvolvimento',
};

/** Visão geral (board `01 · Visão geral`, spec 008 US1). */
@Component({
  selector: 'app-overview-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DokButton,
    DokCard,
    DokDonutChart,
    DokEmptyState,
    DokEngineBadge,
    DokInfoKpi,
    DokLineChart,
    DokStatusChip,
    DokStatusCircle,
    RouterLink,
  ],
  providers: [OverviewFacade],
  styleUrl: './overview.page.css',
  templateUrl: './overview.page.html',
})
export class OverviewPage {
  protected readonly facade = inject(OverviewFacade);
  private readonly stageFilter = inject(StageFilterService);
  private readonly context = inject(ShellContextService);
  private readonly router = inject(Router);

  protected readonly eyebrow = computed(() => {
    const stage = this.stageFilter.stage();
    return stage ? STAGE_EYEBROW[stage] : 'Todos os estágios';
  });

  protected readonly headline = computed(() => {
    const summary = this.facade.summary();
    if (!summary) return 'Visão geral';
    const hosts = summary.hosts.total;
    const containers = summary.containers.total;
    return `${formatNumber(hosts)} ${hosts === 1 ? 'host' : 'hosts'}, ${formatNumber(containers)} containers sob gestão`;
  });

  protected readonly engineLine = computed(() => {
    const summary = this.facade.summary();
    if (!summary) return '';
    const parts = summary.engines.map(engine => {
      const version = engine.versions[0]?.version ?? '';
      return `${ENGINE_LABEL[engine.engine] ?? engine.engine} ${version} em ${engine.hosts} ${engine.hosts === 1 ? 'host' : 'hosts'}`.replace('  ', ' ');
    });
    const updated = summary.observedAt ? ` · atualizado em ${formatDateTime(summary.observedAt)}` : '';
    return `${parts.join(' e ') || 'Nenhum agente conectado'}${updated}`;
  });

  protected readonly kpis = computed(() => {
    const byStatus = this.facade.summary()?.containers.byDisplayStatus ?? {};
    const value = (key: string) => (this.facade.summary() ? byStatus[key] ?? 0 : null);
    return {
      running: value('running'),
      stopped: value('stopped'),
      failing: this.facade.summary() ? (byStatus['failed'] ?? 0) + (byStatus['restarting'] ?? 0) : null,
    };
  });

  protected readonly engines = computed(() =>
    (this.facade.summary()?.engines ?? []).map(engine => ({
      label: ENGINE_LABEL[engine.engine] ?? engine.engine,
      value: engine.containers,
      color: engine.engine === 'podman' ? 'var(--chart-5)' : 'var(--chart-2)',
    })),
  );

  protected readonly hostRows = computed(() =>
    [...this.facade.hosts()]
      .filter(host => host.enabled)
      .sort((a, b) => a.name.localeCompare(b.name, 'pt-BR'))
      .slice(0, 8)
      .map(host => ({
        host,
        situation: HOST_SITUATION[host.situation ?? 'unknown'],
      })),
  );

  protected readonly eventRows = computed(() =>
    this.facade.events().map(event => ({
      id: event.eventId,
      time: formatTime(event.occurredAt),
      skew: event.clockSkewDetected,
      ...describeEvent(event),
    })),
  );

  protected readonly attention = computed(() => this.facade.summary()?.attentionEvents24h ?? 0);
  protected readonly empty = computed(() => !this.facade.loading() && !this.facade.error() && this.facade.hosts().length === 0);
  protected readonly stageName = computed(() => stageLabel(this.stageFilter.stage()));
  protected readonly statusLabels = CONTAINER_STATUS;

  constructor() {
    effect(() => {
      this.context.set('Visão geral', ['Operação', 'Visão geral']);
      this.context.notifications.set(this.attention() || null);
      this.context.alertCount.set(this.attention() || null);
    });
  }

  protected openHosts(): void {
    void this.router.navigate(['/hosts'], { queryParamsHandling: 'merge' });
  }

  protected hostTitle(host: EnvironmentState): string {
    return host.agent?.hostName && host.agent.hostName !== host.name ? `${host.name} (${host.agent.hostName})` : host.name;
  }
}
