import { ChangeDetectionStrategy, Component, computed, effect, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import type { ContainerAction } from '../../data-access/control-plane-api.service';
import { DokDrawer } from '../../design-system/drawer/dok-drawer';
import { DokField } from '../../design-system/field/dok-field';
import { DokIcon } from '../../design-system/icon/dok-icon';
import { JUSTIFICATION_MAX, JUSTIFICATION_MIN, JustificationPolicyService, justificationMode, justificationValid } from '../justification-policy.service';
import { ACTION_LABEL, BATCH_LIMIT, type Eligibility } from './container-actions.service';

/**
 * Confirmação contextual de ação (spec 010 FR-007): lista afetados e ignorados, declara que volumes
 * não são removidos e, para remover mais de um container em Produção, exige digitar a quantidade.
 * Parar, pausar e remover pedem justificativa, obrigatória onde a política do estágio exige (spec 016 FR-015).
 */
@Component({
  selector: 'app-confirm-action-drawer',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DokDrawer, DokField, DokIcon, FormsModule],
  styles: `
    .ca-list { margin: 0; padding: 0; list-style: none; display: flex; flex-direction: column; max-height: 240px; overflow: auto; border: 1px solid var(--hairline); border-radius: var(--radius-sm); }
    .ca-list li { display: flex; justify-content: space-between; gap: 12px; padding: 7px 10px; border-top: 1px solid var(--hairline); font-size: 13px; }
    .ca-list li:first-child { border-top: 0; }
    .ca-name { color: var(--text-strong); font-weight: 500; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .ca-meta { color: var(--muted); white-space: nowrap; }
    .ca-note { display: flex; gap: 10px; align-items: flex-start; padding: 12px 14px; border-radius: var(--radius-md); font-size: 13px; color: var(--foreground); }
    .ca-note--info { background: var(--info-tint); }
    .ca-note--warn { background: var(--warning-tint); }
    .ca-note dok-icon { display: flex; margin-top: 1px; }
    h3 { margin: 0; font-size: 13px; font-weight: 600; color: var(--muted); }
    .ca-justify { display: flex; flex-direction: column; gap: 6px; }
    .ca-textarea { width: 100%; box-sizing: border-box; resize: vertical; padding: 10px 12px; border-radius: var(--radius-md); border: 1px solid var(--glass-border-strong); background: var(--input-fill); color: var(--foreground); font: inherit; }
    .ca-textarea:focus-visible { outline: 2px solid var(--teal-light); outline-offset: 1px; }
    .ca-help { display: flex; justify-content: space-between; gap: 12px; color: var(--muted); }
  `,
  template: `
    <dok-drawer
      [open]="open()"
      [eyebrow]="eyebrow()"
      [title]="title()"
      [subtitle]="subtitle()"
      [chips]="chips()"
      [primaryLabel]="eligibility().eligible.length ? verb() : null"
      [primaryIcon]="icon()"
      [primaryVariant]="action() === 'delete' ? 'danger' : 'primary'"
      [primaryDisabled]="!canConfirm()"
      [primaryLoading]="busy()"
      width="520px"
      (closed)="cancelled.emit()"
      (primary)="confirm()"
    >
      @if (eligibility().eligible.length > limit) {
        <div class="ca-note ca-note--warn" role="alert">
          <dok-icon name="alert" [size]="16" />
          O lote aceita no máximo {{ limit }} containers. Reduza a seleção.
        </div>
      }
      @if (eligibility().eligible.length) {
        <h3>{{ eligibility().eligible.length === 1 ? 'Container afetado' : eligibility().eligible.length + ' containers afetados' }}</h3>
        <ul class="ca-list">
          @for (item of eligibility().eligible; track item.environmentId + item.containerId) {
            <li><span class="ca-name">{{ item.name }}</span><span class="ca-meta">{{ item.environmentName }}</span></li>
          }
        </ul>
      }
      @if (eligibility().skipped.length) {
        <h3>{{ eligibility().skipped.length === 1 ? '1 ignorado' : eligibility().skipped.length + ' ignorados' }}</h3>
        <ul class="ca-list">
          @for (entry of eligibility().skipped; track entry.item.environmentId + entry.item.containerId) {
            <li><span class="ca-name">{{ entry.item.name }} · {{ entry.item.environmentName }}</span><span class="ca-meta">{{ entry.reason }}</span></li>
          }
        </ul>
      }
      @if (action() === 'delete' && eligibility().eligible.length) {
        <div class="ca-note ca-note--info">
          <dok-icon name="info" [size]="16" />
          Volumes não são removidos. Somente o container é excluído do host.
        </div>
        @if (needsTypedConfirmation()) {
          <dok-field
            [label]="'Digite ' + eligibility().eligible.length + ' para confirmar a remoção em Produção'"
            type="number"
            [ngModel]="typed()"
            (ngModelChange)="typed.set($event)"
            [mono]="true"
          />
        }
      }
      @if (mode() !== 'none' && eligibility().eligible.length) {
        <div class="ca-justify">
          <label for="ca-justification" class="alt-field__label">
            {{ mode() === 'required' ? 'Justificativa (obrigatória pela política do estágio)' : 'Justificativa (opcional)' }}
          </label>
          <textarea
            id="ca-justification"
            class="ca-textarea"
            rows="3"
            [attr.maxlength]="max"
            [attr.aria-required]="mode() === 'required'"
            aria-describedby="ca-justification-help"
            [ngModel]="justification()"
            (ngModelChange)="justification.set($event)"
            name="justification"
            placeholder="Ex.: janela de manutenção do banco, chamado 1234"
          ></textarea>
          <span id="ca-justification-help" class="app-caption ca-help">
            <span>De {{ min }} a {{ max }} caracteres. Marca o container como {{ intentionalWord() }} intencionalmente e fica na auditoria; dados sensíveis são mascarados.</span>
            <span class="alt-num" aria-hidden="true">{{ justification().trim().length }}/{{ max }}</span>
          </span>
        </div>
      }
    </dok-drawer>
  `,
})
export class ConfirmActionDrawer {
  readonly open = input(false);
  readonly action = input.required<ContainerAction>();
  readonly eligibility = input.required<Eligibility>();
  readonly production = input(false);
  readonly busy = input(false);
  /** Emite a justificativa informada (aparada) ou null quando vazia. */
  readonly confirmed = output<string | null>();
  readonly cancelled = output<void>();

  private readonly policy = inject(JustificationPolicyService);
  protected readonly limit = BATCH_LIMIT;
  protected readonly min = JUSTIFICATION_MIN;
  protected readonly max = JUSTIFICATION_MAX;
  protected readonly typed = signal('');
  protected readonly justification = signal('');
  protected readonly mode = computed(() =>
    justificationMode(this.action(), this.eligibility().eligible.map(item => item.stage), this.policy.required()),
  );
  protected readonly intentionalWord = computed(() => (this.action() === 'pause' ? 'pausado' : this.action() === 'delete' ? 'removido' : 'parado'));
  protected readonly verb = computed(() => ACTION_LABEL[this.action()].verb);
  protected readonly icon = computed(() => ACTION_LABEL[this.action()].icon);
  protected readonly eyebrow = computed(() => (this.action() === 'delete' ? 'Ação destrutiva' : 'Ação em containers'));
  protected readonly title = computed(() => {
    const count = this.eligibility().eligible.length;
    const verb = this.verb();
    return count === 1 ? `${verb} ${this.eligibility().eligible[0].name}?` : `${verb} ${count} containers?`;
  });
  protected readonly subtitle = computed(() =>
    this.eligibility().eligible.length === 0 ? 'Nenhum container selecionado é elegível para esta ação.' : 'Cada container recebe um comando individual, com auditoria.',
  );
  protected readonly chips = computed(() => (this.production() ? [{ tone: 'warning', icon: 'shield', label: 'Produção' }] : []));
  protected readonly needsTypedConfirmation = computed(() => this.production() && this.eligibility().eligible.length > 1);
  protected readonly canConfirm = computed(() => {
    const count = this.eligibility().eligible.length;
    if (count === 0 || count > this.limit) return false;
    if (!justificationValid(this.justification(), this.mode())) return false;
    if (this.action() === 'delete' && this.needsTypedConfirmation()) return String(this.typed()).trim() === String(count);
    return true;
  });

  constructor() {
    effect(() => {
      if (this.open()) {
        this.typed.set('');
        this.justification.set('');
        void this.policy.load();
      }
    });
  }

  protected confirm(): void {
    if (!this.canConfirm()) return;
    const text = this.mode() === 'none' ? '' : this.justification().trim();
    this.confirmed.emit(text.length ? text : null);
  }
}
