import { effect, inject, Injectable, signal, untracked } from '@angular/core';
import { ControlPlaneApiService } from '../../data-access/control-plane-api.service';
import type { HostMetricsPage, StageMetrics } from '../../data-access/generated/control-plane';
import { pollWhileVisible } from '../../data-access/metrics-poller';
import { StageFilterService } from '../../shell/stage-filter.service';

export type HostMetric = HostMetricsPage['hosts'][number];

/** Métricas da visão geral (spec 009 T013/T014): agregado do estágio (12 h, passo 1 h) e último valor por host. */
@Injectable()
export class OverviewMetricsFacade {
  private readonly api = inject(ControlPlaneApiService);
  private readonly stageFilter = inject(StageFilterService);
  private version = 0;

  readonly stage = signal<StageMetrics | null>(null);
  readonly hosts = signal<ReadonlyMap<string, HostMetric>>(new Map());
  readonly operationalLimit = signal(80);
  readonly failed = signal(false);

  private readonly poller = pollWhileVisible(() => this.load());

  constructor() {
    effect(() => {
      this.stageFilter.stage();
      untracked(() => this.poller.reload());
    });
  }

  private async load(): Promise<void> {
    const version = ++this.version;
    const stage = this.stageFilter.stage();
    try {
      const [stageMetrics, hostMetrics] = await Promise.all([this.api.getStageMetrics(stage, '12h', '1h'), this.api.listHostMetrics(stage)]);
      if (version !== this.version) return;
      this.stage.set(stageMetrics);
      this.hosts.set(new Map(hostMetrics.hosts.map(h => [h.environmentId, h])));
      this.operationalLimit.set(stageMetrics.operationalLimitPercent);
      this.failed.set(false);
    } catch {
      if (version === this.version) this.failed.set(true);
    }
  }
}
