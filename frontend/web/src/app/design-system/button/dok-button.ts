import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { DokIcon } from '../icon/dok-icon';

export type ButtonVariant = 'primary' | 'secondary' | 'outline' | 'ghost' | 'text' | 'danger';
export type ButtonSize = 'xs' | 'sm' | 'md' | 'lg';

/**
 * Botão do kit Altivy aplicado sobre `<button>` ou `<a>` nativos, preservando a semântica.
 * Botão só-ícone (`icon`) exige `aria-label` no elemento hospedeiro.
 */
@Component({
  selector: 'button[dok-button], a[dok-button]',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DokIcon],
  host: {
    '[class]': 'classes()',
    '[attr.aria-busy]': "loading() ? 'true' : null",
    '[attr.disabled]': "(disabled() || loading()) ? '' : null",
    '[attr.aria-disabled]': "(disabled() || loading()) ? 'true' : null",
  },
  template: `
    @if (loading()) {
      <svg class="alt-btn__spin" [attr.width]="iconSize()" [attr.height]="iconSize()" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" aria-hidden="true"><path d="M21 12a9 9 0 1 1-9-9" /></svg>
    } @else if (icon()) {
      <dok-icon [name]="icon()!" [size]="iconSize()" />
    } @else if (startIcon()) {
      <dok-icon [name]="startIcon()!" [size]="iconSize()" />
    }
    @if (!icon()) {
      <ng-content />
    }
    @if (endIcon() && !icon()) {
      <dok-icon [name]="endIcon()!" [size]="iconSize()" />
    }
  `,
})
export class DokButton {
  readonly variant = input<ButtonVariant>('outline');
  readonly size = input<ButtonSize>('sm');
  readonly startIcon = input<string | null>(null);
  readonly endIcon = input<string | null>(null);
  readonly icon = input<string | null>(null);
  readonly loading = input(false);
  readonly disabled = input(false);
  readonly block = input(false);

  protected readonly iconSize = computed(() => (this.size() === 'lg' ? 17 : this.size() === 'xs' ? 14 : 16));

  protected readonly classes = computed(() =>
    [
      'alt-btn',
      `alt-btn--${this.variant()}`,
      this.size() !== 'md' ? `alt-btn--${this.size()}` : '',
      this.icon() ? 'alt-btn--icon' : '',
      this.block() ? 'alt-btn--block' : '',
      this.loading() ? 'alt-btn--loading' : '',
    ]
      .filter(Boolean)
      .join(' '),
  );
}
