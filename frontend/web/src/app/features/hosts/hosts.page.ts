import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { ConsoleRealtimeService } from '../../core/console-realtime.service';
import { EnvironmentDirectoryService } from '../../core/environment-directory.service';
import { HOST_SITUATION } from '../../data-access/console-labels';
import { ControlPlaneApiService } from '../../data-access/control-plane-api.service';
import type { EnvironmentState, Stage } from '../../data-access/generated/control-plane';
import {
  DokButton,
  DokCheckbox,
  DokDataGrid,
  DokDrawer,
  DokEmptyState,
  DokEngineBadge,
  DokField,
  DokGridCell,
  DokSelect,
  DokStatusCircle,
  formatBytes,
  formatPercent,
  formatDateTime,
  formatRelative,
  ToastService,
  type GridColumn,
  type GridRowAction,
} from '../../design-system';
import { ShellContextService } from '../../shell/shell-context.service';
import { STAGES, StageFilterService, stageLabel } from '../../shell/stage-filter.service';
import { OverviewMetricsFacade } from '../overview/overview-metrics.facade';
import { toLoadError, type LoadError } from '../overview/overview.facade';

interface HostRow {
  id: string;
  name: string;
  host: string;
  stage: string;
  engine: string;
  engineVersion: string;
  platform: string;
  agentVersion: string;
  situation: string;
  containers: number | null;
  cpu: number | null;
  memory: number | null;
  memoryText: string;
  lastSeen: string;
  source: EnvironmentState;
}

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;

/** Hosts e engines (spec 008 US2): lista, cadastro com estágio, alteração de estágio e revogação. */
@Component({
  selector: 'app-hosts-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DokButton,
    DokCheckbox,
    DokDataGrid,
    DokDrawer,
    DokEmptyState,
    DokEngineBadge,
    DokField,
    DokGridCell,
    DokSelect,
    DokStatusCircle,
    ReactiveFormsModule,
  ],
  templateUrl: './hosts.page.html',
  providers: [OverviewMetricsFacade],
  styles: `
    .hosts-usage { display: grid; grid-template-columns: minmax(48px, 1fr) 44px; align-items: center; gap: 8px; width: 100%; }
    .hosts-usage .alt-num { text-align: right; }
  `,
})
export class HostsPage {
  readonly q = input<string | undefined>();

  private readonly api = inject(ControlPlaneApiService);
  protected readonly metrics = inject(OverviewMetricsFacade);
  private readonly realtime = inject(ConsoleRealtimeService);
  private readonly directory = inject(EnvironmentDirectoryService);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);
  protected readonly stageFilter = inject(StageFilterService);

  protected readonly hosts = signal<EnvironmentState[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<LoadError | null>(null);
  protected readonly loadedAt = signal<Date | null>(null);
  protected readonly search = signal('');
  protected readonly registerOpen = signal(false);
  protected readonly registering = signal(false);
  protected readonly registerError = signal<string | null>(null);
  protected readonly stageTarget = signal<EnvironmentState | null>(null);
  protected readonly stageSaving = signal(false);
  protected readonly revokeTarget = signal<EnvironmentState | null>(null);
  protected readonly revoking = signal(false);
  protected readonly stageControl = new FormControl<string>('none', { nonNullable: true });
  private version = 0;

  protected readonly stageOptions = [{ value: 'none', label: 'Sem estágio' }, ...STAGES.map(s => ({ value: s.id, label: s.label }))];
  protected readonly registerForm = new FormGroup({
    environmentId: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.pattern(UUID)] }),
    name: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(128)] }),
    host: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(255)] }),
    stage: new FormControl<string>('none', { nonNullable: true }),
    enabled: new FormControl(true, { nonNullable: true }),
  });

  protected readonly columns: GridColumn<HostRow>[] = [
    { field: 'name', header: 'Host', width: 200, key: true },
    { field: 'stage', header: 'Estágio', width: 140, filterable: true },
    { field: 'engine', header: 'Engine', width: 170, filterable: true },
    { field: 'platform', header: 'Sistema', width: 200 },
    { field: 'agentVersion', header: 'Agente', width: 110, mono: true },
    { field: 'situation', header: 'Situação', type: 'status', width: 150, filterable: true },
    { field: 'containers', header: 'Containers', type: 'number', width: 124 },
    { field: 'cpu', header: 'CPU', type: 'number', width: 150 },
    { field: 'memory', header: 'Memória', type: 'number', width: 190 },
    { field: 'lastSeen', header: 'Último contato', width: 150, sortable: false },
  ];
  protected readonly tones = Object.fromEntries(Object.values(HOST_SITUATION).map(s => [s.label, s.tone]));

  protected readonly rows = computed<HostRow[]>(() =>
    this.hosts().map(host => {
      const agent = host.agent;
      return {
        id: host.environmentId,
        name: host.name,
        host: host.host,
        stage: stageLabel(host.stage),
        engine: agent?.engine ? `${agent.engine === 'podman' ? 'Podman' : 'Docker'} ${agent.engineVersion ?? ''}`.trim() : '—',
        engineVersion: agent?.engineVersion ?? '',
        platform: agent?.operatingSystem
          ? `${agent.operatingSystem === 'windows' ? 'Windows' : 'Linux'} · ${agent.architecture ?? ''}${agent.cpuCount ? ` · ${agent.cpuCount} vCPU` : ''}${agent.memoryBytes ? ` · ${formatBytes(agent.memoryBytes)}` : ''}`
          : '—',
        agentVersion: agent?.agentVersion ?? '—',
        situation: HOST_SITUATION[host.situation ?? 'unknown'].label,
        containers: host.containerCount ?? null,
        ...this.resources(host.environmentId),
        lastSeen: agent?.connected ? 'Conectado agora' : agent?.lastSeenAt ? formatRelative(agent.lastSeenAt) : 'Nunca',
        source: host,
      };
    }),
  );

  protected readonly stageName = computed(() => stageLabel(this.stageFilter.stage()));
  protected readonly canManage = computed(() => this.directory.scopes().has('environment:manage'));
  protected readonly updatedAt = computed(() => (this.loadedAt() ? formatDateTime(this.loadedAt()) : null));

  constructor() {
    inject(ShellContextService).set('Hosts', ['Infraestrutura', 'Hosts']);
    effect(() => {
      const stage = this.stageFilter.stage();
      untracked(() => void this.load(stage));
    });
    effect(() => {
      const q = this.q();
      untracked(() => this.search.set(q ?? ''));
    });
    effect(() => {
      const invalidation = this.realtime.lastInvalidation();
      if (invalidation?.kind === 'host' || invalidation?.kind === 'inventory') {
        untracked(() => {
          if (this.hosts().some(h => h.environmentId === invalidation.environmentId)) void this.load(this.stageFilter.stage(), true);
        });
      }
    });
  }

  /** CPU e memória atuais do host (spec 009 T015); `null` = sem amostra recente. */
  private resources(environmentId: string): Pick<HostRow, 'cpu' | 'memory' | 'memoryText'> {
    const metric = this.metrics.hosts().get(environmentId);
    const memory = metric?.memoryUsedBytes != null && metric.memoryTotalBytes ? (metric.memoryUsedBytes / metric.memoryTotalBytes) * 100 : null;
    return {
      cpu: metric?.cpuPercent ?? null,
      memory,
      memoryText: memory === null ? '—' : `${formatBytes(metric!.memoryUsedBytes!)} de ${formatBytes(metric!.memoryTotalBytes!)}`,
    };
  }

  protected pct(value: number | null, decimals: number): string {
    return formatPercent(value, decimals);
  }

  protected isWarn(value: number | null): boolean {
    return value !== null && value >= this.metrics.operationalLimit();
  }

  protected rowActions(row: HostRow): GridRowAction[] {
    const actions: GridRowAction[] = [{ id: 'containers', label: 'Ver containers', icon: 'box' }];
    if (row.source.scopes.includes('environment:manage')) {
      actions.push({ id: 'stage', label: 'Alterar estágio', icon: 'flag' });
      actions.push({ id: 'revoke', label: 'Revogar identidade do agente', icon: 'lock', danger: true });
    }
    return actions;
  }

  protected readonly rowActionsFor = (row: HostRow) => this.rowActions(row);
  protected readonly rowLabel = (row: HostRow) => row.name;

  protected onRowAction(event: { action: string; row: HostRow }): void {
    if (event.action === 'containers') {
      void this.router.navigate(['/containers'], { queryParams: { environmentId: event.row.id }, queryParamsHandling: 'merge' });
    } else if (event.action === 'stage') {
      this.stageControl.setValue(event.row.source.stage ?? 'none');
      this.stageTarget.set(event.row.source);
    } else if (event.action === 'revoke') {
      this.revokeTarget.set(event.row.source);
    }
  }

  protected openRegister(): void {
    this.registerForm.reset({ environmentId: '', name: '', host: '', stage: this.stageFilter.stage() ?? 'none', enabled: true });
    this.registerError.set(null);
    this.registerOpen.set(true);
  }

  protected fieldError(name: 'environmentId' | 'name' | 'host'): string | null {
    const control = this.registerForm.controls[name];
    if (!control.touched || control.valid) return null;
    return name === 'environmentId' ? 'Informe o UUID provisionado no Keycloak.' : 'Campo obrigatório.';
  }

  protected async register(): Promise<void> {
    this.registerForm.markAllAsTouched();
    if (this.registerForm.invalid) return;
    const value = this.registerForm.getRawValue();
    this.registering.set(true);
    this.registerError.set(null);
    try {
      await this.api.registerEnvironment({
        environmentId: value.environmentId.trim(),
        name: value.name.trim(),
        host: value.host.trim(),
        enabled: value.enabled,
        stage: value.stage === 'none' ? null : (value.stage as Stage),
      });
      this.registerOpen.set(false);
      this.toast.success(`Host ${value.name} cadastrado.`);
      void this.directory.load(true);
      await this.load(this.stageFilter.stage(), true);
    } catch (error: unknown) {
      const status = (error as { status?: number }).status;
      this.registerError.set(
        status === 409
          ? 'Já existe um host com esse identificador ou nome.'
          : status === 403
            ? 'Cadastro não autorizado: confirme a permissão de gestão no Keycloak.'
            : toLoadError(error).detail,
      );
    } finally {
      this.registering.set(false);
    }
  }

  protected async saveStage(): Promise<void> {
    const target = this.stageTarget();
    if (!target) return;
    const value = this.stageControl.value;
    this.stageSaving.set(true);
    try {
      const changed = await this.api.setEnvironmentStage(target.environmentId, value === 'none' ? null : (value as Stage));
      this.stageTarget.set(null);
      this.toast.success(changed ? `Estágio de ${target.name} alterado para ${stageLabel(value === 'none' ? null : value)}.` : 'O host já estava nesse estágio.');
      await this.load(this.stageFilter.stage(), true);
    } catch (error: unknown) {
      this.toast.error(toLoadError(error).detail, 'Não foi possível alterar o estágio');
    } finally {
      this.stageSaving.set(false);
    }
  }

  protected async revoke(): Promise<void> {
    const target = this.revokeTarget();
    if (!target) return;
    this.revoking.set(true);
    try {
      await this.api.revokeAgentIdentity(target.environmentId);
      this.revokeTarget.set(null);
      this.toast.success(`Identidade do agente de ${target.name} revogada. A sessão foi encerrada.`);
      await this.load(this.stageFilter.stage(), true);
    } catch (error: unknown) {
      this.toast.error(toLoadError(error).detail, 'Não foi possível revogar');
    } finally {
      this.revoking.set(false);
    }
  }

  protected async load(stage: Stage | null, quiet = false): Promise<void> {
    const version = ++this.version;
    if (!quiet) this.loading.set(true);
    try {
      const all: EnvironmentState[] = [];
      let cursor: string | undefined;
      for (let page = 0; page < 10; page++) {
        const result = await this.api.listEnvironments(cursor, stage ? { stage, limit: 100 } : { limit: 100 });
        all.push(...result.environments);
        if (!result.nextCursor) break;
        cursor = result.nextCursor;
      }
      if (version !== this.version) return;
      this.hosts.set(all);
      this.error.set(null);
      this.loadedAt.set(new Date());
      void this.realtime.watch(all.map(h => h.environmentId));
    } catch (error: unknown) {
      if (version === this.version) this.error.set(toLoadError(error));
    } finally {
      if (version === this.version) this.loading.set(false);
    }
  }
}
