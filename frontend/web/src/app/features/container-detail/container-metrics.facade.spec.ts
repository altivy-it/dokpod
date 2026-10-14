import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { ControlPlaneApiService } from '../../data-access/control-plane-api.service';
import { ContainerMetricsFacade } from './container-metrics.facade';

describe('ContainerMetricsFacade', () => {
  const getContainerMetrics = vi.fn();

  beforeEach(() => {
    getContainerMetrics.mockReset();
    TestBed.configureTestingModule({
      providers: [ContainerMetricsFacade, { provide: ControlPlaneApiService, useValue: { getContainerMetrics } }],
    });
  });

  async function settle(): Promise<void> {
    for (let i = 0; i < 5; i++) await Promise.resolve();
  }

  it('builds resource rows with limit highlight and a partial series note', async () => {
    getContainerMetrics.mockResolvedValue({
      operationalLimitPercent: 80,
      current: { cpuPercent: 85.5, memoryUsedBytes: 256 * 1024 ** 2, memoryLimitBytes: 512 * 1024 ** 2, netRxBps: 2048, netTxBps: null, sampledAt: '2026-10-13T12:00:00Z' },
      series: { x: ['2026-10-13T11:58:00Z', '2026-10-13T11:59:00Z', '2026-10-13T12:00:00Z'], cpu: [null, 80, 85.5], memory: [null, 50, 50] },
    });
    const facade = TestBed.inject(ContainerMetricsFacade);

    facade.watch('env-1', 'abc');
    await settle();

    expect(getContainerMetrics).toHaveBeenCalledWith('env-1', 'abc', '30m', '1m');
    const [cpu, memory, network] = facade.resources()!;
    expect(cpu).toMatchObject({ value: '85,5%', percent: 85.5, warn: true });
    expect(memory).toMatchObject({ value: '256 MiB de 512 MiB', percent: 50, warn: false });
    expect(network.value).toBe('2 KiB/s · —');
    expect(facade.chart().partial).toBe(false);
    expect(facade.chart().reference.label).toBe('Limite 80%');
  });

  it('reports no resources for a container without recent samples', async () => {
    getContainerMetrics.mockResolvedValue({ operationalLimitPercent: 80, current: null, series: { x: [], cpu: [], memory: [] } });
    const facade = TestBed.inject(ContainerMetricsFacade);

    facade.watch('env-1', 'stopped');
    await settle();

    expect(facade.resources()).toBeNull();
    expect(facade.chart().empty).toBe(true);
  });
});
