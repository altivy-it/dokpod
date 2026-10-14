import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { DokIcon } from '../icon/dok-icon';
import { tone } from '../tone';

const STATUS_LABEL: Record<string, string> = { error: 'Crítico', warning: 'Atenção', success: 'Ok', info: 'Info' };
let nextId = 0;

/** Card de vidro (CardBase do kit). Conteúdo no slot padrão, ação de cabeçalho opcional. */
@Component({
  selector: 'dok-card',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DokIcon],
  host: { style: 'display: flex; flex-direction: column; min-width: 0' },
  template: `
    <section class="alt-card-base alt-glass" [attr.aria-labelledby]="titleId" [attr.aria-busy]="loading() ? 'true' : null" style="flex: 1 1 auto">
      <header class="alt-card-base__head">
        @if (status(); as s) {
          <span class="alt-card-base__status" [style.color]="s.color" [attr.title]="s.label">
            <dok-icon [name]="s.icon" [size]="15" [label]="s.label" />
          </span>
        }
        <h3 class="alt-card-base__title" [id]="titleId">{{ title() }}</h3>
        <ng-content select="[cardHeader]" />
        @if (actionLabel()) {
          <button type="button" class="alt-card-base__action" (click)="action.emit()">
            {{ actionLabel() }}<dok-icon name="chevron-right" [size]="14" />
          </button>
        }
      </header>
      @if (loading()) {
        <div class="alt-card-base__progress" role="progressbar" aria-label="Carregando"></div>
      }
      <div class="alt-card-base__collapse">
        <div class="alt-card-base__inner">
          <div class="alt-card-base__body"><ng-content /></div>
        </div>
      </div>
    </section>
  `,
})
export class DokCard {
  readonly title = input.required<string>();
  readonly color = input<string | null>(null);
  readonly statusLabel = input<string | null>(null);
  readonly actionLabel = input<string | null>(null);
  readonly loading = input(false);
  readonly action = output<void>();

  protected readonly titleId = `dok-card-${++nextId}`;
  protected readonly status = computed(() => {
    const color = this.color();
    if (!color || !(color in STATUS_LABEL)) return null;
    const spec = tone(color);
    return { color: spec.color, icon: spec.icon, label: this.statusLabel() ?? STATUS_LABEL[color] };
  });
}
