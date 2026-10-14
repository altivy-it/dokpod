import { ChangeDetectionStrategy, Component, computed, effect, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import type { ContainerAction } from '../../data-access/control-plane-api.service';
import { DokDrawer } from '../../design-system/drawer/dok-drawer';
import { DokField } from '../../design-system/field/dok-field';
import { DokIcon } from '../../design-system/icon/dok-icon';
import { ACTION_LABEL, BATCH_LIMIT, type Eligibility } from './container-actions.service';

/**
 * Confirmação contextual de ação (spec 010 FR-007): lista afetados e ignorados, declara que volumes
 * não são removidos e, para remover mais de um container em Produção, exige digitar a quantidade.
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
      (primary)="confirmed.emit()"
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
    </dok-drawer>
  `,
})
export class ConfirmActionDrawer {
  readonly open = input(false);
  readonly action = input.required<ContainerAction>();
  readonly eligibility = input.required<Eligibility>();
  readonly production = input(false);
  readonly busy = input(false);
  readonly confirmed = output<void>();
  readonly cancelled = output<void>();

  protected readonly limit = BATCH_LIMIT;
  protected readonly typed = signal('');
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
    if (this.action() === 'delete' && this.needsTypedConfirmation()) return String(this.typed()).trim() === String(count);
    return true;
  });

  constructor() {
    effect(() => {
      if (this.open()) this.typed.set('');
    });
  }
}
