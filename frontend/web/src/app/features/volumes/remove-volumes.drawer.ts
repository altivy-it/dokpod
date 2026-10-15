import { ChangeDetectionStrategy, Component, computed, effect, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { JUSTIFICATION_MAX, JUSTIFICATION_MIN, JustificationPolicyService, justificationMode, justificationValid } from '../../core/justification-policy.service';
import { DokDrawer } from '../../design-system/drawer/dok-drawer';
import { DokField } from '../../design-system/field/dok-field';
import { DokIcon } from '../../design-system/icon/dok-icon';
import { VOLUME_BATCH_LIMIT, type VolumeEligibility } from './volume-removal.service';

/** O que o operador precisa digitar para confirmar (FR-006): a quantidade no lote, o nome de um volume de Produção. */
export function typedConfirmation(eligibility: VolumeEligibility): { label: string; expected: string } | null {
  const count = eligibility.eligible.length;
  if (count > 1) return { label: `Digite ${count} para confirmar a remoção`, expected: String(count) };
  const only = eligibility.eligible[0];
  if (count === 1 && only.stage === 'production') return { label: `Digite o nome do volume (${only.name}) para confirmar a remoção em Produção`, expected: only.name };
  return null;
}

/**
 * Confirmação reforçada da remoção de volumes (spec 018 FR-006): avisa que os dados são apagados de forma
 * definitiva, lista afetados e ignorados, exige digitar quantidade ou nome e pede justificativa pela política do estágio.
 */
@Component({
  selector: 'app-remove-volumes-drawer',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DokDrawer, DokField, DokIcon, FormsModule],
  styles: `
    .rv-list { margin: 0; padding: 0; list-style: none; display: flex; flex-direction: column; max-height: 240px; overflow: auto; border: 1px solid var(--hairline); border-radius: var(--radius-sm); }
    .rv-list li { display: flex; justify-content: space-between; gap: 12px; padding: 7px 10px; border-top: 1px solid var(--hairline); font-size: 13px; }
    .rv-list li:first-child { border-top: 0; }
    .rv-name { color: var(--text-strong); font-weight: 500; font-family: var(--font-mono, monospace); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .rv-meta { color: var(--muted); white-space: nowrap; }
    .rv-note { display: flex; gap: 10px; align-items: flex-start; padding: 12px 14px; border-radius: var(--radius-md); font-size: 13px; color: var(--foreground); }
    .rv-note--danger { background: var(--error-tint-strong); }
    .rv-note--warn { background: var(--warning-tint); }
    .rv-note dok-icon { display: flex; margin-top: 1px; }
    h3 { margin: 0; font-size: 13px; font-weight: 600; color: var(--muted); }
    .rv-justify { display: flex; flex-direction: column; gap: 6px; }
    .rv-textarea { width: 100%; box-sizing: border-box; resize: vertical; padding: 10px 12px; border-radius: var(--radius-md); border: 1px solid var(--glass-border-strong); background: var(--input-fill); color: var(--foreground); font: inherit; }
    .rv-textarea:focus-visible { outline: 2px solid var(--teal-light); outline-offset: 1px; }
    .rv-help { display: flex; justify-content: space-between; gap: 12px; color: var(--muted); }
  `,
  template: `
    <dok-drawer
      [open]="open()"
      eyebrow="Ação destrutiva"
      [title]="title()"
      [subtitle]="eligibility().eligible.length ? 'Cada volume recebe um comando individual, com auditoria. Volumes em uso nunca são removidos.' : 'Nenhum volume selecionado pode ser removido.'"
      [chips]="production() ? [{ tone: 'warning', icon: 'shield', label: 'Produção' }] : []"
      [primaryLabel]="eligibility().eligible.length ? 'Remover' : null"
      primaryIcon="trash"
      primaryVariant="danger"
      [primaryDisabled]="!canConfirm()"
      [primaryLoading]="busy()"
      width="520px"
      (closed)="cancelled.emit()"
      (primary)="confirm()"
    >
      @if (eligibility().eligible.length) {
        <div class="rv-note rv-note--danger" role="note">
          <dok-icon name="alert" [size]="16" />
          Os dados dos volumes serão apagados de forma definitiva. Não há como desfazer.
        </div>
      }
      @if (eligibility().eligible.length > limit) {
        <div class="rv-note rv-note--warn" role="alert">
          <dok-icon name="alert" [size]="16" />
          O lote aceita no máximo {{ limit }} volumes. Reduza a seleção.
        </div>
      }
      @if (eligibility().eligible.length) {
        <h3>{{ eligibility().eligible.length === 1 ? 'Volume afetado' : eligibility().eligible.length + ' volumes afetados' }}</h3>
        <ul class="rv-list">
          @for (item of eligibility().eligible; track item.environmentId + '/' + item.name) {
            <li><span class="rv-name" [title]="item.name">{{ item.name }}</span><span class="rv-meta">{{ item.environmentName }}</span></li>
          }
        </ul>
      }
      @if (eligibility().skipped.length) {
        <h3>{{ eligibility().skipped.length === 1 ? '1 ignorado' : eligibility().skipped.length + ' ignorados' }}</h3>
        <ul class="rv-list">
          @for (entry of eligibility().skipped; track entry.item.environmentId + '/' + entry.item.name) {
            <li><span class="rv-name" [title]="entry.item.name">{{ entry.item.name }} · {{ entry.item.environmentName }}</span><span class="rv-meta">{{ entry.reason }}</span></li>
          }
        </ul>
      }
      @if (typed(); as t) {
        <dok-field [label]="t.label" [ngModel]="typedValue()" (ngModelChange)="typedValue.set($event)" [mono]="true" />
      }
      @if (eligibility().eligible.length) {
        <div class="rv-justify">
          <label for="rv-justification" class="alt-field__label">
            {{ mode() === 'required' ? 'Justificativa (obrigatória pela política do estágio)' : 'Justificativa (opcional)' }}
          </label>
          <textarea
            id="rv-justification"
            class="rv-textarea"
            rows="3"
            [attr.maxlength]="max"
            [attr.aria-required]="mode() === 'required'"
            aria-describedby="rv-justification-help"
            [ngModel]="justification()"
            (ngModelChange)="justification.set($event)"
            name="justification"
            placeholder="Ex.: limpeza de volumes órfãos, chamado 1234"
          ></textarea>
          <span id="rv-justification-help" class="app-caption rv-help">
            <span>De {{ min }} a {{ max }} caracteres. Fica na auditoria; dados sensíveis são mascarados.</span>
            <span class="alt-num" aria-hidden="true">{{ justification().trim().length }}/{{ max }}</span>
          </span>
        </div>
      }
    </dok-drawer>
  `,
})
export class RemoveVolumesDrawer {
  readonly open = input(false);
  readonly eligibility = input.required<VolumeEligibility>();
  readonly busy = input(false);
  /** Emite a justificativa informada (aparada) ou null quando vazia. */
  readonly confirmed = output<string | null>();
  readonly cancelled = output<void>();

  private readonly policy = inject(JustificationPolicyService);
  protected readonly limit = VOLUME_BATCH_LIMIT;
  protected readonly min = JUSTIFICATION_MIN;
  protected readonly max = JUSTIFICATION_MAX;
  protected readonly typedValue = signal('');
  protected readonly justification = signal('');
  protected readonly production = computed(() => this.eligibility().eligible.some(item => item.stage === 'production'));
  protected readonly typed = computed(() => typedConfirmation(this.eligibility()));
  protected readonly mode = computed(() => justificationMode('delete', this.eligibility().eligible.map(item => item.stage), this.policy.required()));
  protected readonly title = computed(() => {
    const eligible = this.eligibility().eligible;
    return eligible.length === 1 ? `Remover ${eligible[0].name}?` : `Remover ${eligible.length} volumes?`;
  });
  protected readonly canConfirm = computed(() => {
    const count = this.eligibility().eligible.length;
    if (count === 0 || count > this.limit) return false;
    if (!justificationValid(this.justification(), this.mode())) return false;
    const typed = this.typed();
    return !typed || String(this.typedValue()).trim() === typed.expected;
  });

  constructor() {
    effect(() => {
      if (this.open()) {
        this.typedValue.set('');
        this.justification.set('');
        void this.policy.load();
      }
    });
  }

  protected confirm(): void {
    if (!this.canConfirm()) return;
    const text = this.justification().trim();
    this.confirmed.emit(text.length ? text : null);
  }
}
