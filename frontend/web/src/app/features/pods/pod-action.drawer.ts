import { ChangeDetectionStrategy, Component, computed, effect, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { JUSTIFICATION_MAX, JUSTIFICATION_MIN, JustificationPolicyService, justificationMode, justificationValid } from '../../core/justification-policy.service';
import type { PodAction } from '../../data-access/generated/control-plane';
import { DokDrawer } from '../../design-system/drawer/dok-drawer';
import { DokIcon } from '../../design-system/icon/dok-icon';
import { POD_BATCH_LIMIT, type PodEligibility } from './pod-actions.service';
import { POD_ACTION } from './pod-labels';

/**
 * Confirmação de ação em pods (spec 021 US3): lista afetados e ignorados (inclusive o pod do agente) e pede
 * justificativa para parar e pausar conforme a política do estágio (spec 016).
 */
@Component({
  selector: 'app-pod-action-drawer',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DokDrawer, DokIcon, FormsModule],
  styles: `
    .pa-list { margin: 0; padding: 0; list-style: none; display: flex; flex-direction: column; max-height: 240px; overflow: auto; border: 1px solid var(--hairline); border-radius: var(--radius-sm); }
    .pa-list li { display: flex; justify-content: space-between; gap: 12px; padding: 7px 10px; border-top: 1px solid var(--hairline); font-size: 13px; }
    .pa-list li:first-child { border-top: 0; }
    .pa-name { color: var(--text-strong); font-weight: 500; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .pa-meta { color: var(--muted); white-space: nowrap; }
    .pa-note { display: flex; gap: 10px; align-items: flex-start; padding: 12px 14px; border-radius: var(--radius-md); font-size: 13px; background: var(--info-tint); }
    .pa-note--warn { background: var(--warning-tint); }
    .pa-note dok-icon { display: flex; margin-top: 1px; }
    h3 { margin: 0; font-size: 13px; font-weight: 600; color: var(--muted); }
    .pa-justify { display: flex; flex-direction: column; gap: 6px; }
    .pa-textarea { width: 100%; box-sizing: border-box; resize: vertical; padding: 10px 12px; border-radius: var(--radius-md); border: 1px solid var(--glass-border-strong); background: var(--input-fill); color: var(--foreground); font: inherit; }
    .pa-textarea:focus-visible { outline: 2px solid var(--teal-light); outline-offset: 1px; }
    .pa-help { display: flex; justify-content: space-between; gap: 12px; color: var(--muted); }
  `,
  template: `
    <dok-drawer
      [open]="open()"
      eyebrow="Ação em pods"
      [title]="title()"
      [subtitle]="eligibility().eligible.length ? 'Cada pod recebe um comando individual, com auditoria. A ação vale para todos os containers do pod.' : 'Nenhum pod selecionado é elegível para esta ação.'"
      [chips]="production() ? [{ tone: 'warning', icon: 'shield', label: 'Produção' }] : []"
      [primaryLabel]="eligibility().eligible.length ? verb() : null"
      [primaryIcon]="icon()"
      [primaryDisabled]="!canConfirm()"
      [primaryLoading]="busy()"
      width="520px"
      (closed)="cancelled.emit()"
      (primary)="confirm()"
    >
      @if (eligibility().eligible.length > limit) {
        <div class="pa-note pa-note--warn" role="alert"><dok-icon name="alert" [size]="16" />O lote aceita no máximo {{ limit }} pods. Reduza a seleção.</div>
      }
      @if (eligibility().eligible.length) {
        <h3>{{ eligibility().eligible.length === 1 ? 'Pod afetado' : eligibility().eligible.length + ' pods afetados' }}</h3>
        <ul class="pa-list">
          @for (item of eligibility().eligible; track item.environmentId + '/' + item.podId) {
            <li><span class="pa-name">{{ item.name }}</span><span class="pa-meta">{{ item.containers.length }} containers · {{ item.environmentName }}</span></li>
          }
        </ul>
      }
      @if (eligibility().skipped.length) {
        <h3>{{ eligibility().skipped.length === 1 ? '1 ignorado' : eligibility().skipped.length + ' ignorados' }}</h3>
        <ul class="pa-list">
          @for (entry of eligibility().skipped; track entry.item.environmentId + '/' + entry.item.podId) {
            <li><span class="pa-name">{{ entry.item.name }} · {{ entry.item.environmentName }}</span><span class="pa-meta">{{ entry.reason }}</span></li>
          }
        </ul>
      }
      @if (mode() !== 'none' && eligibility().eligible.length) {
        <div class="pa-justify">
          <label for="pa-justification" class="alt-field__label">
            {{ mode() === 'required' ? 'Justificativa (obrigatória pela política do estágio)' : 'Justificativa (opcional)' }}
          </label>
          <textarea
            id="pa-justification"
            class="pa-textarea"
            rows="3"
            [attr.maxlength]="max"
            [attr.aria-required]="mode() === 'required'"
            aria-describedby="pa-justification-help"
            [ngModel]="justification()"
            (ngModelChange)="justification.set($event)"
            name="justification"
            placeholder="Ex.: janela de manutenção do serviço, chamado 1234"
          ></textarea>
          <span id="pa-justification-help" class="app-caption pa-help">
            <span>De {{ min }} a {{ max }} caracteres. Fica na auditoria; dados sensíveis são mascarados.</span>
            <span class="alt-num" aria-hidden="true">{{ justification().trim().length }}/{{ max }}</span>
          </span>
        </div>
      }
    </dok-drawer>
  `,
})
export class PodActionDrawer {
  readonly open = input(false);
  readonly action = input.required<PodAction>();
  readonly eligibility = input.required<PodEligibility>();
  readonly busy = input(false);
  readonly confirmed = output<string | null>();
  readonly cancelled = output<void>();

  private readonly policy = inject(JustificationPolicyService);
  protected readonly limit = POD_BATCH_LIMIT;
  protected readonly min = JUSTIFICATION_MIN;
  protected readonly max = JUSTIFICATION_MAX;
  protected readonly justification = signal('');
  protected readonly mode = computed(() => justificationMode(this.action(), this.eligibility().eligible.map(item => item.stage), this.policy.required()));
  protected readonly verb = computed(() => POD_ACTION[this.action()].verb);
  protected readonly icon = computed(() => POD_ACTION[this.action()].icon);
  protected readonly production = computed(() => this.eligibility().eligible.some(item => item.stage === 'production'));
  protected readonly title = computed(() => {
    const eligible = this.eligibility().eligible;
    return eligible.length === 1 ? `${this.verb()} o pod ${eligible[0].name}?` : `${this.verb()} ${eligible.length} pods?`;
  });
  protected readonly canConfirm = computed(() => {
    const count = this.eligibility().eligible.length;
    return count > 0 && count <= this.limit && justificationValid(this.justification(), this.mode());
  });

  constructor() {
    effect(() => {
      if (this.open()) {
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
