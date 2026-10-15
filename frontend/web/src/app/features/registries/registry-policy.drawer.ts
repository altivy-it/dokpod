import { ChangeDetectionStrategy, Component, computed, effect, inject, input, output, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DokDrawer } from '../../design-system/drawer/dok-drawer';
import { DokIcon } from '../../design-system/icon/dok-icon';
import { normalizeRegistry, RegistriesApiService, type PolicyStage } from './registries-api.service';
import { POLICY_STAGE_LABEL } from './registry-labels';

/** Lista digitada (um registro por linha ou separados por vírgula) normalizada; inválidos devolvidos à parte. */
export function parseRegistries(text: string): { registries: string[]; invalid: string[] } {
  const registries: string[] = [];
  const invalid: string[] = [];
  for (const raw of text.split(/[\n,]+/)) {
    if (!raw.trim()) continue;
    const name = normalizeRegistry(raw);
    if (name === null) invalid.push(raw.trim());
    else if (!registries.includes(name)) registries.push(name);
  }
  return { registries: registries.sort(), invalid };
}

/**
 * Edição da política de registros de um estágio (spec 020 US2): lista vazia = qualquer registro; a confirmação mostra
 * as imagens existentes que ficam fora, sem remover nada (FR-007).
 */
@Component({
  selector: 'app-registry-policy-drawer',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DokDrawer, DokIcon, FormsModule],
  styles: `
    .rp-field { display: flex; flex-direction: column; gap: 6px; }
    .rp-textarea { width: 100%; box-sizing: border-box; resize: vertical; padding: 10px 12px; border-radius: var(--radius-md); border: 1px solid var(--glass-border-strong); background: var(--input-fill); color: var(--foreground); font-family: var(--font-mono, monospace); font-size: 13px; }
    .rp-textarea:focus-visible { outline: 2px solid var(--teal-light); outline-offset: 1px; }
    .rp-note { display: flex; gap: 10px; align-items: flex-start; padding: 12px 14px; border-radius: var(--radius-md); font-size: 13px; }
    .rp-note--info { background: var(--info-tint); }
    .rp-note--warn { background: var(--warning-tint); }
    .rp-note dok-icon { display: flex; margin-top: 1px; }
    .rp-help { color: var(--muted); }
  `,
  template: `
    <dok-drawer
      [open]="open()"
      eyebrow="Política de registros"
      [title]="'Registros permitidos em ' + stageLabel()"
      subtitle="Vale para os próximos downloads e criações de container. Imagens e containers existentes não são removidos."
      primaryLabel="Salvar política"
      primaryIcon="check"
      [primaryDisabled]="!canSave()"
      [primaryLoading]="busy()"
      width="520px"
      (closed)="closed.emit()"
      (primary)="save()"
    >
      <div class="rp-field">
        <label for="rp-registries" class="alt-field__label">Registros permitidos (um por linha; vazio = qualquer registro)</label>
        <textarea
          id="rp-registries"
          class="rp-textarea"
          rows="6"
          aria-describedby="rp-help"
          [ngModel]="text()"
          (ngModelChange)="text.set($event)"
          name="registries"
          placeholder="registry.empresa.com&#10;quay.io"
        ></textarea>
        <span id="rp-help" class="app-caption rp-help">Somente host ou host:porta, sem https://, caminho ou credencial. Ex.: registry.empresa.com:5000.</span>
      </div>
      @if (parsed().invalid.length) {
        <div class="rp-note rp-note--warn" role="alert"><dok-icon name="alert" [size]="16" />Registros inválidos: {{ parsed().invalid.join(', ') }}</div>
      }
      <div class="rp-note rp-note--info" aria-live="polite">
        <dok-icon name="info" [size]="16" />
        @if (parsed().registries.length === 0) {
          Qualquer registro será aceito em {{ stageLabel() }}.
        } @else if (impact(); as i) {
          {{ i.images === 0 ? 'Nenhuma imagem existente fica fora da lista.' : i.images + (i.images === 1 ? ' imagem' : ' imagens') + ' em ' + i.hosts + (i.hosts === 1 ? ' host fica' : ' hosts ficam') + ' fora da lista (nada é removido).' }}
        } @else {
          Calculando as imagens que ficam fora da lista…
        }
      </div>
    </dok-drawer>
  `,
})
export class RegistryPolicyDrawer {
  readonly open = input(false);
  readonly stage = input.required<PolicyStage>();
  readonly current = input<string[]>([]);
  readonly busy = input(false);
  readonly saved = output<string[]>();
  readonly closed = output<void>();

  private readonly api = inject(RegistriesApiService);
  private impactVersion = 0;
  private impactTimer: ReturnType<typeof setTimeout> | null = null;
  protected readonly text = signal('');
  protected readonly impact = signal<{ images: number; hosts: number } | null>(null);
  protected readonly parsed = computed(() => parseRegistries(this.text()));
  protected readonly stageLabel = computed(() => POLICY_STAGE_LABEL[this.stage()]);
  protected readonly canSave = computed(() => this.parsed().invalid.length === 0 && this.parsed().registries.length <= 100);

  constructor() {
    effect(() => {
      if (!this.open()) return;
      const current = this.current();
      untracked(() => this.text.set(current.join('\n')));
    });
    effect(() => {
      const { registries, invalid } = this.parsed();
      const stage = this.stage();
      untracked(() => {
        const version = ++this.impactVersion;
        this.impact.set(null);
        if (this.impactTimer) clearTimeout(this.impactTimer);
        if (invalid.length || registries.length === 0) return;
        this.impactTimer = setTimeout(() => {
          void this.api.impact(stage, registries).then(
            result => version === this.impactVersion && this.impact.set(result),
            () => version === this.impactVersion && this.impact.set({ images: 0, hosts: 0 }),
          );
        }, 400);
      });
    });
  }

  protected save(): void {
    if (this.canSave()) this.saved.emit(this.parsed().registries);
  }
}
