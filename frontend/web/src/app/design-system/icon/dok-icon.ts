import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { ICON_PATHS, type IconName } from './icon-paths';

@Component({
  selector: 'dok-icon',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { style: 'display: contents' },
  template: `
    <svg
      class="alt-icon"
      [attr.width]="size()"
      [attr.height]="size()"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      [attr.stroke-width]="strokeWidth()"
      stroke-linecap="round"
      stroke-linejoin="round"
      [attr.aria-hidden]="label() ? null : 'true'"
      [attr.role]="label() ? 'img' : null"
      [attr.aria-label]="label()"
      focusable="false"
    >
      @for (segment of segments(); track $index) {
        <path [attr.d]="segment" />
      }
    </svg>
  `,
})
export class DokIcon {
  readonly name = input.required<IconName | string>();
  readonly size = input(18);
  readonly strokeWidth = input(2);
  readonly label = input<string | null>(null);

  protected readonly segments = computed(() => {
    const d = (ICON_PATHS as Record<string, string>)[this.name()] ?? ICON_PATHS.circle;
    return d.split(/(?=M)/);
  });
}
