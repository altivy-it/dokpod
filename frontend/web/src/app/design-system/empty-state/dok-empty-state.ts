import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { DokIcon } from '../icon/dok-icon';
import { tone } from '../tone';

export type EmptyStateKind = 'empty' | 'error' | 'forbidden' | 'offline' | 'stale' | 'loading' | 'soon';

const PRESETS: Record<EmptyStateKind, { tone: string; icon: string; title: string }> = {
  loading: { tone: 'accent', icon: 'refresh', title: 'Carregando' },
  empty: { tone: 'neutral', icon: 'search', title: 'Nenhum registro encontrado' },
  error: { tone: 'error', icon: 'alert', title: 'Não foi possível carregar' },
  forbidden: { tone: 'error', icon: 'lock', title: 'Acesso negado' },
  offline: { tone: 'warning', icon: 'wifi-off', title: 'Sem conexão com o plano de controle' },
  stale: { tone: 'warning', icon: 'clock', title: 'Dados desatualizados' },
  soon: { tone: 'accent', icon: 'clock', title: 'Em breve' },
};

/** Estado vazio/erro/carregando (EmptyStates do kit). Ação opcional no slot. */
@Component({
  selector: 'dok-empty-state',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DokIcon],
  host: { style: 'display: block' },
  template: `
    <div
      class="alt-empty-states__item"
      [class.alt-glass]="!bare()"
      [class.alt-empty-states__item--compact]="compact()"
      [attr.role]="role()"
      [attr.aria-live]="kind() === 'loading' ? 'polite' : null"
    >
      <div class="alt-empty-states__icon" [style.color]="spec().color" [style.background]="spec().background">
        @if (kind() === 'loading') {
          <span class="alt-empty-states__spinner" aria-hidden="true"></span>
        } @else {
          <dok-icon [name]="icon() ?? preset().icon" [size]="compact() ? 18 : 20" />
        }
      </div>
      @if (code()) {
        <span class="alt-empty-states__code alt-num">{{ code() }}</span>
      }
      <div class="alt-empty-states__title">{{ title() ?? preset().title }}</div>
      @if (detail()) {
        <div class="alt-empty-states__desc">{{ detail() }}</div>
      }
      <div class="alt-empty-states__action"><ng-content /></div>
    </div>
  `,
})
export class DokEmptyState {
  readonly kind = input<EmptyStateKind>('empty');
  readonly title = input<string | null>(null);
  readonly detail = input<string | null>(null);
  readonly code = input<string | null>(null);
  readonly icon = input<string | null>(null);
  readonly compact = input(false);
  readonly bare = input(false);

  protected readonly preset = computed(() => PRESETS[this.kind()]);
  protected readonly spec = computed(() => tone(this.preset().tone));
  protected readonly role = computed(() =>
    this.kind() === 'error' || this.kind() === 'forbidden' ? 'alert' : this.kind() === 'loading' ? 'status' : null,
  );
}
