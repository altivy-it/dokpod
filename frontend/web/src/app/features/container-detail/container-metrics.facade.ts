import { computed, inject, Injectable, signal } from '@angular/core';
import { ControlPlaneApiService } from '../../data-access/control-plane-api.service';
import type { ContainerMetrics } from '../../data-access/generated/control-plane';
import { pollWhileVisible } from '../../data-access/metrics-poller';
import { formatBytes, formatPercent, formatTime } from '../../design-system';

export interface ResourceRow {
  label: string;
  value: string;
  percent: number | null;
  warn: boolean;
}

/** Métricas do container (spec 009 T016 para a spec 011): "Recursos agora" e série de 30 min. */
@Injectable()
export class ContainerMetricsFacade {
  private readonly api = inject(ControlPlaneApiService);
  private target: { environmentId: string; containerId: string } | null = null;
  private version = 0;

  readonly metrics = signal<ContainerMetrics | null>(null);
  readonly failed = signal(false);
  private readonly poller = pollWhileVisible(() => this.load());

  readonly limit = computed(() => this.metrics()?.operationalLimitPercent ?? 80);

  readonly resources = computed<ResourceRow[] | null>(() => {
    const current = this.metrics()?.current;
    if (!current) return null;
    const limit = this.limit();
    const cpu = current.cpuPercent ?? null;
    const memoryPercent = current.memoryUsedBytes != null && current.memoryLimitBytes ? (current.memoryUsedBytes / current.memoryLimitBytes) * 100 : null;
    const rate = (value: number | null | undefined) => (value == null ? '—' : `${formatBytes(value)}/s`);
    return [
      { label: 'CPU', value: formatPercent(cpu, 1), percent: cpu, warn: cpu !== null && cpu >= limit },
      {
        label: 'Memória',
        value: current.memoryUsedBytes == null ? '—' : current.memoryLimitBytes ? `${formatBytes(current.memoryUsedBytes)} de ${formatBytes(current.memoryLimitBytes)}` : formatBytes(current.memoryUsedBytes),
        percent: memoryPercent,
        warn: memoryPercent !== null && memoryPercent >= limit,
      },
      { label: 'Rede · entrada / saída', value: `${rate(current.netRxBps)} · ${rate(current.netTxBps)}`, percent: null, warn: false },
      { label: 'Disco · leitura / escrita', value: `${rate(current.blkReadBps)} · ${rate(current.blkWriteBps)}`, percent: null, warn: false },
    ];
  });

  readonly chart = computed(() => {
    const metrics = this.metrics();
    const series = metrics?.series;
    const samples = series ? series.cpu.filter(v => v !== null).length : 0;
    return {
      x: series?.x.map(value => formatTime(value)) ?? [],
      series: series
        ? [
            { name: 'CPU', data: series.cpu, area: true },
            { name: 'Memória', data: series.memory },
          ]
        : [],
      reference: { value: this.limit(), label: `Limite ${formatPercent(this.limit(), 0)}` },
      partial: !!metrics?.current && samples > 0 && samples < (series?.x.length ?? 0) - 1,
      empty: samples === 0 && !metrics?.current,
    };
  });

  watch(environmentId: string, containerId: string): void {
    this.target = { environmentId, containerId };
    this.metrics.set(null);
    this.poller.reload();
  }

  private async load(): Promise<void> {
    const target = this.target;
    if (!target) return;
    const version = ++this.version;
    try {
      const metrics = await this.api.getContainerMetrics(target.environmentId, target.containerId, '30m', '1m');
      if (version !== this.version) return;
      this.metrics.set(metrics);
      this.failed.set(false);
    } catch {
      if (version === this.version) this.failed.set(true);
    }
  }
}
