import { ChangeDetectionStrategy, Component, computed, effect, inject, input, output, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { EnvironmentDirectoryService } from '../../core/environment-directory.service';
import type { EnvironmentState } from '../../data-access/generated/control-plane';
import { parseImageReference } from '../../data-access/image-reference';
import { DokButton, DokStatusChip, formatBytes, ToastService } from '../../design-system';
import { DokCheckbox } from '../../design-system/checkbox/dok-checkbox';
import { DokDrawer } from '../../design-system/drawer/dok-drawer';
import { DokField } from '../../design-system/field/dok-field';
import { DokIcon } from '../../design-system/icon/dok-icon';
import { StageFilterService, stageLabel } from '../../shell/stage-filter.service';
import { ImagePullService, type PullState } from './image-pull.service';

interface HostOption {
  env: EnvironmentState;
  eligible: boolean;
  reason: string | null;
}

const STATE_LABEL: Record<PullState, { label: string; tone: 'success' | 'error' | 'warning' | 'info' }> = {
  queued: { label: 'Na fila', tone: 'info' },
  pending: { label: 'Aguardando o agente', tone: 'info' },
  dispatched: { label: 'Enviado ao agente', tone: 'info' },
  accepted: { label: 'Baixando', tone: 'info' },
  succeeded: { label: 'Concluído', tone: 'success' },
  failed: { label: 'Falhou', tone: 'error' },
  indeterminate: { label: 'Indeterminado', tone: 'warning' },
};

/** Motivo pelo qual o host não aceita pull agora; nulo quando elegível (scope × capability × conexão). */
export function pullIneligibility(env: EnvironmentState): string | null {
  if (!env.enabled) return 'host desabilitado';
  if (!env.scopes.includes('image:pull')) return 'sem permissão image:pull';
  if (!env.agent?.connected) return 'agente desconectado';
  if (!env.agent.capabilities.includes('ImagePull')) return 'agente sem suporte a download';
  return null;
}

/**
 * Download de imagem (spec 012 US2): referência validada com aviso de tag mutável, hosts do estágio
 * selecionáveis e acompanhamento por host com os estados do comando.
 */
@Component({
  selector: 'app-pull-image-drawer',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DokButton, DokCheckbox, DokDrawer, DokField, DokIcon, DokStatusChip, FormsModule],
  styleUrl: './pull-image.drawer.css',
  templateUrl: './pull-image.drawer.html',
})
export class PullImageDrawer {
  readonly open = input(false);
  readonly preselected = input<string[]>([]);
  readonly initialReference = input('');
  readonly closed = output<void>();

  private readonly directory = inject(EnvironmentDirectoryService);
  private readonly stageFilter = inject(StageFilterService);
  private readonly toast = inject(ToastService);
  protected readonly pulls = inject(ImagePullService);
  protected readonly states = STATE_LABEL;
  protected readonly formatBytes = formatBytes;

  protected readonly reference = signal('');
  protected readonly touched = signal(false);
  protected readonly selected = signal<ReadonlySet<string>>(new Set());
  protected readonly submitting = signal(false);

  protected readonly parsed = computed(() => parseImageReference(this.reference().trim()));
  protected readonly referenceError = computed(() =>
    this.touched() && this.reference().trim() && !this.parsed()
      ? 'Use o formato registro/repositório:tag ou @sha256:…, sem credenciais, espaços ou maiúsculas no caminho.'
      : null,
  );
  protected readonly stageName = computed(() => stageLabel(this.stageFilter.stage()));
  protected readonly hosts = computed<HostOption[]>(() =>
    this.directory
      .environments()
      .filter(env => !this.stageFilter.stage() || env.stage === this.stageFilter.stage())
      .map(env => {
        const reason = pullIneligibility(env);
        return { env, eligible: reason === null, reason };
      })
      .sort((a, b) => Number(b.eligible) - Number(a.eligible) || a.env.name.localeCompare(b.env.name)),
  );
  protected readonly eligibleCount = computed(() => this.hosts().filter(h => h.eligible).length);
  protected readonly selectedHosts = computed(() => this.hosts().filter(h => h.eligible && this.selected().has(h.env.environmentId)));
  protected readonly tracking = computed(() => this.pulls.run() !== null);
  protected readonly canSubmit = computed(() => !!this.parsed() && this.selectedHosts().length > 0 && !this.pulls.running());
  protected readonly chips = computed(() => [{ label: this.stageName() }]);
  protected readonly elapsed = computed(() => {
    const ms = this.pulls.elapsedMs();
    return ms === null ? null : ms < 60_000 ? `${Math.round(ms / 1000)} s` : `${Math.floor(ms / 60_000)} min ${Math.round((ms % 60_000) / 1000)} s`;
  });

  constructor() {
    effect(() => {
      if (!this.open()) return;
      const pre = this.preselected();
      const reference = this.initialReference();
      untracked(() => {
        void this.directory.load();
        if (!this.pulls.running()) {
          this.touched.set(false);
          this.reference.set(reference);
          const eligible = this.hosts().filter(h => h.eligible).map(h => h.env.environmentId);
          this.selected.set(new Set(pre.length ? pre.filter(id => eligible.includes(id)) : eligible.length === 1 ? eligible : []));
        }
      });
    });
  }

  protected toggle(environmentId: string, checked: boolean): void {
    const next = new Set(this.selected());
    if (checked) next.add(environmentId);
    else next.delete(environmentId);
    this.selected.set(next);
  }

  protected selectAll(): void {
    this.selected.set(new Set(this.hosts().filter(h => h.eligible).map(h => h.env.environmentId)));
  }

  protected async submit(): Promise<void> {
    this.touched.set(true);
    const parsed = this.parsed();
    if (this.tracking() && !this.pulls.running()) {
      this.pulls.dismiss();
      return;
    }
    if (!parsed || !this.canSubmit()) return;
    this.submitting.set(true);
    try {
      await this.pulls.start(
        parsed.canonical,
        this.selectedHosts().map(h => ({ environmentId: h.env.environmentId, name: h.env.name })),
      );
    } catch (error: unknown) {
      this.toast.error(error instanceof Error ? error.message : 'Não foi possível solicitar o download.');
    } finally {
      this.submitting.set(false);
    }
  }

  protected primaryLabel(): string | null {
    if (this.tracking()) return this.pulls.running() ? null : 'Novo download';
    const count = this.selectedHosts().length;
    return count > 1 ? `Baixar em ${count} hosts` : 'Baixar imagem';
  }
}
