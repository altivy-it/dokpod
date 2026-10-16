import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { AuditAccessService } from '../../core/audit-access.service';
import { ConfirmActionDrawer } from '../../core/container-actions/confirm-action.drawer';
import { ACTION_LABEL, ContainerActionsService, evaluateEligibility, type Eligibility } from '../../core/container-actions/container-actions.service';
import { ConsoleRealtimeService } from '../../core/console-realtime.service';
import { intentionalLabel } from '../../data-access/alert-labels';
import { formatDateTime } from '../../design-system/format/format';
import { CONTAINER_STATUS } from '../../data-access/console-labels';
import { ControlPlaneApiService, type ContainerAction } from '../../data-access/control-plane-api.service';
import type { ContainerConfigSummary, ContainerDetail, ContainerInspection } from '../../data-access/generated/control-plane';
import {
  DokButton,
  DokCard,
  DokEmptyState,
  DokEngineBadge,
  DokIcon,
  DokKeyValue,
  DokLineChart,
  DokStatusChip,
  DokTabs,
  formatBytes,
  formatDuration,
  formatPercent,
  ToastService,
  type KeyValueItem,
  type TabItem,
} from '../../design-system';
import { ShellContextService } from '../../shell/shell-context.service';
import { toLoadError, type LoadError } from '../overview/overview.facade';
import { ContainerMetricsFacade } from './container-metrics.facade';
import { LogsPanel } from './logs.panel';
import { TerminalTab } from './terminal.tab';
import { EnvironmentDirectoryService } from '../../core/environment-directory.service';
import { PodsApiService } from '../pods/pods-api.service';

type TabId = 'resumo' | 'logs' | 'inspecionar' | 'estatisticas' | 'terminal';
type Loadable<T> = { state: 'idle' | 'loading' | 'ready' | 'forbidden' | 'unavailable' | 'error'; value: T | null; message?: string };

const HEALTH: Record<string, string> = { healthy: 'saudável', unhealthy: 'não saudável', starting: 'iniciando', none: 'não configurado' };

/** Detalhe do container (board `03 · Detalhe`, spec 011). */
@Component({
  selector: 'app-container-detail-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ConfirmActionDrawer,
    DokButton,
    DokCard,
    DokEmptyState,
    DokEngineBadge,
    DokIcon,
    DokKeyValue,
    DokLineChart,
    DokStatusChip,
    DokTabs,
    LogsPanel,
    RouterLink,
    TerminalTab,
  ],
  styleUrl: './container-detail.page.css',
  templateUrl: './container-detail.page.html',
  providers: [ContainerMetricsFacade],
})
export class ContainerDetailPage {
  readonly environmentId = input.required<string>();
  readonly containerId = input.required<string>();
  readonly aba = input<string | undefined>();

  private readonly api = inject(ControlPlaneApiService);
  protected readonly metrics = inject(ContainerMetricsFacade);
  protected readonly percent = (value: number) => formatPercent(value, 1);
  protected readonly isStopped = computed(() => ['stopped', 'failed'].includes(this.detail()?.displayStatus ?? ''));
  private readonly realtime = inject(ConsoleRealtimeService);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);
  private readonly context = inject(ShellContextService);
  protected readonly actions = inject(ContainerActionsService);
  protected readonly auditAccess = inject(AuditAccessService);

  protected readonly detail = signal<ContainerDetail | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<LoadError | null>(null);
  protected readonly removed = signal(false);
  protected readonly summary = signal<Loadable<ContainerConfigSummary>>({ state: 'idle', value: null });
  protected readonly inspection = signal<Loadable<ContainerInspection>>({ state: 'idle', value: null });
  protected readonly pending = signal<{ action: ContainerAction; eligibility: Eligibility } | null>(null);
  protected readonly submitting = signal(false);

  protected readonly tabs: TabItem[] = [
    { id: 'resumo', label: 'Resumo', icon: 'info' },
    { id: 'logs', label: 'Logs', icon: 'list' },
    { id: 'inspecionar', label: 'Inspecionar', icon: 'eye' },
    { id: 'estatisticas', label: 'Estatísticas', icon: 'chart-line' },
    { id: 'terminal', label: 'Terminal', icon: 'command' },
  ];
  protected readonly tabLabel = computed(() => this.tabs.find(t => t.id === this.tab())?.label ?? '');
  protected readonly tab = computed<TabId>(() => {
    const value = this.aba();
    return value === 'resumo' || value === 'inspecionar' || value === 'estatisticas' || value === 'terminal' ? value : 'logs';
  });

  protected readonly status = computed(() => {
    const intentional = this.detail()?.intentional;
    if (intentional) return { label: intentionalLabel(intentional), tone: intentional.action === 'pause' ? ('info' as const) : ('neutral' as const) };
    return CONTAINER_STATUS[this.detail()?.displayStatus ?? 'stopped'];
  });
  /** Autor, horário e motivo da parada ou pausa justificada (spec 016 FR-018). */
  protected readonly intentionalNote = computed(() => {
    const intentional = this.detail()?.intentional;
    if (!intentional) return null;
    return `${intentionalLabel(intentional)} por ${intentional.actor} em ${formatDateTime(intentional.at)}: ${intentional.reason}`;
  });
  protected readonly shortId = computed(() => this.containerId().slice(0, 12));
  protected readonly facts = computed(() => {
    const d = this.detail();
    if (!d) return '';
    const parts = [d.environmentName];
    const state = (d.state ?? '').toLowerCase();
    if (state === 'running' && d.startedAt) parts.push(`ativo há ${formatDuration(d.startedAt)}`);
    else if (d.exitCode !== null && d.exitCode !== undefined) parts.push(`encerrado com código ${d.exitCode}`);
    parts.push(`${d.restartsLast24h} ${d.restartsLast24h === 1 ? 'reinício' : 'reinícios'} nas últimas 24 h`);
    if (d.health) parts.push(`health check: ${HEALTH[d.health] ?? d.health}`);
    if (d.oomKilled) parts.push('encerrado por falta de memória (OOM)');
    return parts.join(' · ');
  });
  protected readonly allowed = computed(() => new Set<string>(this.detail()?.allowedActions ?? []));
  private readonly directory = inject(EnvironmentDirectoryService);
  /** Motivo de o terminal estar indisponível (spec 014 T012): sem permissão, sem suporte no agente ou parado. */
  protected readonly terminalBlocked = computed(() => {
    const d = this.detail();
    if (!d) return 'Carregando';
    if (this.allowed().has('exec')) return null;
    const env = this.directory.byId(this.environmentId());
    if (env && !env.scopes.includes('container:exec')) return 'Sem permissão para abrir terminal';
    if (!(d.capabilities ?? []).includes('ContainerExec')) return 'Terminal não suportado pelo agente deste host';
    return 'Disponível somente com o container em execução';
  });
  protected readonly summaryItems = computed<KeyValueItem[]>(() => {
    const s = this.summary().value;
    if (!s) return [];
    const health = s.healthcheck;
    const labels = Object.entries(s.labels ?? {});
    const items: KeyValueItem[] = [
      { label: 'Comando', value: s.command || '—', mono: true },
      { label: 'Usuário', value: s.user || 'padrão da imagem', mono: true },
      { label: 'Diretório', value: s.workingDir || '/', mono: true },
      {
        label: 'Health check',
        value: health?.test
          ? `${health.test}${health.intervalSeconds ? ` a cada ${health.intervalSeconds} s` : ''}${health.retries ? ` · ${health.retries} tentativas` : ''}${health.timeoutSeconds ? ` · tempo limite ${health.timeoutSeconds} s` : ''}`
          : 'não configurado',
      },
      { label: 'Labels', value: labels.length ? labels.map(([k, v]) => `${k}=${v}`).join(' · ') : 'nenhuma', mono: true },
      {
        label: 'Logs',
        value: s.logging?.driver
          ? `Driver ${s.logging.driver}${Object.keys(s.logging.options ?? {}).length ? ` · ${Object.entries(s.logging.options ?? {}).map(([k, v]) => `${k}=${v}`).join(', ')}` : ''}`
          : '—',
      },
    ];
    return items;
  });
  protected readonly networkItems = computed<KeyValueItem[]>(() => {
    const d = this.detail();
    const s = this.summary().value;
    const ports = (d?.ports ?? []).map(p => (p.hostPort ? `${p.hostIp ?? '0.0.0.0'}:${p.hostPort} → ${p.containerPort}/${p.protocol}` : `${p.containerPort}/${p.protocol}`));
    const items: KeyValueItem[] = [{ label: 'Portas', value: ports.length ? ports.join(', ') : 'nenhuma publicada', mono: true }];
    if (s) {
      const networks = s.networks ?? [];
      const volumes = s.volumes ?? [];
      items.push({ label: 'Rede', value: networks.map(n => n.name).join(', ') || '—', mono: true });
      items.push({ label: 'IP', value: networks.map(n => n.ipAddress).filter(Boolean).join(', ') || '—', mono: true });
      items.push({
        label: 'Volumes',
        value: volumes.length ? volumes.map(v => `${v.name || v.type} → ${v.destination}${v.readOnly ? ' (ro)' : ''}`).join(', ') : 'nenhum',
        mono: true,
      });
      items.push({ label: 'Reinício', value: s.restartPolicy?.name || 'no' });
      items.push({ label: 'Variáveis', value: `${s.environmentVariableCount ?? 0} definidas${s.secretCount ? ` · ${s.secretCount} segredos ocultos` : ''}` });
    }
    return items;
  });
  /** Volumes nomeados montados, com link para o detalhe em Operação → Volumes (spec 018 FR-009). */
  protected readonly volumeLinks = computed(() => {
    const environmentId = this.environmentId();
    return (this.summary().value?.volumes ?? [])
      .filter(v => v.type === 'volume' && !!v.name)
      .map(v => ({ name: v.name!, destination: v.destination, queryParams: { environmentId, volumeHost: environmentId, volume: v.name } }));
  });
  /** Pod do container (spec 021 FR-004), consultado só em hosts com pods. */
  protected readonly pod = signal<{ name: string; podId: string } | null>(null);
  private readonly podsApi = inject(PodsApiService);

  /** Redes do container, com link para o detalhe em Operação → Redes (spec 019 FR-009). */
  protected readonly networkLinks = computed(() => {
    const environmentId = this.environmentId();
    return (this.summary().value?.networks ?? [])
      .filter(n => !!n.name)
      .map(n => ({ name: n.name!, address: n.ipAddress || null, queryParams: { environmentId, redeHost: environmentId, rede: n.name } }));
  });
  protected readonly limits = computed(() => {
    const s = this.summary().value;
    return {
      cpu: s?.cpuLimitCores ? `limite ${s.cpuLimitCores} vCPU` : 'sem limite',
      memory: s?.memoryLimitBytes ? `limite ${formatBytes(s.memoryLimitBytes)}` : 'sem limite',
    };
  });
  protected readonly inspectText = computed(() => {
    const value = this.inspection().value;
    return value ? JSON.stringify(value.json, null, 2) : '';
  });

  constructor() {
    effect(() => {
      const environmentId = this.environmentId();
      const containerId = this.containerId();
      untracked(() => this.metrics.watch(environmentId, containerId));
    });
    // Pod do container (spec 021): uma consulta por container; hosts sem pods devolvem lista vazia.
    effect(() => {
      const environmentId = this.environmentId();
      const containerId = this.containerId();
      untracked(() => {
        this.pod.set(null);
        void this.podsApi.podOfContainer(environmentId, containerId).then(
          pod => this.containerId() === containerId && this.pod.set(pod ? { name: pod.name, podId: pod.podId } : null),
          () => this.pod.set(null),
        );
      });
    });
    effect(() => {
      const env = this.environmentId();
      const id = this.containerId();
      untracked(() => {
        this.summary.set({ state: 'idle', value: null });
        this.inspection.set({ state: 'idle', value: null });
        void this.load(env, id);
        void this.loadSummary();
      });
    });
    effect(() => {
      const d = this.detail();
      this.context.set(d?.name ?? 'Container', ['Operação', 'Containers', d?.name ?? this.shortId()]);
    });
    effect(() => {
      if (this.tab() === 'inspecionar' && untracked(() => this.inspection().state) === 'idle') untracked(() => void this.loadInspection());
    });
    effect(() => {
      const invalidation = this.realtime.lastInvalidation();
      if (invalidation?.kind === 'inventory' && invalidation.environmentId === untracked(() => this.environmentId())) {
        untracked(() => void this.load(this.environmentId(), this.containerId(), true));
      }
    });
  }

  protected selectTab(tab: string): void {
    void this.router.navigate([], { queryParams: { aba: tab }, queryParamsHandling: 'merge', replaceUrl: true });
  }

  protected run(action: ContainerAction): void {
    const d = this.detail();
    if (!d) return;
    this.pending.set({ action, eligibility: evaluateEligibility([d], action) });
  }

  protected async confirm(justification: string | null = null): Promise<void> {
    const pending = this.pending();
    if (!pending) return;
    this.submitting.set(true);
    try {
      await this.actions.run(pending.action, pending.eligibility.eligible, justification);
      this.pending.set(null);
      this.toast.notify('info', { message: `${ACTION_LABEL[pending.action].verb}: comando enviado. O resultado aparece em instantes.` });
    } catch (error: unknown) {
      this.toast.error(error instanceof Error ? error.message : 'Não foi possível enviar o comando.');
    } finally {
      this.submitting.set(false);
    }
  }

  protected async copyInspect(): Promise<void> {
    try {
      await navigator.clipboard.writeText(this.inspectText());
      this.toast.success('JSON de inspeção copiado.');
    } catch {
      this.toast.error('Não foi possível copiar para a área de transferência.');
    }
  }

  protected async loadInspection(): Promise<void> {
    this.inspection.set({ state: 'loading', value: null });
    try {
      this.inspection.set({ state: 'ready', value: await this.api.inspectContainer(this.environmentId(), this.containerId()) });
    } catch (error: unknown) {
      this.inspection.set(failure(error));
    }
  }

  private async loadSummary(): Promise<void> {
    this.summary.set({ state: 'loading', value: null });
    try {
      this.summary.set({ state: 'ready', value: await this.api.getContainerSummary(this.environmentId(), this.containerId()) });
    } catch (error: unknown) {
      this.summary.set(failure(error));
    }
  }

  private async load(environmentId: string, containerId: string, quiet = false): Promise<void> {
    if (!quiet) this.loading.set(true);
    try {
      this.detail.set(await this.api.getContainer(environmentId, containerId));
      this.error.set(null);
      this.removed.set(false);
      void this.realtime.watch([environmentId]);
    } catch (error: unknown) {
      const status = (error as { status?: number }).status;
      if (status === 404 && this.detail()) this.removed.set(true);
      else this.error.set(status === 404 ? { status, title: 'Container não encontrado', detail: 'O container não existe ou você não tem acesso a este host.' } : toLoadError(error));
    } finally {
      this.loading.set(false);
    }
  }
}

function failure<T>(error: unknown): Loadable<T> {
  const status = (error as { status?: number }).status;
  if (status === 403) return { state: 'forbidden', value: null, message: 'Sem permissão para inspecionar este container.' };
  if (status === 409) return { state: 'unavailable', value: null, message: 'Indisponível: agente desconectado.' };
  if (status === 422) return { state: 'unavailable', value: null, message: 'Não suportado pelo agente deste host.' };
  return { state: 'error', value: null, message: toLoadError(error).detail };
}
