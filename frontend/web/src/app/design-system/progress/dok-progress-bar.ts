import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/** Barra de uso (`ct-bar`), em aviso a partir de `warnAt`. */
@Component({
  selector: 'dok-progress-bar',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { style: 'display: block; flex: 1; min-width: 0' },
  template: `
    <div
      class="ct-bar"
      [class.ct-bar--warn]="warn()"
      role="progressbar"
      aria-valuemin="0"
      aria-valuemax="100"
      [attr.aria-valuenow]="clamped()"
      [attr.aria-label]="label()"
    >
      <span [style.width.%]="clamped()"></span>
    </div>
  `,
})
export class DokProgressBar {
  readonly value = input(0);
  readonly warnAt = input(80);
  readonly label = input<string | null>(null);

  protected readonly clamped = computed(() => Math.max(0, Math.min(100, this.value() ?? 0)));
  protected readonly warn = computed(() => this.clamped() >= this.warnAt());
}
