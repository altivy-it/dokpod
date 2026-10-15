import { ChangeDetectionStrategy, Component, computed, effect, inject, input, output, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { EnvironmentDirectoryService } from '../../core/environment-directory.service';
import { JUSTIFICATION_MAX, JUSTIFICATION_MIN } from '../../core/justification-policy.service';
import { ControlPlaneApiService } from '../../data-access/control-plane-api.service';
import type { CreateAlertSilenceRequest, Stage } from '../../data-access/generated/control-plane';
import { DokDrawer, DokField } from '../../design-system';
import { STAGES } from '../../shell/stage-filter.service';
import { toLocalInput } from './alert-detail.drawer';
import { actionError } from './alerts.facade';

export const MAX_SILENCE_MS = 7 * 24 * 3600_000;

interface SilenceForm {
  stage: Stage | '';
  environmentId: string;
  containerPattern: string;
  startsAt: string;
  endsAt: string;
  justification: string;
}

/** Valida a janela e a justificativa; devolve o corpo ou a mensagem do primeiro problema. */
export function silenceRequest(form: SilenceForm, now = new Date()): { request: CreateAlertSilenceRequest | null; error: string | null } {
  const starts = form.startsAt ? new Date(form.startsAt) : now;
  const ends = new Date(form.endsAt);
  if (Number.isNaN(starts.getTime()) || Number.isNaN(ends.getTime())) return { request: null, error: 'Informe início e fim válidos.' };
  if (ends <= starts) return { request: null, error: 'O fim deve ser depois do início.' };
  if (ends.getTime() - starts.getTime() > MAX_SILENCE_MS) return { request: null, error: 'O silenciamento dura no máximo 7 dias.' };
  if (ends <= now) return { request: null, error: 'O fim deve estar no futuro.' };
  const justification = form.justification.trim();
  if (justification.length < JUSTIFICATION_MIN || justification.length > JUSTIFICATION_MAX) {
    return { request: null, error: `A justificativa tem de ${JUSTIFICATION_MIN} a ${JUSTIFICATION_MAX} caracteres.` };
  }
  const pattern = form.containerPattern.trim();
  if (pattern.length > 128) return { request: null, error: 'O padrão de nome aceita até 128 caracteres.' };
  return {
    error: null,
    request: {
      scope: { stage: form.stage || null, environmentId: form.environmentId || null, containerPattern: pattern || null },
      startsAt: starts.toISOString(),
      endsAt: ends.toISOString(),
      justification,
    },
  };
}

/** Novo silenciamento (spec 016 US5): abrangência, janela de até 7 dias e justificativa. */
@Component({
  selector: 'app-silence-drawer',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DokDrawer, DokField, FormsModule],
  styles: `
    .sd-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }
    .sd-full { grid-column: 1 / -1; }
    .sd-label { display: flex; flex-direction: column; gap: 6px; }
    .sd-input { width: 100%; box-sizing: border-box; padding: 8px 10px; border-radius: var(--radius-md); border: 1px solid var(--glass-border-strong); background: var(--input-fill); color: var(--foreground); font: inherit; }
    .sd-input:focus-visible { outline: 2px solid var(--teal-light); outline-offset: 1px; }
    textarea.sd-input { resize: vertical; }
    .sd-help { display: flex; justify-content: space-between; gap: 12px; color: var(--muted); }
    .sd-error { margin: 0; color: var(--signal-error); font-size: 13px; }
    @media (max-width: 560px) { .sd-grid { grid-template-columns: 1fr; } }
  `,
  template: `
    <dok-drawer
      [open]="open()"
      eyebrow="Silenciamento"
      title="Novo silenciamento"
      subtitle="Alertas abrangidos ficam registrados como silenciados, fora do contador e do sino."
      primaryLabel="Silenciar"
      primaryIcon="bell"
      [primaryDisabled]="busy()"
      [primaryLoading]="busy()"
      width="560px"
      (closed)="closed.emit()"
      (primary)="save()"
    >
      <form class="sd-grid" (submit)="$event.preventDefault(); save()">
        <label class="sd-label">
          <span class="alt-field__label">Estágio</span>
          <select class="sd-input" [ngModel]="form().stage" (ngModelChange)="patch({ stage: $event })" name="stage">
            <option value="">Todos os estágios</option>
            @for (stage of stages; track stage.id) {
              <option [value]="stage.id">{{ stage.label }}</option>
            }
          </select>
        </label>
        <label class="sd-label">
          <span class="alt-field__label">Host</span>
          <select class="sd-input" [ngModel]="form().environmentId" (ngModelChange)="patch({ environmentId: $event })" name="host">
            <option value="">Todos os hosts</option>
            @for (host of hosts(); track host.environmentId) {
              <option [value]="host.environmentId">{{ host.name }}</option>
            }
          </select>
        </label>
        <div class="sd-full">
          <dok-field label="Containers (padrão de nome, opcional)" [ngModel]="form().containerPattern" (ngModelChange)="patch({ containerPattern: $event })" name="pattern" placeholder="ex.: batch-*" [mono]="true" />
        </div>
        <label class="sd-label">
          <span class="alt-field__label">Início</span>
          <input class="sd-input" type="datetime-local" [ngModel]="form().startsAt" (ngModelChange)="patch({ startsAt: $event })" name="startsAt" />
        </label>
        <label class="sd-label">
          <span class="alt-field__label">Fim (até 7 dias)</span>
          <input class="sd-input" type="datetime-local" [ngModel]="form().endsAt" (ngModelChange)="patch({ endsAt: $event })" name="endsAt" />
        </label>
        <label class="sd-label sd-full">
          <span class="alt-field__label">Justificativa</span>
          <textarea class="sd-input" rows="3" [attr.maxlength]="max" aria-required="true" aria-describedby="sd-help" [ngModel]="form().justification" (ngModelChange)="patch({ justification: $event })" name="justification" placeholder="Ex.: janela de manutenção do cluster de banco"></textarea>
          <span id="sd-help" class="app-caption sd-help">
            <span>De {{ min }} a {{ max }} caracteres; fica na auditoria.</span>
            <span class="alt-num" aria-hidden="true">{{ form().justification.trim().length }}/{{ max }}</span>
          </span>
        </label>
        @if (error(); as message) {
          <p class="sd-error sd-full" role="alert">{{ message }}</p>
        }
      </form>
    </dok-drawer>
  `,
})
export class SilenceDrawer {
  readonly open = input(false);
  readonly closed = output<void>();
  readonly saved = output<void>();

  private readonly api = inject(ControlPlaneApiService);
  private readonly directory = inject(EnvironmentDirectoryService);
  protected readonly stages = STAGES;
  protected readonly min = JUSTIFICATION_MIN;
  protected readonly max = JUSTIFICATION_MAX;
  protected readonly form = signal<SilenceForm>(this.blank());
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly hosts = computed(() => [...this.directory.environments()].sort((a, b) => a.name.localeCompare(b.name, 'pt-BR')));

  constructor() {
    effect(() => {
      if (!this.open()) return;
      untracked(() => {
        this.form.set(this.blank());
        this.error.set(null);
      });
    });
  }

  protected patch(changes: Partial<SilenceForm>): void {
    this.form.set({ ...this.form(), ...changes });
  }

  protected async save(): Promise<void> {
    const { request, error } = silenceRequest(this.form());
    if (!request) {
      this.error.set(error);
      return;
    }
    this.busy.set(true);
    this.error.set(null);
    try {
      await this.api.createAlertSilence(request);
      this.saved.emit();
    } catch (failure) {
      this.error.set(actionError(failure));
    } finally {
      this.busy.set(false);
    }
  }

  private blank(): SilenceForm {
    const now = new Date();
    return { stage: '', environmentId: '', containerPattern: '', startsAt: toLocalInput(now), endsAt: toLocalInput(new Date(now.getTime() + 3600_000)), justification: '' };
  }
}
